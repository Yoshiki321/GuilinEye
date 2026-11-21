using System;
using System.Collections.Generic;
using System.Text;
using GuilinEye.Core;
using GuilinEye.Shared;
using UnityEngine;

namespace GuilinEye.Integration
{
    [Serializable]
    public class OpsAck
    {
        public bool ok;
        public string message;
    }

    [Serializable]
    public class OpsHeartbeat
    {
        public string venue = "桂林之眼";
        public int modules;
        public int sensors;
        public int staleSensors;
        public float masterSeconds;
        public string segment;
        public bool paused;
        public long atUnixMs;
    }

    /// <summary>
    /// BP-04 运维桥：把 VenueBus 事件批量送到 venue-api，并提供心跳/控制命令入口。
    /// 默认关闭，避免没有服务端时每 5 s 刷错误；现场/联调时调用 StartBridge()。
    /// </summary>
    public class OpsBridge : MonoBehaviour
    {
        public static OpsBridge Instance { get; private set; }

        public string baseUrl = "http://127.0.0.1:8080";
        public float flushInterval = 5f;
        public float heartbeatInterval = 30f;
        public bool bridgeEnabled;
        public bool logErrors = true;

        TelemetryBuffer _buffer;
        JsonHttpClient _client;
        readonly List<IDisposable> _subs = new List<IDisposable>();
        float _nextFlush;
        float _nextHeartbeat;
        long _sent;
        long _failed;

        public long SentCount { get { return _sent; } }
        public long FailedCount { get { return _failed; } }
        public int BufferedCount { get { return _buffer != null ? _buffer.Count : 0; } }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _buffer = new TelemetryBuffer(512);
        }

        public void StartBridge()
        {
            var env = Environment.GetEnvironmentVariable("VENUE_API_URL");
            if (!string.IsNullOrEmpty(env)) baseUrl = env.TrimEnd('/');
            var token = Environment.GetEnvironmentVariable("VENUE_OPS_TOKEN");
            if (_client == null)
            {
                _client = JsonHttpClient.Instance;
                if (_client == null)
                {
                    var go = new GameObject("JsonHttpClient");
                    _client = go.AddComponent<JsonHttpClient>();
                }
                _client.baseUrl = baseUrl;
            }
            if (!string.IsNullOrEmpty(token)) _client.opsToken = token.Trim();
            bridgeEnabled = true;
            _nextFlush = Time.unscaledTime + flushInterval;
            _nextHeartbeat = Time.unscaledTime + heartbeatInterval;
            if (_subs.Count == 0)
            {
                _subs.Add(VenueBus.Subscribe("show.master.tick", OnBusEvent));
                _subs.Add(VenueBus.Subscribe("show.master.segment", OnBusEvent));
                _subs.Add(VenueBus.Subscribe("show.master.seek", OnBusEvent));
                _subs.Add(VenueBus.Subscribe("control.state", OnBusEvent));
                _subs.Add(VenueBus.Subscribe("sensor.stale", OnBusEvent));
                _subs.Add(VenueBus.Subscribe("sensor.recovered", OnBusEvent));
                _subs.Add(VenueBus.Subscribe("bp01.rig.built", OnBusEvent));
            }
            Debug.Log("[BP-04] OpsBridge 启动 → " + baseUrl);
        }

        public void StopBridge() { bridgeEnabled = false; }

        void OnBusEvent(VenueEvent e)
        {
            if (!bridgeEnabled || e == null || _buffer == null) return;
            _buffer.Add(e.type, e.source, PayloadText(e.payload));
        }

        static string PayloadText(object payload)
        {
            if (payload == null) return "";
            if (payload is string) return (string)payload;
            try { return JsonUtility.ToJson(payload, false); }
            catch { return payload.ToString(); }
        }

        void Update()
        {
            if (!bridgeEnabled) return;
            if (Time.unscaledTime >= _nextFlush)
            {
                _nextFlush = Time.unscaledTime + Mathf.Max(1f, flushInterval);
                Flush();
            }
            if (Time.unscaledTime >= _nextHeartbeat)
            {
                _nextHeartbeat = Time.unscaledTime + Mathf.Max(5f, heartbeatInterval);
                SendHeartbeat();
            }
        }

        public void Flush()
        {
            if (_buffer == null || _buffer.Count == 0 || _client == null) return;
            var events = _buffer.Drain();
            var json = JsonUtility.ToJson(new TelemetryEnvelope { events = events }, false);
            var key = "ops-" + DateTime.UtcNow.Ticks;
            _client.Post<OpsAck>("api/v1/telemetry", json,
                ack => { _sent += events.Length; },
                err =>
                {
                    _failed++;
                    for (var i = 0; i < events.Length; i++)
                        _buffer.Add(events[i].type, events[i].source, events[i].payload);
                    if (logErrors) Debug.LogWarning("[BP-04] telemetry 失败：" + err);
                }, key);
        }

        public void SendHeartbeat()
        {
            if (_client == null) return;
            var director = ShowDirector.Instance;
            var supervisor = SensorSupervisor.Instance;
            var payload = new OpsHeartbeat
            {
                modules = director != null ? director.ModuleCount : 0,
                sensors = supervisor != null ? supervisor.SensorCount : 0,
                staleSensors = supervisor != null ? supervisor.StaleCount : 0,
                masterSeconds = director != null ? director.MasterSeconds : 0f,
                segment = director != null && director.Current != null ? director.Current.id : "",
                paused = director != null && director.Paused,
                atUnixMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond
            };
            _client.Post<OpsAck>("api/v1/ops/heartbeat", payload,
                ack => { if (logErrors && (!ack.ok)) Debug.LogWarning("[BP-04] heartbeat 非 ok"); },
                err => { _failed++; if (logErrors) Debug.LogWarning("[BP-04] heartbeat 失败：" + err); },
                "hb-" + payload.atUnixMs);
        }

        public void SendControl(string command, string segment = null, float volume = 1f)
        {
            if (_client == null) return;
            var state = new ControlState { command = command, segment = segment, volume = volume, paused = command == "pause" };
            _client.Post<OpsAck>("api/v1/control/" + command, state,
                ack => VenueBus.Publish("ops.control.ack", "OpsBridge", ack),
                err => { if (logErrors) Debug.LogWarning("[BP-04] control 失败：" + err); },
                "ctl-" + DateTime.UtcNow.Ticks);
        }

        void OnDestroy()
        {
            for (var i = 0; i < _subs.Count; i++) _subs[i].Dispose();
            _subs.Clear();
            if (Instance == this) Instance = null;
        }
    }
}
