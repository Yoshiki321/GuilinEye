using System;
using System.Collections.Generic;
using GuilinEye.Core;
using Orbbec;
using UnityEngine;

namespace GuilinEye.Hardware
{
    /// <summary>
    /// ORBBEC Femto Bolt 适配器（Orbbec SDK v1.1.9 C# / ob_csharp.dll）。
    /// 只启用深度流：presence / point2d；S1/S2 额外用深度质心左右往复做挥手启发式。
    /// 32 关节骨骼需要 Azure Kinect Body Tracking SDK，属于现场可选深化项，未在本类中伪造。
    /// </summary>
    public class FemtoBoltProvider : ISensorProvider
    {
        class WaveSample
        {
            public long timeMs;
            public float x;
        }

        readonly string _id;
        readonly List<WaveSample> _wave = new List<WaveSample>();
        readonly object _stateLock = new object();
        Action<SensorEvent> _emit;
        FemtoEndpoint _endpoint;

        Context _context;
        DeviceList _deviceList;
        Device _device;
        Pipeline _pipeline;
        Config _config;
        byte[] _depthData;
        long _lastFrameMs;
        long _nextReconnectMs;
        bool _offlineLogged;

        volatile bool _running;
        bool _present;
        long _lastValidMs;
        long _lastPointMs;
        long _lastGestureMs;
        float _lastX;
        float _lastY;
        string _lastZone;

        public FemtoBoltProvider(string id) { _id = id; }
        public string SensorId { get { return _id; } }

        public SensorCapabilities Capabilities
        {
            get
            {
                return _id == "S3"
                    ? SensorCapabilityMap.FromStrings(new[] { "presence", "region" })
                    : SensorCapabilityMap.FromStrings(new[] { "presence", "point2d", "gesture" });
            }
        }

        public bool Online { get { return _running; } }

        public void StartProvider(Action<SensorEvent> emit)
        {
            _emit = emit;
            _endpoint = HardwareSettings.Femto(_id);
            if (_endpoint == null || !_endpoint.enabled)
            {
                Debug.LogWarning("[" + _id + "] 未配置 Femto Bolt 端点，保持 Mock");
                return;
            }

            try
            {
                StartSdk();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[" + _id + "] Femto Bolt 启动失败：" + e.GetType().Name + " " + e.Message);
                Cleanup();
                _nextReconnectMs = NowMs() + 5000;
            }
        }

        /// <summary>由 SensorHub 在主线程调用：设备掉线/无帧时每 5s 重连一次。</summary>
        public void Tick()
        {
            if (_endpoint == null || !_endpoint.enabled) return;
            var now = NowMs();
            if (_running && now - _lastFrameMs > 2500)
            {
                Debug.LogWarning("[" + _id + "] 深度流 2.5s 无帧，尝试重连");
                Cleanup();
                _nextReconnectMs = now + 5000;
                return;
            }
            if (_running || now < _nextReconnectMs) return;
            try { StartSdk(); }
            catch (Exception e)
            {
                if (!_offlineLogged)
                {
                    Debug.LogWarning("[" + _id + "] Femto Bolt 重连失败：" + e.GetType().Name + " " + e.Message);
                    _offlineLogged = true;
                }
                Cleanup();
                _nextReconnectMs = now + 5000;
            }
        }

        void StartSdk()
        {
            Cleanup();
            _context = new Context();
            _deviceList = _context.QueryDeviceList();
            if (_deviceList == null || _deviceList.DeviceCount() == 0)
            {
                if (!_offlineLogged)
                {
                    Debug.LogWarning("[" + _id + "] 未发现 ORBBEC 设备；检查 USB3/光纤/USB 传输器与供电，5s 后重试");
                    _offlineLogged = true;
                }
                Cleanup();
                _nextReconnectMs = NowMs() + 5000;
                return;
            }

            _device = SelectDevice();
            if (_device == null)
            {
                Debug.LogWarning("[" + _id + "] 设备列表非空，但未选中目标设备");
                Cleanup();
                _nextReconnectMs = NowMs() + 5000;
                return;
            }

            var info = _device.GetDeviceInfo();
            _offlineLogged = false;
            Debug.Log("[" + _id + "] Femto Bolt 在线：" + info.Name() + " SN=" + info.SerialNumber() +
                      " 固件=" + info.FirmwareVersion());
            info.Dispose();

            _pipeline = new Pipeline(_device);
            var profiles = _pipeline.GetStreamProfileList(SensorType.OB_SENSOR_DEPTH);
            var profile = FindDepthProfile(profiles);
            if (profile == null)
            {
                Debug.LogWarning("[" + _id + "] 找不到深度流配置（期望 " + _endpoint.width + "x" + _endpoint.height + " Y16）");
                Cleanup();
                _nextReconnectMs = NowMs() + 5000;
                return;
            }

            _config = new Config();
            _config.EnableStream(profile);

            _running = true;
            _pipeline.Start(_config, OnFrameset);
            _lastFrameMs = NowMs();
            Debug.Log("[" + _id + "] 深度流已启动 " + profile.GetWidth() + "x" + profile.GetHeight() +
                      "@" + profile.GetFPS() + " " + profile.GetFormat() +
                      "；深度窗口 " + _endpoint.minDepthMm + "—" + _endpoint.maxDepthMm + "mm");
        }

