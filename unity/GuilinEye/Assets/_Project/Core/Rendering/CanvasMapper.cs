using UnityEngine;

namespace GuilinEye.Core
{
    /// <summary>把 mm 参数映射到场景世界（1 mm = 0.001 m），并生成占位画面。</summary>
    public static class CanvasMapper
    {
        public const float MmToMeter = 0.001f;

        public static GameObject CreateCanvasRoot(CanvasConfig canvas, Transform parent, Vector3 origin)
        {
            var root = new GameObject("Canvas_" + canvas.id);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = origin;
            root.transform.localRotation = Quaternion.identity;
            return root;
        }

        public static Vector3 LocalCenter(CanvasConfig canvas, RectMM r)
        {
            return new Vector3((r.x + r.w * 0.5f) * MmToMeter, (r.y + r.h * 0.5f) * MmToMeter, 0f);
        }

        public static Vector3 LocalSize(RectMM r)
        {
            return new Vector3(Mathf.Max(0.001f, r.w * MmToMeter), Mathf.Max(0.001f, r.h * MmToMeter), 1f);
        }

        public static GameObject CreateQuad(Transform parent, string name, CanvasConfig canvas, RectMM rect, Material material, int order = 0)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = LocalCenter(canvas, rect) + new Vector3(0f, 0f, order * 0.0005f);
            go.transform.localScale = LocalSize(rect);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            return go;
        }

        public static Camera CreateAreaCamera(CanvasConfig canvas, Transform parent, Vector3 origin)
        {
            var go = new GameObject("Camera_" + canvas.id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = origin + new Vector3(canvas.width * 0.5f * MmToMeter, canvas.height * 0.5f * MmToMeter, -0.5f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = canvas.height * 0.5f * MmToMeter;
            cam.aspect = canvas.width / canvas.height;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 10f;
            return cam;
        }

        public static Material CreatePlaceholderMaterial(Color color, string label)
        {
            var shader = ShaderLibrary.UnlitTextured();
            var m = new Material(shader);
            m.color = color;
            m.name = "PH_" + label;
            return m;
        }
    }
}
