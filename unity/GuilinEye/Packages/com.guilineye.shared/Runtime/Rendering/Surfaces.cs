using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GuilinEye.Shared
{
    /// <summary>SP-12 mm→m 与矩形几何；不依赖 Core 的 RectMM。</summary>
    public static class LayoutMath
    {
        public const float MmToMeters = 0.001f;

        public static Vector3 Center(float x, float y, float w, float h)
        {
            return new Vector3((x + w * 0.5f) * MmToMeters, (y + h * 0.5f) * MmToMeters, 0f);
        }

        public static Vector3 Size(float w, float h)
        {
            return new Vector3(Mathf.Max(0.001f, w * MmToMeters), Mathf.Max(0.001f, h * MmToMeters), 1f);
        }

        public static Vector3 ToMeters(float x, float y, float z = 0f)
        {
            return new Vector3(x * MmToMeters, y * MmToMeters, z * MmToMeters);
        }

        public static float Clamp01(float value) { return Mathf.Clamp01(value); }
    }

    [Serializable]
    public class ProjectionSurface
    {
        public string id;
        public Renderer renderer;
        public CanvasGroup group;
    }

    /// <summary>SP-08 多 surface 编组；显隐与 alpha 一起管，避免各模块直接 SetActive。</summary>
    public class SurfaceSet : MonoBehaviour
    {
        public List<ProjectionSurface> surfaces = new List<ProjectionSurface>();

        public int Count { get { return surfaces.Count; } }

        public void Register(string id, Renderer renderer, CanvasGroup group = null)
        {
            surfaces.Add(new ProjectionSurface { id = id, renderer = renderer, group = group });
        }

        public ProjectionSurface Get(string id)
        {
            for (var i = 0; i < surfaces.Count; i++) if (surfaces[i].id == id) return surfaces[i];
            return null;
        }

        /// <summary>同一逻辑层可跨北/南/地多个画布，全部返回供显隐与脉冲使用。</summary>
        public List<ProjectionSurface> FindAll(string id)
        {
            var list = new List<ProjectionSurface>();
            for (var i = 0; i < surfaces.Count; i++)
                if (surfaces[i] != null && surfaces[i].id == id) list.Add(surfaces[i]);
            return list;
        }

        public void SetVisible(bool visible)
        {
            for (var i = 0; i < surfaces.Count; i++)
            {
                var s = surfaces[i];
                if (s == null) continue;
                if (s.renderer != null) s.renderer.enabled = visible;
                if (s.group != null) s.group.alpha = visible ? 1f : 0f;
            }
        }

        public void SetAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            for (var i = 0; i < surfaces.Count; i++)
            {
                var s = surfaces[i];
                if (s == null) continue;
                if (s.renderer != null) s.renderer.enabled = alpha > 0.001f;
                if (s.group != null) s.group.alpha = alpha;
            }
        }
    }

    /// <summary>SP-08 画面层 alpha 淡化；3 s 交叉溶解/1.2 s 两态切换统一走这里。</summary>
    public class SurfaceFader : MonoBehaviour
    {
        public float Alpha { get; private set; } = 1f;
        public bool HideAtZero = true;
        public bool IncludeInactive = true;

        CanvasGroup[] _groups;
        Renderer[] _renderers;
        Coroutine _fade;

        public event Action<float> AlphaChanged;

        void Awake() { Capture(); }

        public void Capture()
        {
            _groups = GetComponentsInChildren<CanvasGroup>(IncludeInactive);
            _renderers = GetComponentsInChildren<Renderer>(IncludeInactive);
            SetAlpha(Alpha);
        }

        public void SetAlpha(float alpha)
        {
            Alpha = Mathf.Clamp01(alpha);
            if (_groups == null || _renderers == null) Capture();

            for (var i = 0; i < _groups.Length; i++)
                if (_groups[i] != null) _groups[i].alpha = Alpha;

            for (var i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                if (HideAtZero) r.enabled = Alpha > 0.001f;
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                var baseColor = r.sharedMaterial != null ? r.sharedMaterial.color : Color.white;
                var c = baseColor;
                c.a *= Alpha;
                block.SetColor("_Color", c);
                block.SetColor("_BaseColor", c);
                r.SetPropertyBlock(block);
            }

            if (AlphaChanged != null) AlphaChanged(Alpha);
        }

        public void FadeTo(float target, float durationSeconds, Action onDone = null)
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(Mathf.Clamp01(target), Mathf.Max(0f, durationSeconds), onDone));
        }

        IEnumerator FadeRoutine(float target, float duration, Action onDone)
        {
            var from = Alpha;
            if (duration <= 0.001f)
            {
                SetAlpha(target);
                _fade = null;
                if (onDone != null) onDone();
                yield break;
            }
            var t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, target, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            SetAlpha(target);
            _fade = null;
            if (onDone != null) onDone();
        }

        public void SnapVisible(bool visible) { SetAlpha(visible ? 1f : 0f); }
        public void Show(float duration = 0f) { FadeTo(1f, duration); }
        public void Hide(float duration = 0f) { FadeTo(0f, duration); }

        void OnDisable()
        {
            if (_fade != null) { StopCoroutine(_fade); _fade = null; }
        }
    }
}
