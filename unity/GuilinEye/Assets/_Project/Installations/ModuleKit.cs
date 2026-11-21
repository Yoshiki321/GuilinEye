using System;
using System.Collections;
using System.Collections.Generic;
using GuilinEye.Hardware;
using GuilinEye.Shared;
using UnityEngine;

namespace GuilinEye.Core
{
    /// <summary>
    /// 模块通用壳：状态机、冷却/防抖、层注册、事件发布、段索引。
    /// 子类只写 BuildModule / OnModuleTick / OnAction / OnSegment，不重复通用逻辑。
    /// </summary>
    public abstract class ModuleKit : InstallationBase
    {
        protected ModuleStateMachine Machine;
        protected CooldownGate Cooldown;
        protected DebounceGate Debounce;
        protected SurfaceSet Layers;
        protected float Seconds;
        protected int SegmentIndex = -1;
        protected bool Activated;
        readonly Dictionary<string, float> _cooldownWarnAt = new Dictionary<string, float>();
        readonly Dictionary<Renderer, Color> _baseColors = new Dictionary<Renderer, Color>();

        protected override void OnConfigured()
        {
            Machine = new ModuleStateMachine(Id);
            Machine.Changed += OnMachineChanged;
            Cooldown = new CooldownGate();
            Debounce = new DebounceGate();
            Layers = gameObject.AddComponent<SurfaceSet>();

            Machine.TryTransition(ModuleState.Initializing);
            BuildModule();
            RemovePlaceholderSurfaces();
            Machine.TryTransition(ModuleState.Standby);
            ReportHealth("模拟通过");
        }

        protected abstract void BuildModule();
        protected virtual void OnModuleTick(float showSeconds, float deltaSeconds) { }

        public override void SetVisible(bool visible)
        {
            base.SetVisible(visible);
            if (Layers != null) Layers.SetVisible(visible);
        }
        protected virtual void OnAction(string action, SensorEvent e) { }
        protected virtual void OnSegment(SegmentConfig segment, float localSeconds) { }
        protected virtual void OnStateChanged(ModuleState from, ModuleState to) { }
        protected virtual float CooldownFor(string action) { return 0f; }

        protected override void OnTick(float showSeconds, float deltaSeconds)
        {
            Seconds = showSeconds;
            if (!Activated && Machine != null && Machine.State == ModuleState.Standby)
            {
                Activated = true;
                Machine.TryTransition(ModuleState.Active);
            }
            if (Machine != null && (Machine.State == ModuleState.Active || Machine.State == ModuleState.Interrupted))
                OnModuleTick(showSeconds, deltaSeconds);
        }

        protected override void OnSegmentChanged(SegmentConfig segment, float localSeconds)
        {
            if (segment == null) return;
            SegmentIndex = IndexOf(segment.id);
            OnSegment(segment, localSeconds);
        }

        protected override void HandleAction(string action, SensorEvent e)
        {
            if (string.IsNullOrEmpty(action)) return;
            var key = action + "|" + (e != null ? e.zoneId : "");
            if (!Debounce.TryPass(key, 0.1f, Time.time)) return;
            var cd = CooldownFor(action);
            if (cd <= 0f) cd = CooldownForAction(action);
            if (!Cooldown.TryPass(key, cd, Time.time))
            {
                var now = Time.time;
                float lastWarn;
                if (!_cooldownWarnAt.TryGetValue(key, out lastWarn) || now - lastWarn > 5f)
                {
                    _cooldownWarnAt[key] = now;
                    Log("冷却中 " + action + " " + cd + "s");
                }
                return;
            }
            PlayCue(action);
            OnAction(action, e);
        }

        protected void PlayCue(string action)
        {
            if (AudioBus.Instance == null) return;
            AudioBus.Instance.PlayOneShot(ProceduralAudio.Get(ProceduralAudio.CueFor(action)), AudioBusType.Sfx, 0.5f * Volume);
        }

        public string CurrentStateName { get { return Machine != null ? Machine.State.ToString() : "-"; } }

        protected float CooldownForAction(string action)
        {
            if (Cfg == null || Cfg.interactions == null) return 0f;
            for (var i = 0; i < Cfg.interactions.Length; i++)
            {
                var it = Cfg.interactions[i];
                if (it != null && it.action == action) return it.cooldown;
            }
            return 0f;
        }

        int IndexOf(string segmentId)
        {
            if (Clock == null || Clock.Show == null || Clock.Show.segments == null) return -1;
            for (var i = 0; i < Clock.Show.segments.Length; i++)
                if (Clock.Show.segments[i].id == segmentId) return i;
            return -1;
        }

