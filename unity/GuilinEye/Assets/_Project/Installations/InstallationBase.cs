using System;
using System.Collections.Generic;
using GuilinEye.Hardware;
using UnityEngine;

namespace GuilinEye.Core
{
    /// <summary>
    /// 18 个模块的公共基类。只负责：参数、画面占位、时钟、传感器过滤、控幕、三态标记。
    /// 具体玩法在 Modules/&lt;ID&gt;/Scripts 内重写 Hook。
    /// </summary>
    public class InstallationBase : MonoBehaviour, IControllable
    {
        public string Id { get; protected set; }
        public ModuleConfig Cfg { get; protected set; }
        public ShowClock Clock { get; protected set; }
        public StatusMarker Marker { get; protected set; }
        public ArtSlotRegistry Art { get; protected set; }
        public float Volume { get; protected set; } = 1f;
        public bool startAtOwnSegment;
        public bool previewSingleSurface;

        public IEnumerable<string> Zones { get { return _zoneIds; } }

        readonly List<Renderer> _surfaces = new List<Renderer>();
        readonly HashSet<string> _zoneIds = new HashSet<string>();
        IDisposable _sensorSub;
        IDisposable _controlSub;
        bool _active = true;

        public virtual void Configure(ModuleConfig cfg)
        {
            Cfg = cfg;
            Id = cfg.id;
            gameObject.name = cfg.id + "_" + cfg.name;
            transform.localPosition = Vector3.zero;

            var color = AreaColor(cfg.area);
            var mat = CanvasMapper.CreatePlaceholderMaterial(color, cfg.id);

            for (var i = 0; i < cfg.surfaces.Length; i++)
            {
                var s = cfg.surfaces[i];
                var canvas = ConfigDatabase.GetCanvas(s.canvas);
                if (canvas == null) continue;
                var root = GameObject.Find("Canvas_" + s.canvas);
                var parent = root != null ? root.transform : transform;
                if (i == 0) transform.SetParent(parent, false);
                var go = CanvasMapper.CreateQuad(parent, cfg.id + "_" + s.canvas, canvas, s.rect, mat, s.z);
                _surfaces.Add(go.GetComponent<Renderer>());
                AddLabel(go.transform, canvas, s.rect, cfg.id + " " + cfg.name);
            }

            Clock = gameObject.AddComponent<ShowClock>();
            var show = ConfigDatabase.GetShow(cfg.show);
            var start = 0f;
            if (startAtOwnSegment && show != null && show.segments != null)
                foreach (var s in show.segments)
                    if (s.id == cfg.id) { start = s.start; break; }
            Clock.Configure(show, start);
            Clock.SegmentChanged += OnSegmentChangedInternal;

            Art = gameObject.AddComponent<ArtSlotRegistry>();
            Art.Initialize(cfg);

            Marker = gameObject.AddComponent<StatusMarker>();
            Marker.Attach(cfg.id, ConfigDatabase.GetCanvas(cfg.surfaces[0].canvas),
                CanvasMapper.LocalCenter(ConfigDatabase.GetCanvas(cfg.surfaces[0].canvas), cfg.surfaces[0].rect)
                + new Vector3(-cfg.surfaces[0].rect.w * 0.5f * CanvasMapper.MmToMeter + 0.002f,
                              -cfg.surfaces[0].rect.h * 0.5f * CanvasMapper.MmToMeter + 0.002f, -0.01f));

            var interactionIndex = 0;
            foreach (var it in cfg.interactions)
            {
                // H5 / 扫码等没有物理传感区的模块，合成一个本地虚拟触点，便于单机验收与 HUD 注入。
                if (string.IsNullOrEmpty(it.zone) && (it.zones == null || it.zones.Length == 0))
                {
                    it.zone = cfg.id + ".virtual." + interactionIndex;
                    if (string.IsNullOrEmpty(it.action)) it.action = cfg.id.ToLowerInvariant() + ".interact";
                }
                if (!string.IsNullOrEmpty(it.zone)) _zoneIds.Add(it.zone);
                if (it.zones != null)
                    foreach (var z in it.zones) _zoneIds.Add(z);
                interactionIndex++;
            }
            if (SensorHub.Instance != null) _sensorSub = SensorHub.Instance.Subscribe(OnSensor);
            _controlSub = VenueBus.Subscribe("control.state", OnControl);

            OnConfigured();
            Debug.Log("[" + cfg.id + "] 已装配：画布 " + string.Join(",", SurfaceNames()) + "；时间轴 " + cfg.show);
        }

        void Start()
        {
            // 场景内预装配（Editor 生成场景）时 Configure 早于 SensorHub 创建，
            // Play 时必须补订阅，否则单模块场景收不到传感器事件。
            Resubscribe();
        }

        void Resubscribe()
        {
            if (_sensorSub != null) { _sensorSub.Dispose(); _sensorSub = null; }
            if (_controlSub != null) { _controlSub.Dispose(); _controlSub = null; }
            if (SensorHub.Instance != null) _sensorSub = SensorHub.Instance.Subscribe(OnSensor);
            _controlSub = VenueBus.Subscribe("control.state", OnControl);
        }

        string[] SurfaceNames()
        {
            var list = new List<string>();
            foreach (var s in Cfg.surfaces) list.Add(s.canvas + s.rect);
            return list.ToArray();
        }

