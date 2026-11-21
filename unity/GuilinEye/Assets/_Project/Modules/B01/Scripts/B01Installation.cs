using System.Collections.Generic;
using UnityEngine;
using GuilinEye.Core;
using GuilinEye.Hardware;

namespace GuilinEye.Modules.B01
{
    /// <summary>B-01 喀斯特：5×36s；T1 长按 / T2 掠过 / T3 悬停；三人同按彩蛋。</summary>
    public sealed class B01Installation : ModuleKit
    {
        long _years;
        readonly Dictionary<string, TouchState> _touches = new Dictionary<string, TouchState>();
        readonly Dictionary<string, float> _holdStart = new Dictionary<string, float>();
        readonly Dictionary<string, float> _lastPoint = new Dictionary<string, float>();
        readonly HashSet<string> _triggered = new HashSet<string>();

        sealed class TouchState
        {
            public string trackingId;
            public float time;
        }

        protected override void BuildModule()
        {
            AddLayer("海底", new Color(0.12f, 0.30f, 0.42f), 1, 0.9f);
            AddLayer("抬升", new Color(0.25f, 0.50f, 0.43f), 2, 0.7f);
            AddLayer("溶蚀", new Color(0.55f, 0.62f, 0.48f), 3, 0.45f);
            AddLayer("天坑", new Color(0.70f, 0.66f, 0.45f), 4, 0.25f);
            AddLayer("万年后", new Color(1f, 0.85f, 0.55f), 5, 0.10f);
        }

        protected override void OnSegment(SegmentConfig segment, float local)
        {
            var n = Mathf.Clamp(SegmentIndex, 0, 4);
            SetLayer("海底", n == 0, n == 0 ? 0.9f : 0.2f);
            SetLayer("抬升", n == 1, n == 1 ? 0.9f : 0.2f);
            SetLayer("溶蚀", n == 2, n == 2 ? 0.9f : 0.2f);
            SetLayer("天坑", n == 3, n == 3 ? 0.9f : 0.2f);
            SetLayer("万年后", n == 4, n == 4 ? 0.9f : 0.1f);
        }

        protected override void OnAction(string action, SensorEvent e)
        {
            switch (action)
            {
                case "counter":
                    _years += 10000;
                    Pulse("抬升");
                    Log("T1 长按 3s：每滴 1 万年，累计 " + _years + " 年");
                    break;
                case "dissolve":
                    Pulse("溶蚀");
                    Log("T2 掠过：CaCO₃+H₂O+CO₂⇌Ca(HCO₃)₂，200mm，3s 淡出");
                    break;
                case "moon":
                    Pulse("天坑");
                    Log("T3 悬停 0.8s：月随手水平移动，8s 落回");
                    break;
                case "easterEgg.future":
                    Pulse("万年后", 1.5f);
                    Log("三人同时长按：彩蛋「万年后」，象山被溶穿");
                    break;
            }
        }

        protected override void HandleSensor(InteractionConfig it, SensorEvent e)
        {
            if (e == null || it == null) return;
            var tid = string.IsNullOrEmpty(e.trackingId) ? "" : e.trackingId;
            var zone = string.IsNullOrEmpty(it.zone) ? e.zoneId : it.zone;
            if (e.type != "point") return;

            if (it.type == "multiTouch")
            {
                if (!string.IsNullOrEmpty(zone))
                    _touches[zone] = new TouchState { trackingId = tid, time = Time.time };
                var now = Time.time;
                var all = it.zones != null && it.zones.Length > 0;
                var ids = new HashSet<string>();
                if (it.zones != null)
                {
                    foreach (var z in it.zones)
                    {
                        var t = _touches.ContainsKey(z) ? _touches[z] : null;
                        if (t == null || now - t.time > 2f || string.IsNullOrEmpty(t.trackingId)) { all = false; break; }
                        ids.Add(t.trackingId);
                    }
                }
                // 必须是 3 个不同 trackingId，避免单人快速扫过三区误触“三人彩蛋”。
                if (all && ids.Count == it.zones.Length)
                {
                    _touches.Clear();
                    HandleAction(it.action, e);
                }
                return;
            }

            if (it.gesture == "longpress" || it.gesture == "hover")
            {
                var hold = it.gesture == "hover" && it.hover > 0f
                    ? it.hover
                    : (it.hold > 0f ? it.hold : (it.gesture == "longpress" ? 3f : 0.8f));
                var key = zone + "|" + tid; 
                float last;
                if (_lastPoint.TryGetValue(key, out last) && Time.time - last > 0.45f)
                {
                    _holdStart.Remove(key);
                    _triggered.Remove(key);
                }
                _lastPoint[key] = Time.time;

                if (_triggered.Contains(key)) return;
                float start;
                if (!_holdStart.TryGetValue(key, out start)) { _holdStart[key] = Time.time; return; }
                if (Time.time - start < hold) return;
                _holdStart.Remove(key);
                _triggered.Add(key);
                HandleAction(it.action, e);
                return;
            }

            // T2 的 sweep 目前按“进入即掠过”处理；真正的方向/速度判定留到现场雷达轨迹。
            if (it.gesture == "sweep") { HandleAction(it.action, e); return; }
            base.HandleSensor(it, e);
        }

        protected override void OnSensorLeave(InteractionConfig it, SensorEvent e)
        {
            if (e == null) return;
            var zone = string.IsNullOrEmpty(e.zoneId) ? (it != null ? it.zone : "") : e.zoneId;
            if (string.IsNullOrEmpty(zone)) return;
            _touches.Remove(zone);
            ClearTracking(zone, string.IsNullOrEmpty(e.trackingId) ? "" : e.trackingId);
        }

        void ClearTracking(string zone, string tid)
        {
            var prefix = zone + "|" + tid;
            _holdStart.Remove(prefix);
            _lastPoint.Remove(prefix);
            _triggered.Remove(prefix);
        }
    }
}
