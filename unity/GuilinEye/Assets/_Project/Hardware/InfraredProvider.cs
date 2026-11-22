using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuilinEye.Hardware
{
    /// <summary>
    /// 大厅四角红外适配器：只做触发与人数计数，不做定位、不参与画面生成。
    /// 现场“摄像头调试软件”输出 TUIO 时，按光标数量聚合；多台红外可挂多个端点。
    /// </summary>
    public class InfraredProvider : ISensorProvider
    {
        Action<SensorEvent> _emit;
        readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        readonly Dictionary<string, string> _active = new Dictionary<string, string>();
        readonly object _gate = new object();
        int _count;

        public string SensorId { get { return "IR"; } }
        public SensorCapabilities Capabilities { get { return SensorCapabilityMap.FromStrings(new[] { "presence", "count" }); } }
        public bool Online { get { return _subscriptions.Count > 0; } }

        public void StartProvider(Action<SensorEvent> emit)
        {
            _emit = emit;
            var endpoints = HardwareSettings.Infrared();
            if (endpoints == null || endpoints.Length == 0)
            {
                Debug.LogWarning("[IR] 未配置红外端点，保持 Mock");
                return;
            }

            for (var i = 0; i < endpoints.Length; i++)
            {
                var ep = endpoints[i];
                if (ep == null || !ep.enabled) continue;
                var captured = ep;
                _subscriptions.Add(TuioHub.Subscribe(captured.port, c => OnCursor(captured, c), c => OnCursorRemoved(captured, c)));
                Debug.Log("[IR] TUIO UDP :" + captured.port + " source=" +
                          (string.IsNullOrEmpty(captured.source) ? "(any)" : captured.source) +
                          " 仅 presence/count，不做定位");
            }
        }

        public void StopProvider()
        {
            for (var i = 0; i < _subscriptions.Count; i++) _subscriptions[i].Dispose();
            _subscriptions.Clear();
            lock (_gate)
            {
                _active.Clear();
                _count = 0;
            }
            _emit = null;
        }

        void OnCursor(TuioEndpoint ep, TuioCursor c)
        {
            if (c == null || _emit == null) return;
            if (!MatchesSource(ep, c)) return;
            var key = KeyOf(ep, c);
            lock (_gate)
            {
                if (_active.ContainsKey(key)) return;
                _active[key] = ep.sensorId;
                _count = _active.Count;
            }
            Emit("presence", ZoneOf(ep, c), key, _count);
        }

        void OnCursorRemoved(TuioEndpoint ep, TuioCursor c)
        {
            if (c == null || _emit == null) return;
            if (!MatchesSource(ep, c)) return;
            var key = KeyOf(ep, c);
            lock (_gate)
            {
                if (!_active.Remove(key)) return;
                _count = _active.Count;
            }
            Emit("leave", ZoneOf(ep, c), key, _count);
        }

        static string KeyOf(TuioEndpoint ep, TuioCursor c)
        {
            var source = string.IsNullOrEmpty(c.Source) ? "" : c.Source.Trim();
            return ep.sensorId + "@" + ep.port + ":" + source + ":" + c.Id;
        }

        string ZoneOf(TuioEndpoint ep, TuioCursor c)
        {
            if (ep != null && ep.useGridZones && c != null)
            {
                var cols = Mathf.Max(1, ep.gridCols);
                var rows = Mathf.Max(1, ep.gridRows);
                var nx = Mathf.Clamp01(ep.flipX ? 1f - c.X : c.X);
                var ny = Mathf.Clamp01(ep.flipY ? 1f - c.Y : c.Y);
                var col = Mathf.Min(cols - 1, (int)(nx * cols));
                var row = Mathf.Min(rows - 1, (int)(ny * rows));
                return "F" + (row * cols + col + 1);
            }
            return string.IsNullOrEmpty(ep.defaultZone) ? "F5" : ep.defaultZone;
        }

        bool MatchesSource(TuioEndpoint ep, TuioCursor c)
        {
            if (string.IsNullOrEmpty(ep.source)) return true;
            if (string.IsNullOrEmpty(c.Source)) return ep.allowNoSource;
            return string.Equals(c.Source, ep.source, StringComparison.OrdinalIgnoreCase)
                || c.Source.StartsWith(ep.source + "@", StringComparison.OrdinalIgnoreCase);
        }

        void Emit(string type, string zone, string trackingId, int count)
        {
            Debug.Log("[IR] " + type + " " + zone + " count=" + count + " id=" + trackingId);
            _emit(new SensorEvent
            {
                sensorId = "IR", type = type, zoneId = zone,
                hasPosition = false, confidence = 1f, trackingId = trackingId,
                count = count, timestampMs = (long)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond)
            });
        }

        public void SimulateStep(string zone)
        {
            if (_emit == null) return;
            _emit(new SensorEvent
            {
                sensorId = "IR", type = "presence", zoneId = zone, confidence = 1f,
                timestampMs = (long)(Time.realtimeSinceStartup * 1000)
            });
        }
    }
}
