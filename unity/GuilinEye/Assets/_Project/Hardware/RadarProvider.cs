using System;
using System.Collections.Generic;
using GuilinEye.Core;
using UnityEngine;

namespace GuilinEye.Hardware
{
    /// <summary>
    /// 北阳 10m 雷达适配器：现场“雷达调试软件”输出 TUIO 时，直接收 UDP。
    /// R1 北墙 3400—8000；R2 东墙 1200—4800（题名石 4800—4850 仍不覆盖）。
    /// 原点/跨度/翻转都在 config/hardware.json，不在代码里写死第二套坐标。
    /// </summary>
    public class RadarProvider : ISensorProvider
    {
        readonly string _id;
        readonly Dictionary<int, SensorEvent> _last = new Dictionary<int, SensorEvent>();
        readonly object _lastGate = new object();
        Action<SensorEvent> _emit;
        IDisposable _subscription;
        TuioEndpoint _endpoint;
        bool _sourceWarning;
        bool _firstCursor;
        bool _zoneWarning;
        bool _firstEmit;

        public RadarProvider(string id) { _id = id; }
        public string SensorId { get { return _id; } }
        public SensorCapabilities Capabilities { get { return SensorCapabilityMap.FromStrings(new[] { "point2d", "tracking" }); } }
        public bool Online { get { return _subscription != null; } }

        public void StartProvider(Action<SensorEvent> emit)
        {
            _emit = emit;
            _endpoint = HardwareSettings.Radar(_id);
            if (_endpoint == null || !_endpoint.enabled)
            {
                Debug.LogWarning("[" + _id + "] 未配置雷达端点，保持 Mock");
                return;
            }

            _subscription = TuioHub.Subscribe(_endpoint.port, OnCursor, OnCursorRemoved);
            Debug.Log("[" + _id + "] TUIO UDP :" + _endpoint.port +
                      " source=" + (string.IsNullOrEmpty(_endpoint.source) ? "(any)" : _endpoint.source) +
                      " 画布X=" + _endpoint.originX + "—" + (_endpoint.originX + _endpoint.spanX) + "mm");
        }

        public void StopProvider()
        {
            if (_subscription != null) { _subscription.Dispose(); _subscription = null; }
            lock (_lastGate) { _last.Clear(); }
            _emit = null;
        }

        void OnCursor(TuioCursor c)
        {
            if (c == null || _emit == null || !Matches(c)) return;

            if (!_firstCursor)
            {
                _firstCursor = true;
                Debug.Log("[" + _id + "] TUIO 首光标 id=" + c.Id + " xy=" + c.X + "," + c.Y + " source=" + (c.Source ?? ""));
            }

            var x = MapX(c.X);
            var y = MapY(c.Y);
            var zone = FindZone(x, y);
            if (zone == null)
            {
                if (!_zoneWarning)
                {
                    _zoneWarning = true;
                    Debug.LogWarning("[" + _id + "] 光标 " + c.Id + " 映射到 " + x + "," + y + "mm，不在任何 " + _id + " 子区域；检查 hardware.json 的 origin/span/flip");
                }
                // 雷达覆盖边缘或标定未完成时不伪造触点；只保留最近一次，等标定。
                return;
            }

            var e = new SensorEvent
            {
                sensorId = _id,
                type = "point",
                zoneId = zone.id,
                x = x,
                y = y,
                hasPosition = true,
                confidence = 0.9f,
                trackingId = _id + "-" + c.Id,
                timestampMs = c.TimestampMs
            };
            lock (_lastGate) { _last[c.Id] = e; }
            _emit(e);
            if (!_firstEmit)
            {
                _firstEmit = true;
                Debug.Log("[" + _id + "] 已入队 point zone=" + zone.id + " x=" + x + " y=" + y);
            }
        }

        void OnCursorRemoved(TuioCursor c)
        {
            if (c == null || _emit == null || !Matches(c)) return;
            SensorEvent last = null;
            lock (_lastGate)
            {
                if (_last.TryGetValue(c.Id, out last)) _last.Remove(c.Id);
            }
            if (last != null)
            {
                _emit(new SensorEvent
                {
                    sensorId = _id, type = "leave", zoneId = last.zoneId,
                    x = last.x, y = last.y, hasPosition = true,
                    confidence = 0.8f, trackingId = last.trackingId,
                    timestampMs = c.TimestampMs
                });
            }
        }

        bool Matches(TuioCursor c)
        {
            if (c.Id < _endpoint.idMin && _endpoint.idMin >= 0) return false;
            if (c.Id > _endpoint.idMax && _endpoint.idMax >= 0) return false;
            if (string.IsNullOrEmpty(_endpoint.source)) return true;
            if (string.IsNullOrEmpty(c.Source))
            {
                if (!_sourceWarning)
                {
                    _sourceWarning = true;
                    Debug.LogWarning("[" + _id + "] TUIO 未带 source；R1/R2 共用端口时按归一化X分段（R1≥0.5，R2<0.5）");
                }
                if (!_endpoint.allowNoSource) return false;
                if (_endpoint.fallbackMinX >= 0f &&
                    (c.X < _endpoint.fallbackMinX || c.X > _endpoint.fallbackMaxX)) return false;
                return true;
            }
            return string.Equals(c.Source, _endpoint.source, StringComparison.OrdinalIgnoreCase)
                || c.Source.StartsWith(_endpoint.source + "@", StringComparison.OrdinalIgnoreCase);
        }

        float MapX(float nx)
        {
            var n = _endpoint.flipX ? 1f - nx : nx;
            return _endpoint.originX + n * _endpoint.spanX;
        }

        float MapY(float ny)
        {
            var n = _endpoint.flipY ? 1f - ny : ny;
            return _endpoint.originY + n * _endpoint.spanY;
        }

        ZoneConfig FindZone(float x, float y)
        {
            if (ConfigDatabase.Sensors == null || ConfigDatabase.Sensors.zones == null) return null;
            ZoneConfig best = null;
            var bestArea = float.MaxValue;
            foreach (var z in ConfigDatabase.Sensors.zones)
            {
                if (z == null || z.sensor != _id || z.rect == null) continue;
                if (!z.rect.Contains(x, y)) continue;
                var area = z.rect.w * z.rect.h;
                if (area < bestArea) { best = z; bestArea = area; }
            }
            return best;
        }

        public void SimulateTouch(string zone, float x, float y)
        {
            if (_emit == null) return;
            _emit(new SensorEvent
            {
                sensorId = _id, type = "point", zoneId = zone, x = x, y = y,
                hasPosition = true, confidence = 0.9f,
                timestampMs = (long)(Time.realtimeSinceStartup * 1000)
            });
        }
    }
}
