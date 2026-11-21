using UnityEngine;

namespace GuilinEye.Shared
{
    /// <summary>SP-09 亮度规则：常规 100%，D03 60%，镜面再降 30—40%。</summary>
    public static class BrightnessGuard
    {
        public const float DefaultMirrorAttenuation = 0.35f;

        public static float AreaMultiplier(string area)
        {
            return area == "D03" ? 0.6f : 1f;
        }

        public static float MirrorMultiplier(float attenuation)
        {
            return Mathf.Clamp01(1f - attenuation);
        }

        public static float Combine(string area, bool mirror, float attenuation = DefaultMirrorAttenuation, float extra = 1f)
        {
            var value = AreaMultiplier(area) * Mathf.Clamp01(extra);
            if (mirror) value *= MirrorMultiplier(attenuation);
            return Mathf.Clamp01(value);
        }

        public static Color Apply(Color color, string area, bool mirror, float attenuation = DefaultMirrorAttenuation, float extra = 1f)
        {
            var factor = Combine(area, mirror, attenuation, extra);
            return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
        }

        public static void Apply(Renderer renderer, string area, bool mirror, float attenuation = DefaultMirrorAttenuation, float extra = 1f)
        {
            if (renderer == null) return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            var baseColor = renderer.sharedMaterial != null ? renderer.sharedMaterial.color : Color.white;
            var tinted = Apply(baseColor, area, mirror, attenuation, extra);
            block.SetColor("_Color", tinted);
            block.SetColor("_BaseColor", tinted);
            renderer.SetPropertyBlock(block);
        }

        public static void Apply(GameObject root, string area, bool mirror, float attenuation = DefaultMirrorAttenuation, float extra = 1f)
        {
            if (root == null) return;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++) Apply(renderers[i], area, mirror, attenuation, extra);
        }
    }

    /// <summary>SP-09 镜面左右翻转复制；只翻 X、不竖立，亮度按衰减。</summary>
    public class MirrorCopy : MonoBehaviour
    {
        public float attenuation = BrightnessGuard.DefaultMirrorAttenuation;
        public string area;

        public static GameObject Build(Transform source, float attenuation = BrightnessGuard.DefaultMirrorAttenuation, string area = null, string name = null)
        {
            if (source == null) return null;
            var copy = Object.Instantiate(source.gameObject);
            copy.name = string.IsNullOrEmpty(name) ? "Mirror_" + source.name : name;
            copy.transform.SetParent(source.parent, false);
            var scale = source.localScale;
            copy.transform.localScale = new Vector3(-Mathf.Abs(scale.x), scale.y, scale.z);
            copy.transform.localPosition = source.localPosition;
            copy.transform.localRotation = source.localRotation;
            var mirror = copy.AddComponent<MirrorCopy>();
            mirror.attenuation = Mathf.Clamp01(attenuation);
            mirror.area = area;
            mirror.ApplyBrightness();
            return copy;
        }

        public void ApplyBrightness()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
                BrightnessGuard.Apply(renderers[i], area, true, attenuation);
        }
    }
}
