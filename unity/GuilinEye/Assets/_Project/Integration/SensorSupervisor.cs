using System;
using System.Collections.Generic;
using System.Text;
using GuilinEye.Core;
using GuilinEye.Hardware;
using UnityEngine;

namespace GuilinEye.Integration
{
    /// <summary>
    /// BP-03 传感监督：订阅 SensorHub，统计每个传感器的存活/速率/最后事件，
    /// 做 stale 判定与能力查询，向运维广播。模块不直接依赖厂商 SDK。
    /// </summary>
    public class SensorSupervisor : MonoBehaviour
    {
        [Serializable]
        public class SensorRuntime
        {
            public string id;
            public string device;
            public string[] supports;
            public long lastTimestampMs;
            public long eventCount;
            public bool stale;
            public bool seen;
        }

        public static SensorSupervisor Instance { get; private set; }

        [Tooltip("超过该毫秒数无事件即判 stale；IR 只有触发，允许更长。")]
        public int staleMs = 500;

        readonly Dictionary<string, SensorRuntime> _sensors = new Dictionary<string, SensorRuntime>();
        readonly Dictionary<string, string> _zoneSensor = new Dictionary<string, string>();
        IDisposable _sub;
        float _startedAt;
        long _startedAtMs;

        public int SensorCount { get { return _sensors.Count; } }
        public int StaleCount { get; private set; }
        public bool Healthy { get { return StaleCount == 0; } }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Build()
        {
            _startedAt = Time.realtimeSinceStartup;
            _startedAtMs = NowMs();
            _sensors.Clear();
            _zoneSensor.Clear();
            if (ConfigDatabase.Sensors != null)
            {
                if (ConfigDatabase.Sensors.capabilities != null)
                {
                    foreach (var kv in ConfigDatabase.Sensors.capabilities)
                    {
                        var cap = kv.Value;
                        _sensors[kv.Key] = new SensorRuntime
                        {
                            id = kv.Key,
                            device = cap != null ? cap.device : "",
                            supports = cap != null ? cap.supports : null
                        };
                    }
                }
                if (ConfigDatabase.Sensors.zones != null)
                {
                    foreach (var z in ConfigDatabase.Sensors.zones)
                        if (z != null) _zoneSensor[z.id] = z.sensor;
                }
            }
            foreach (var s in _sensors.Values) s.lastTimestampMs = _startedAtMs;

            var hub = SensorHub.Instance;
            if (hub == null)
            {
                var go = new GameObject("SensorHub");
                hub = go.AddComponent<SensorHub>();
            }
            _sub = hub.Subscribe(OnSensor);
            Debug.Log("[BP-03] 传感器 " + _sensors.Count + "，zone " + _zoneSensor.Count);
        }

        void OnSensor(SensorEvent e)
        {
            if (e == null || string.IsNullOrEmpty(e.sensorId)) return;
            var s = _sensors.ContainsKey(e.sensorId) ? _sensors[e.sensorId] : null;
            if (s == null)
            {
                s = new SensorRuntime { id = e.sensorId };
                _sensors[e.sensorId] = s;
            }
            s.seen = true;
            s.eventCount++;
            // 统一使用本机单调接收时间；厂商的 epoch ms 与 Time.realtimeSinceStartup 不能混算。
            s.lastTimestampMs = NowMs();
            if (s.stale)
            {
                s.stale = false;
                VenueBus.Publish("sensor.recovered", e.sensorId, e.zoneId);
            }
        }

        void Update()
        {
            var now = NowMs();
            var stale = 0;
            foreach (var kv in _sensors)
            {
                var s = kv.Value;
                var continuous = s.id != null && s.id.StartsWith("S");
                var provider = SensorHub.Instance != null ? SensorHub.Instance.Get(s.id) : null;
                var online = provider != null && provider.Online;
                var last = s.lastTimestampMs > 0 ? s.lastTimestampMs : _startedAtMs;
                var age = now - last;
                var threshold = continuous ? 2000L : 5000L;

                // 事件型雷达/红外：没人经过本来就没有报文，不能按“无事件”判掉线。
                if (!continuous && online)
                {
                    if (s.stale)
                    {
                        s.stale = false;
                        VenueBus.Publish("sensor.recovered", s.id, null);
                    }
                    continue;
                }

                // 启动期给 Femto 2.5s 枚举时间，避免开机 0.5s 就全员报 stale。
                if (!s.seen && continuous && age < 2500L) continue;

                if (age > threshold)
                {
                    if (!s.stale)
                    {
                        s.stale = true;
                        VenueBus.Publish("sensor.stale", s.id, new SensorStale(s.id, age));
                        Debug.LogWarning("[BP-03] " + s.id + " stale " + age + " ms" + (online ? "" : "（provider offline）"));
                    }
                }
                else if (s.stale)
                {
                    s.stale = false;
                    VenueBus.Publish("sensor.recovered", s.id, null);
                }
                if (s.stale) stale++;
            }
            StaleCount = stale;
        }

        public bool Supports(string sensorId, string capability)
        {
            var s = _sensors.ContainsKey(sensorId) ? _sensors[sensorId] : null;
            if (s == null || s.supports == null || capability == null) return false;
            for (var i = 0; i < s.supports.Length; i++)
                if (s.supports[i] == capability) return true;
            return false;
        }

        public bool IsStale(string sensorId)
        {
            var s = _sensors.ContainsKey(sensorId) ? _sensors[sensorId] : null;
            return s == null || s.stale;
        }

        public string SensorForZone(string zoneId)
        {
            string s = null;
            return zoneId != null && _zoneSensor.TryGetValue(zoneId, out s) ? s : null;
        }

        public SensorRuntime Get(string sensorId)
        {
            SensorRuntime s = null;
            return sensorId != null && _sensors.TryGetValue(sensorId, out s) ? s : null;
        }

        public string SnapshotJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"healthy\":").Append(Healthy ? "true" : "false")
              .Append(",\"stale\":").Append(StaleCount)
              .Append(",\"sensors\":[");
            var first = true;
            foreach (var kv in _sensors)
            {
                var s = kv.Value;
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"id\":\"").Append(s.id).Append("\",\"seen\":").Append(s.seen ? "true" : "false")
                  .Append(",\"stale\":").Append(s.stale ? "true" : "false")
                  .Append(",\"count\":").Append(s.eventCount).Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        static long NowMs() { return (long)(Time.realtimeSinceStartup * 1000f); }

        void OnDestroy()
        {
            if (_sub != null) _sub.Dispose();
            if (Instance == this) Instance = null;
        }
    }

    public class SensorStale
    {
        public string sensorId;
        public long ageMs;
        public SensorStale(string id, long age) { sensorId = id; ageMs = age; }
    }
}