        void AddLabel(Transform parent, CanvasConfig canvas, RectMM rect, string text)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.001f);
            go.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 40;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 1f, 1f, 0.85f);
        }

        void Update()
        {
            if (Clock == null) return;
            Clock.Tick(Time.deltaTime);
            if (IsMasterShow())
            {
                var active = Clock.Current != null && Clock.Current.id == Id;
                if (active != _active) SetVisible(active);
            }
            OnTick((float)Clock.ShowSeconds, Time.deltaTime);
        }

        bool IsMasterShow() { return Cfg != null && Cfg.show == "D-MASTER"; }

        public virtual void SetVisible(bool visible)
        {
            _active = visible;
            foreach (var r in _surfaces) if (r != null) r.enabled = visible;
        }

        /// <summary>专业模块自建图层后，移除通用占位面，避免不透明占位面盖住所有层。</summary>
        protected void RemovePlaceholderSurfaces()
        {
            foreach (var r in _surfaces)
                if (r != null) Destroy(r.gameObject);
            _surfaces.Clear();
        }

        protected virtual void OnConfigured() { }
        protected virtual void OnTick(float showSeconds, float deltaSeconds) { }
        protected virtual void OnSegmentChanged(SegmentConfig segment, float localSeconds) { }
        protected virtual void HandleAction(string action, SensorEvent e) { }

        void OnSegmentChangedInternal(SegmentConfig seg, float local)
        {
            if (seg != null) VenueBus.Publish("show.segment.changed", Id, seg.id);
            OnSegmentChanged(seg, local);
        }

        public void InjectSensor(string zoneId, string type = "mock")
        {
            if (string.IsNullOrEmpty(zoneId)) return;
            OnSensor(new SensorEvent
            {
                sensorId = "HUD", type = type, zoneId = zoneId, hasPosition = false,
                confidence = 1f, timestampMs = (long)(Time.realtimeSinceStartup * 1000)
            });
        }

        void OnSensor(SensorEvent e)
        {
            if (e == null || !_active || !_zoneIds.Contains(e.zoneId)) return;
            VenueBus.Publish("sensor.event", e.sensorId, e);
            // leave 不作为“再次触发”，但必须让模块有机会即时清理按住/悬停状态，
            // 否则抬手后 0.45s 内再次触摸同一区会被 _triggered 误吞。
            if (e.type == "leave")
            {
                foreach (var it in Cfg.interactions)
                {
                    if (it.zone == e.zoneId || (it.zones != null && Array.IndexOf(it.zones, e.zoneId) >= 0))
                        OnSensorLeave(it, e);
                }
                return;
            }
            foreach (var it in Cfg.interactions)
            {
                if (it.zone == e.zoneId || (it.zones != null && Array.IndexOf(it.zones, e.zoneId) >= 0))
                    HandleSensor(it, e);
            }
        }

        /// <summary>离场清理钩子；默认无操作，保持“leave 不触发动作”的全局契约。</summary>
        protected virtual void OnSensorLeave(InteractionConfig it, SensorEvent e) { }

        protected virtual void HandleSensor(InteractionConfig it, SensorEvent e)
        {
            if (string.IsNullOrEmpty(it.action)) return;
            HandleAction(it.action, e);
        }

        void OnControl(VenueEvent e)
        {
            var s = e.payload as ControlState;
            if (s == null) return;
            if (s.paused) Pause();
            else Resume();
            if (!string.IsNullOrEmpty(s.segment)) SeekSegment(s.segment);
            if (s.volume >= 0f) SetVolume(s.volume);
            if (string.IsNullOrEmpty(s.segment) && !s.paused && s.command != "resume" && s.command != "volume") { }
        }

        public virtual void Pause() { if (Clock != null) Clock.SetPaused(true); }
        public virtual void Resume() { if (Clock != null) Clock.SetPaused(false); }

        public virtual void SeekSegment(string segmentId)
        {
            if (Clock == null || Clock.Show == null || string.IsNullOrEmpty(segmentId)) return;
            foreach (var s in Clock.Show.segments)
            {
                if (s.id == segmentId) { Clock.Seek(s.start); return; }
            }
            Debug.LogWarning("[" + Id + "] 段不存在 " + segmentId);
        }

        public virtual void SetVolume(float volume)
        {
            Volume = Mathf.Clamp01(volume);
            if (AudioBus.Instance != null)
            {
                AudioBus.Instance.SetBusVolume(AudioBusType.Bgm, Volume);
                AudioBus.Instance.SetBusVolume(AudioBusType.Sfx, Volume);
                AudioBus.Instance.SetBusVolume(AudioBusType.Voice, Volume);
            }
        }

        protected void Log(string action, SensorEvent e = null)
        {
            Debug.Log("[" + Id + "] " + action + (e != null ? " @" + e.zoneId : ""));
        }

        protected virtual void OnDestroy()
        {
            if (_sensorSub != null) _sensorSub.Dispose();
            if (_controlSub != null) _controlSub.Dispose();
        }

        public static Color AreaColor(string area)
        {
            switch (area)
            {
                case "A": return new Color(0.18f, 0.50f, 0.53f);
                case "B": return new Color(0.31f, 0.60f, 0.45f);
                case "C": return new Color(0.85f, 0.65f, 0.63f);
                case "D": return new Color(0.69f, 0.54f, 0.18f);
                default: return Color.gray;
            }
        }
    }

    public interface IControllable
    {
        void Pause();
        void Resume();
        void SeekSegment(string segmentId);
        void SetVolume(float volume);
    }
}