        protected Renderer AddLayer(string id, Color color, int order, float alpha = 1f)
        {
            if (Cfg == null || Cfg.surfaces == null || Cfg.surfaces.Length == 0) return null;
            var shader = ShaderLibrary.UnlitTextured();
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var pattern = ProceduralArt.PatternFor(Id, id);
            var seed = Mathf.Abs((Id + "_" + id).GetHashCode()) % 9973;
            var tex = ProceduralArt.Create(pattern, seed, color, order <= 1);
            Renderer first = null;
            var surfaceCount = previewSingleSurface ? 1 : Cfg.surfaces.Length;
            for (var i = 0; i < surfaceCount; i++)
            {
                var surface = Cfg.surfaces[i];
                var canvas = ConfigDatabase.GetCanvas(surface.canvas);
                if (canvas == null) continue;
                var canvasRoot = GameObject.Find("Canvas_" + surface.canvas);
                var parent = canvasRoot != null ? canvasRoot.transform : transform;
                var mat = new Material(shader);
                mat.name = "PH_" + Id + "_" + id + "_" + surface.canvas;
                mat.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
                mat.mainTexture = tex;
                var go = CanvasMapper.CreateQuad(parent, Id + "_" + id + "_" + surface.canvas, canvas, surface.rect, mat, surface.z + order);
                var motion = go.AddComponent<LayerMotion>();
                motion.Init(order <= 1 ? 0.004f : 0.009f, order <= 1 ? 0.006f : 0.012f, 0.10f + order * 0.03f);
                motion.phase = seed * 0.37f + i * 1.1f;
                var r = go.GetComponent<Renderer>();
                if (r == null) continue;
                Layers.Register(id, r);
                _baseColors[r] = color;
                r.enabled = alpha > 0.001f;
                if (first == null) first = r;
            }
            return first;
        }

        protected void SetLayer(string id, bool visible, float alpha = 1f)
        {
            if (Layers == null) return;
            var list = Layers.FindAll(id);
            for (var i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s == null || s.renderer == null) continue;
                s.renderer.enabled = visible;
                var c = _baseColors.ContainsKey(s.renderer) ? _baseColors[s.renderer] : Color.white;
                c.a = visible ? Mathf.Clamp01(alpha) : 0f;
                s.renderer.material.color = c;
            }
        }

        protected void Pulse(string id, float duration = 0.6f)
        {
            if (Layers == null) return;
            var list = Layers.FindAll(id);
            for (var i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].renderer != null)
                    StartCoroutine(PulseRoutine(list[i].renderer, duration));
        }

        IEnumerator PulseRoutine(Renderer renderer, float duration)
        {
            if (renderer == null) yield break;
            var baseColor = _baseColors.ContainsKey(renderer) ? _baseColors[renderer] : Color.white;
            var t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                var k = Mathf.Clamp01(t / Mathf.Max(0.01f, duration));
                var a = 1f - Mathf.Abs(k * 2f - 1f);
                var c = baseColor;
                c.a = 0.35f + 0.65f * a;
                renderer.material.color = c;
                yield return null;
            }
            renderer.material.color = baseColor;
        }

        protected void SetState(ModuleState to)
        {
            if (Machine == null) return;
            if (!Machine.TryTransition(to)) Debug.LogWarning("[" + Id + "] 非法状态 " + Machine.State + "→" + to);
        }

        protected void Publish(string action, object payload = null)
        {
            VenueBus.Publish((Id ?? "module").ToLowerInvariant() + ".action", Id, new ModuleAction(action, payload));
        }

        protected void ReportHealth(string status, string note = null)
        {
            HealthRegistry.Report(new ModuleHealth
            {
                moduleId = Id,
                status = status,
                state = Machine != null ? Machine.State.ToString() : "Uninitialized",
                fps = 1f / Mathf.Max(0.0001f, Time.smoothDeltaTime),
                frameMs = Time.smoothDeltaTime * 1000f,
                note = note
            });
        }

        void OnMachineChanged(ModuleState from, ModuleState to)
        {
            OnStateChanged(from, to);
            ReportHealth("模拟通过");
            VenueBus.Publish("module.state", Id, new ModuleStateEvent(Id, from.ToString(), to.ToString()));
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Machine != null) Machine.Changed -= OnMachineChanged;
        }
    }

    public class ModuleAction
    {
        public string moduleId;
        public string action;
        public object payload;
        public long atUnixMs;
        public ModuleAction(string action, object payload)
        {
            this.action = action;
            this.payload = payload;
            atUnixMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        }
    }

    public class ModuleStateEvent
    {
        public string moduleId;
        public string from;
        public string to;
        public ModuleStateEvent(string id, string f, string t) { moduleId = id; from = f; to = t; }
    }
}
