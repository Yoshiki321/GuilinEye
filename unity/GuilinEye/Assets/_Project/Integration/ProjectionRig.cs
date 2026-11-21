using System;
using System.Collections.Generic;
using System.Text;
using GuilinEye.Core;
using GuilinEye.Shared;
using UnityEngine;

namespace GuilinEye.Integration
{
    /// <summary>
    /// BP-01 投影台架：把 11 画布 / 24 通道转成运行时通道描述、预览相机与校准入口。
    /// 真实融合/同步仍由 EHE 负责；本组件只做 Unity 侧逻辑通道与预览，不替代 EHE。
    /// </summary>
    public class ProjectionRig : MonoBehaviour
    {
        [Serializable]
        public class ChannelRuntime
        {
            public string id;
            public string canvasId;
            public string projector;
            public string model;
            public string tier;
            public RectMM rect;
            public int outputWidth;
            public int outputHeight;
            public float baseOrthographicSize;
            public Vector3 baseLocalPosition;
            public Camera camera;
            public RenderTexture target;
            public ChannelCalibration calibration;
        }

        public static ProjectionRig Instance { get; private set; }

        [Tooltip("Editor/预览时创建低分辨率 RT；关闭后只保留通道描述，真实输出交给 EHE。")]
        public bool previewEnabled = true;
        [Tooltip("预览 RT 的最大宽度；4K 通道按比例缩小，避免 24 路 RT 爆显存。")]
        public int previewMaxWidth = 960;

        readonly Dictionary<string, ChannelRuntime> _channels = new Dictionary<string, ChannelRuntime>();
        readonly List<ChannelRuntime> _order = new List<ChannelRuntime>();

