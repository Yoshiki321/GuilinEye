using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace GuilinEye.Hardware
{
    /// <summary>传感器统一入口：厂商适配器只向 Hub 发事件，模块只订阅 Hub。</summary>
    public class SensorHub : MonoBehaviour
    {
        public static SensorHub Instance { get; private set; }

        readonly Dictionary<string, ISensorProvider> _providers = new Dictionary<string, ISensorProvider>();
        readonly List<Action<SensorEvent>> _subscribers = new List<Action<SensorEvent>>();
        static readonly ConcurrentQueue<SensorEvent> Pending = new ConcurrentQueue<SensorEvent>();
        bool _queueLog;
        public bool MockMode = Application.isEditor || Debug.isDebugBuild;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildProviders();
        }

        void Update()
        {
            foreach (var p in _providers.Values)
            {
                var femto = p as FemtoBoltProvider;
                if (femto != null) femto.Tick();
            }

            SensorEvent e;
            while (Pending.TryDequeue(out e))
            {
                if (!_queueLog)
                {
                    _queueLog = true;
                    Debug.Log("[SensorHub] 主线程收到首个硬件事件 " + e.sensorId + "/" + e.type + "@" + e.zoneId);
                }
                Emit(e);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            StopAll();
            SensorEvent ignored;
            while (Pending.TryDequeue(out ignored)) { }
        }

        void BuildProviders()
        {
            if (MockMode) Register(new MockSensorProvider());
            Register(new FemtoBoltProvider("S1"));
            Register(new FemtoBoltProvider("S2"));
            Register(new FemtoBoltProvider("S3"));
            Register(new RadarProvider("R1"));
            Register(new RadarProvider("R2"));
            Register(new InfraredProvider());
        }

        public void Register(ISensorProvider provider)
        {
            if (provider == null) return;
            _providers[provider.SensorId] = provider;
        }

        public void StartAll()
        {
            foreach (var p in _providers.Values)
                p.StartProvider(Enqueue);
        }

        public void StopAll()
        {
            foreach (var p in _providers.Values) p.StopProvider();
        }

        /// <summary>供网络线程安全入队；真正的 Emit 统一在 Update() 主线程执行。</summary>
        public static void Enqueue(SensorEvent e)
        {
            if (e == null) return;
            Pending.Enqueue(e);
        }

        public ISensorProvider Get(string id)
        {
            ISensorProvider p = null;
            return _providers.TryGetValue(id, out p) ? p : null;
        }

        public IDisposable Subscribe(Action<SensorEvent> handler)
        {
            _subscribers.Add(handler);
            return new Token(_subscribers, handler);
        }

        public void Emit(SensorEvent e)
        {
            if (e == null) return;
            if (string.IsNullOrEmpty(e.trackingId)) e.trackingId = e.sensorId + "-" + e.zoneId;
            if (e.timestampMs == 0) e.timestampMs = (long)(Time.realtimeSinceStartup * 1000);
            for (var i = 0; i < _subscribers.Count; i++)
            {
                try { _subscribers[i](e); }
                catch (Exception ex) { Debug.LogError("[SensorHub] " + ex.Message); }
            }
        }

        class Token : IDisposable
        {
            readonly List<Action<SensorEvent>> _list;
            readonly Action<SensorEvent> _handler;
            public Token(List<Action<SensorEvent>> l, Action<SensorEvent> h) { _list = l; _handler = h; }
            public void Dispose() { _list.Remove(_handler); }
        }
    }
}