        Device SelectDevice()
        {
            if (_deviceList == null) return null;
            if (!string.IsNullOrEmpty(_endpoint.serial))
            {
                try
                {
                    var bySn = _deviceList.GetDeviceBySN(_endpoint.serial);
                    if (bySn != null) return bySn;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[" + _id + "] 按 SN 查找失败：" + e.Message);
                }
            }

            var count = _deviceList.DeviceCount();
            if (count == 0) return null;
            var index = (uint)Mathf.Max(0, _endpoint.index);
            if (index >= count)
            {
                Debug.LogWarning("[" + _id + "] 设备枚举数量=" + count + "，请求 index=" + index +
                                 " 越界；不再回退到 0，避免抢占其它相机");
                return null;
            }
            return _deviceList.GetDevice(index);
        }

        VideoStreamProfile FindDepthProfile(StreamProfileList profiles)
        {
            if (profiles == null) return null;
            try
            {
                var exact = profiles.GetVideoStreamProfile(_endpoint.width, _endpoint.height, Format.OB_FORMAT_Y16, _endpoint.fps);
                if (exact != null) return exact;
            }
            catch (Exception) { }

            var count = profiles.ProfileCount();
            for (uint i = 0; i < count; i++)
            {
                var p = profiles.GetProfile((int)i);
                if (p == null) continue;
                if (p.GetStreamType() != StreamType.OB_STREAM_DEPTH) continue;
                var v = p.As<VideoStreamProfile>();
                if (v != null && v.GetFormat() == Format.OB_FORMAT_Y16) return v;
            }
            return null;
        }

        void OnFrameset(Frameset frameset)
        {
            if (!_running || frameset == null) return;
            _lastFrameMs = NowMs();
            DepthFrame depth = null;
            try
            {
                depth = frameset.GetDepthFrame();
                if (depth != null) ProcessDepth(depth);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[" + _id + "] 深度帧处理异常：" + e.Message);
            }
            finally
            {
                if (depth != null)
                {
                    try { depth.Dispose(); } catch { }
                }
                frameset.Dispose();
            }
        }

        void ProcessDepth(DepthFrame depth)
        {
            lock (_stateLock)
            {
            if (depth.GetFormat() != Format.OB_FORMAT_Y16) return;
            var width = (int)depth.GetWidth();
            var height = (int)depth.GetHeight();
            var dataSize = (int)depth.GetDataSize();
            if (width <= 0 || height <= 0 || dataSize < width * height * 2) return;

            if (_depthData == null || _depthData.Length != dataSize) _depthData = new byte[dataSize];
            depth.CopyData(ref _depthData);

            var scale = depth.GetValueScale();
            if (scale <= 0f) scale = 1f;

            var step = Mathf.Max(2, width / 80);
            var sampled = 0;
            var valid = 0;
            var sumX = 0f;
            var sumY = 0f;
            var minDepth = _endpoint.minDepthMm;
            var maxDepth = _endpoint.maxDepthMm;

            for (var y = step; y < height - step; y += step)
            {
                for (var x = step; x < width - step; x += step)
                {
                    sampled++;
                    var offset = (y * width + x) * 2;
                    var raw = BitConverter.ToUInt16(_depthData, offset);
                    if (raw == 0) continue;
                    var z = raw * scale;
                    if (z < minDepth || z > maxDepth) continue;
                    valid++;
                    sumX += x;
                    sumY += y;
                }
            }

            var now = NowMs();
            var enterThreshold = Mathf.Max(24f, sampled * 0.015f);
            var leaveThreshold = enterThreshold * 0.45f;
            // 已在场时用更低的 leaveThreshold，避免边界站位在 enter 阈值附近反复进出。
            var hasPerson = _present ? valid >= leaveThreshold : valid >= enterThreshold;

            if (hasPerson)
            {
                _lastValidMs = now;
                if (!_present)
                {
                    _present = true;
                    Emit("presence", _endpoint.zone, 0f, 0f, false, 1f, _id + "-presence");
                    Debug.Log("[" + _id + "] 进入感应区（有效深度点 " + valid + "/" + sampled + "）");
                }

                if (valid > 0)
                {
                    var cx = sumX / valid;
                    var cy = sumY / valid;
                    var nx = cx / Mathf.Max(1f, width - 1f);
                    var ny = cy / Mathf.Max(1f, height - 1f);
                    if (_endpoint.flipX) nx = 1f - nx;
                    _lastX = nx;
                    _lastY = ny;
                    _lastZone = _endpoint.zone;

                    if (now - _lastPointMs >= 100)
                    {
                        _lastPointMs = now;
                        var p = CanvasPoint(nx, ny);
                        Emit("point", _endpoint.zone, p.x, p.y, true, 0.85f, _id + "-" + Mathf.RoundToInt(nx * 1000f));
                    }

                    if (_endpoint.gesture) UpdateWave(nx, now);
                }
            }
            else if (_present && now - _lastValidMs > 450)
            {
                _present = false;
                _wave.Clear();
                Emit("leave", _endpoint.zone, CanvasPoint(_lastX, _lastY).x, CanvasPoint(_lastX, _lastY).y,
                    true, 0.8f, _id + "-presence");
                Debug.Log("[" + _id + "] 离开感应区");
            }
            }
        }