        public int ChannelCount { get { return _order.Count; } }
        public int CanvasCount { get; private set; }
        public bool Built { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Build()
        {
            if (Built) return;
            if (!ConfigDatabase.Loaded && !ConfigDatabase.Load())
            {
                Debug.LogError("[BP-01] 参数源未加载，ProjectionRig 不构建");
                return;
            }

            ReleaseAll();
            CanvasCount = ConfigDatabase.Canvas.canvases.Length;
            foreach (var canvas in ConfigDatabase.Canvas.canvases)
            {
                if (canvas.channels == null) continue;
                foreach (var ch in canvas.channels)
                {
                    if (ch == null || string.IsNullOrEmpty(ch.id)) continue;
                    var runtime = new ChannelRuntime
                    {
                        id = ch.id,
                        canvasId = canvas.id,
                        projector = ch.projector,
                        model = ch.model,
                        tier = ch.tier,
                        rect = ch.rect,
                        outputWidth = ch.output != null && ch.output.Length > 0 ? ch.output[0] : (int)canvas.width,
                        outputHeight = ch.output != null && ch.output.Length > 1 ? ch.output[1] : (int)canvas.height
                    };
                    runtime.baseOrthographicSize = Mathf.Max(0.001f, ch.rect.h * CanvasMapper.MmToMeter * 0.5f);
                    runtime.baseLocalPosition = CanvasMapper.LocalCenter(canvas, ch.rect) + new Vector3(0f, 0f, -0.5f);
                    if (previewEnabled) CreatePreview(runtime, canvas);
                    else CreateCameraOnly(runtime, canvas, false);
                    _channels[runtime.id] = runtime;
                    _order.Add(runtime);
                }
            }

            Built = true;
            VenueBus.Publish("bp01.rig.built", "ProjectionRig", ReportJson());
            Debug.Log("[BP-01] 通道 " + _order.Count + " / 画布 " + CanvasCount + "，预览=" + previewEnabled);
        }

        void CreatePreview(ChannelRuntime runtime, CanvasConfig canvas)
        {
            var scale = 1f;
            if (previewMaxWidth > 0 && runtime.outputWidth > previewMaxWidth)
                scale = (float)previewMaxWidth / runtime.outputWidth;
            var w = Mathf.Max(2, Mathf.RoundToInt(runtime.outputWidth * scale));
            var h = Mathf.Max(2, Mathf.RoundToInt(runtime.outputHeight * scale));
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.name = "RT_" + runtime.id + "_" + w + "x" + h;
            rt.Create();
            runtime.target = rt;
            CreateCameraOnly(runtime, canvas, true);
        }

        void CreateCameraOnly(ChannelRuntime runtime, CanvasConfig canvas, bool enabled)
        {
            var root = GameObject.Find("Canvas_" + canvas.id);
            var parent = root != null ? root.transform : transform;
            var go = new GameObject("Channel_" + runtime.id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = runtime.baseLocalPosition;
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = runtime.baseOrthographicSize;
            cam.aspect = runtime.rect.w / Mathf.Max(1f, runtime.rect.h);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 10f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.01f, 0.02f, 1f);
            cam.enabled = enabled;
            if (runtime.target != null) cam.targetTexture = runtime.target;
            runtime.camera = cam;
        }

        public ChannelRuntime Get(string channelId)
        {
            ChannelRuntime c = null;
            return channelId != null && _channels.TryGetValue(channelId, out c) ? c : null;
        }

        public string[] GetChannelIds()
        {
            var ids = new string[_order.Count];
            for (var i = 0; i < _order.Count; i++) ids[i] = _order[i].id;
            return ids;
        }

        public IReadOnlyList<ChannelRuntime> Channels { get { return _order; } }

        public bool ApplyCalibration(ChannelCalibration cal)
        {
            if (cal == null || string.IsNullOrEmpty(cal.channelId)) return false;
            var runtime = Get(cal.channelId);
            if (runtime == null) return false;
            runtime.calibration = cal;
            if (runtime.camera != null)
            {
                runtime.camera.transform.localPosition = runtime.baseLocalPosition +
                    new Vector3(cal.offsetX * CanvasMapper.MmToMeter, cal.offsetY * CanvasMapper.MmToMeter, 0f);
                runtime.camera.orthographicSize = runtime.baseOrthographicSize * (cal.scaleY <= 0f ? 1f : cal.scaleY);
                runtime.camera.transform.localRotation = Quaternion.Euler(0f, 0f, cal.rotationDeg);
                runtime.camera.enabled = previewEnabled && cal.brightness > 0.01f;
            }
            return true;
        }

        public ChannelCalibration CaptureCalibration(string channelId)
        {
            var runtime = Get(channelId);
            if (runtime == null) return null;
            if (runtime.calibration != null) return runtime.calibration;
            return new ChannelCalibration { channelId = channelId, scaleX = 1f, scaleY = 1f, brightness = 1f };
        }

        public void SetPreviewEnabled(bool enabled)
        {
            previewEnabled = enabled;
            for (var i = 0; i < _order.Count; i++)
            {
                var c = _order[i];
                if (c.camera != null) c.camera.enabled = enabled;
                if (!enabled && c.target != null) { c.target.Release(); c.target = null; }
            }
        }

        public string ReportJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"channels\":").Append(_order.Count)
              .Append(",\"canvases\":").Append(CanvasCount)
              .Append(",\"preview\":").Append(previewEnabled ? "true" : "false")
              .Append(",\"pixels\":").Append(TotalPixels())
              .Append(",\"items\":[");
            for (var i = 0; i < _order.Count; i++)
            {
                var c = _order[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":\"").Append(c.id).Append("\",\"canvas\":\"").Append(c.canvasId)
                  .Append("\",\"w\":").Append(c.outputWidth).Append(",\"h\":").Append(c.outputHeight).Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        long TotalPixels()
        {
            long total = 0;
            for (var i = 0; i < _order.Count; i++)
                total += (long)_order[i].outputWidth * _order[i].outputHeight;
            return total;
        }

        void ReleaseAll()
        {
            for (var i = 0; i < _order.Count; i++)
            {
                var c = _order[i];
                if (c.target != null) { c.target.Release(); c.target = null; }
                if (c.camera != null) Destroy(c.camera.gameObject);
            }
            _order.Clear();
            _channels.Clear();
        }

        void OnDestroy()
        {
            ReleaseAll();
            if (Instance == this) Instance = null;
        }
    }
}
