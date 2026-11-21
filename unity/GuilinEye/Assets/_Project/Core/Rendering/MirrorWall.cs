using UnityEngine;

namespace GuilinEye.Core
{
    /// <summary>东西短墙镜面：只做左右翻转复制，不竖立；亮度按配置衰减。</summary>
    public class MirrorWall : MonoBehaviour
    {
        public float attenuation = 0.35f;

        public static GameObject Build(Transform sourceRoot, CanvasConfig mirrorCanvas, float attenuation)
        {
            var root = new GameObject("Mirror_" + mirrorCanvas.id);
            root.transform.SetParent(sourceRoot.parent, false);

            var copy = Object.Instantiate(sourceRoot.gameObject);
            copy.name = "MirrorCopy_" + mirrorCanvas.id;
            copy.transform.SetParent(root.transform, false);
            copy.transform.localScale = new Vector3(-1f, 1f, 1f);
            copy.transform.localPosition = sourceRoot.localPosition;

            var mw = root.AddComponent<MirrorWall>();
            mw.attenuation = Mathf.Clamp01(attenuation >= 1f ? 0.35f : attenuation);

            var renderers = root.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                var m = r.material;
                if (m == null) continue;
                var c = m.color;
                m.color = new Color(c.r * (1f - mw.attenuation), c.g * (1f - mw.attenuation), c.b * (1f - mw.attenuation), c.a);
            }
            return root;
        }

        /// <summary>D-03 在镜面衰减前再叠全场 60% 亮度。</summary>
        public void ApplyGlobalBrightness(float brightness)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                var m = r.material;
                if (m == null) continue;
                var c = m.color;
                m.color = new Color(c.r * brightness, c.g * brightness, c.b * brightness, c.a);
            }
        }
    }
}