        void UpdateWave(float nx, long now)
        {
            _wave.Add(new WaveSample { timeMs = now, x = nx });
            while (_wave.Count > 0 && now - _wave[0].timeMs > 1200) _wave.RemoveAt(0);
            if (_wave.Count < 5) return;

            var min = float.MaxValue;
            var max = float.MinValue;
            for (var i = 0; i < _wave.Count; i++)
            {
                if (_wave[i].x < min) min = _wave[i].x;
                if (_wave[i].x > max) max = _wave[i].x;
            }
            if (max - min < 0.06f) return;

            var reversals = 0;
            var lastDir = 0f;
            for (var i = 1; i < _wave.Count; i++)
            {
                var dx = _wave[i].x - _wave[i - 1].x;
                if (Mathf.Abs(dx) < 0.015f) continue;
                var dir = Mathf.Sign(dx);
                if (lastDir != 0f && dir != lastDir) reversals++;
                lastDir = dir;
            }

            if (reversals >= 2 && now - _lastGestureMs > 1200)
            {
                _lastGestureMs = now;
                _wave.Clear();
                var p = CanvasPoint(nx, _lastY);
                Emit("gesture", _endpoint.zone, p.x, p.y, true, 0.7f, _id + "-gesture");
                Debug.Log("[" + _id + "] 深度挥手启发式：左右往复 " + reversals + " 次");
            }
        }

        Vector2 CanvasPoint(float nx, float ny)
        {
            var zone = ConfigDatabase.GetZone(_endpoint.zone);
            if (zone == null || zone.rect == null) return new Vector2(nx * 1000f, ny * 1000f);
            var x = zone.rect.x + nx * zone.rect.w;
            var y = zone.rect.y + (1f - ny) * zone.rect.h;
            return new Vector2(x, y);
        }

        void Emit(string type, string zone, float x, float y, bool hasPosition, float confidence, string trackingId)
        {
            if (_emit == null) return;
            _emit(new SensorEvent
            {
                sensorId = _id,
                type = type,
                zoneId = string.IsNullOrEmpty(zone) ? _id : zone,
                x = x, y = y,
                hasPosition = hasPosition,
                confidence = confidence,
                trackingId = trackingId,
                timestampMs = NowMs()
            });
        }

        static long NowMs()
        {
            return DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        }

        void Cleanup()
        {
            _running = false;
            if (_pipeline != null)
            {
                try { _pipeline.Stop(); } catch { }
                try { _pipeline.Dispose(); } catch { }
                _pipeline = null;
            }
            if (_config != null) { try { _config.Dispose(); } catch { } _config = null; }
            if (_device != null) { try { _device.Dispose(); } catch { } _device = null; }
            if (_deviceList != null) { try { _deviceList.Dispose(); } catch { } _deviceList = null; }
            if (_context != null) { try { _context.Dispose(); } catch { } _context = null; }
            lock (_stateLock)
            {
                _depthData = null;
                _wave.Clear();
                _present = false;
            }
        }

        public void StopProvider()
        {
            _running = false;
            Cleanup();
            lock (_stateLock) { _present = false; _wave.Clear(); }
            _emit = null;
        }

        public void SimulatePresence(string zone)
        {
            if (_emit == null) return;
            _emit(new SensorEvent { sensorId = _id, type = "presence", zoneId = zone, confidence = 1f, timestampMs = NowMs() });
        }
    }
}
