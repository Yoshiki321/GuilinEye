using System;
using System.Collections.Generic;
using GuilinEye.Core;
using GuilinEye.Hardware;
using GuilinEye.UI;
using UnityEngine;

namespace GuilinEye.Installations
{
    /// <summary>组合场景启动器：建画布、相机、18 个模块。模块按 ID 反射装配，缺失时退回通用基类。</summary>
    public class VenueBootstrap : MonoBehaviour
    {
        public bool buildAll = true;
        public string onlyModule;
        public string onlyArea;
        public bool buildMirrors = true;
        public bool createCameras = true;
        public bool useOrigins = true;

        public readonly List<InstallationBase> Modules = new List<InstallationBase>();

        readonly Dictionary<string, Vector3> _origins = new Dictionary<string, Vector3>
        {
            { "A-WALL", new Vector3(0f, 0f, 0f) },
            { "B-NORTH", new Vector3(9f, 2f, 0f) },
            { "B-EAST", new Vector3(18f, 2f, 0f) },
            { "B-SOUTH", new Vector3(9f, 6.5f, 0f) },
            { "C-SOUTH", new Vector3(21f, 0f, 0f) },
            { "C-EAST", new Vector3(27f, 0f, 0f) },
            { "D-FLOOR", new Vector3(36f, 0f, 0f) },
            { "D-NORTH", new Vector3(36f, 8.5f, 0f) },
            { "D-SOUTH", new Vector3(36f, -3.5f, 0f) },
            { "D-MIRROR-W", new Vector3(27f, 0f, 0f) },
            { "D-MIRROR-E", new Vector3(45f, 0f, 0f) }
        };

        public void PauseAll()
        {
            foreach (var m in Modules) if (m != null) m.Pause();
        }

        public void ResumeAll()
        {
            foreach (var m in Modules) if (m != null) m.Resume();
        }

        public void SeekAll(string segmentId)
        {
            foreach (var m in Modules) if (m != null) m.SeekSegment(segmentId);
        }

        void Awake()
        {
            ApplyCommandLineOverrides();
            if (!ConfigDatabase.Loaded && !ConfigDatabase.Load())
            {
                Debug.LogError("[Boot] 参数源加载失败，进入安全模式");
                return;
            }
            // 模块 Configure 时会立即订阅 SensorHub，必须先于 Build 建立 Hub。
            if (SensorHub.Instance == null)
                new GameObject("SensorHub").AddComponent<SensorHub>();
            if (AudioBus.Instance == null)
                new GameObject("AudioBus").AddComponent<AudioBus>();
            Build();
        }

        /// <summary>
        /// 现场联调用：-onlyArea B,D；-onlyModule B01；-all 建全场。
        /// 不传时保持场景里序列化的分区，不改变正常展项启动。
        /// </summary>
        void ApplyCommandLineOverrides()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                var key = args[i].ToLowerInvariant();
                if (key == "-onlyarea")
                {
                    if (i + 1 >= args.Length) continue;
                    onlyArea = args[++i];
                    if (onlyArea == "all" || onlyArea == "*") onlyArea = "";
                }
                else if (key == "-onlymodule")
                {
                    if (i + 1 >= args.Length) continue;
                    onlyModule = args[++i];
                }
                else if (key == "-all")
                {
                    buildAll = true;
                    onlyArea = "";
                    onlyModule = "";
                }
            }
        }

        bool AreaMatches(string area)
        {
            if (string.IsNullOrEmpty(onlyArea)) return true;
            var parts = onlyArea.Split(',');
            for (var i = 0; i < parts.Length; i++)
                if (parts[i].Trim() == area) return true;
            return false;
        }

        public void Build()
        {
            var canvases = new List<CanvasConfig>();
            foreach (var c in ConfigDatabase.Canvas.canvases)
            {
                Vector3 origin;
                if (!useOrigins) origin = Vector3.zero;
                else if (!_origins.TryGetValue(c.id, out origin)) origin = Vector3.zero;
                var root = CanvasMapper.CreateCanvasRoot(c, transform, origin);
                if (createCameras && !c.isVirtual && c.channels != null && c.channels.Length > 0)
                    CanvasMapper.CreateAreaCamera(c, root.transform, Vector3.zero);
                canvases.Add(c);
            }

            foreach (var m in ConfigDatabase.Installations.modules)
            {
                if (!AreaMatches(m.area)) continue;
                if (!buildAll && onlyModule != m.id) continue;
                BuildModule(m);
            }
        }

        void BuildModule(ModuleConfig cfg)
        {
            var parent = GameObject.Find("Canvas_" + cfg.surfaces[0].canvas);
            var go = new GameObject(cfg.id);
            if (parent != null) go.transform.SetParent(parent.transform, false);

            InstallationBase comp;
            var type = TypeResolver.Find("GuilinEye.Modules." + cfg.id + "." + cfg.id + "Installation");
            if (type != null)
                comp = (InstallationBase)go.AddComponent(type);
            else
            {
                Debug.LogWarning("[Boot] " + cfg.id + " 专用控制器未找到，使用通用基类");
                comp = go.AddComponent<InstallationBase>();
            }
            comp.startAtOwnSegment = !buildAll && onlyModule == cfg.id;
            comp.previewSingleSurface = !buildAll;
            comp.Configure(cfg);
            Modules.Add(comp);
        }

        void Start()
        {
            if (SensorHub.Instance != null) SensorHub.Instance.StartAll();
            if (ControlApi.Instance == null)
                new GameObject("ControlApi").AddComponent<ControlApi>();
            if (ControlApi.Instance != null) ControlApi.Instance.StartPolling();
            var existingIntegration = GameObject.Find("Integration");
            var integrationType = TypeResolver.Find("GuilinEye.Integration.IntegrationBootstrap");
            if (existingIntegration != null)
            {
                // 只调用 IntegrationBootstrap.Build，不能用 SendMessage：SendMessage 会同时
                // 调到同物体上的 ShowDirector/SensorSupervisor/CalibrationTool.Build，导致 BP-02/03/05 重复装配。
                if (integrationType != null)
                {
                    var boot = existingIntegration.GetComponent(integrationType);
                    var build = integrationType.GetMethod("Build");
                    if (boot != null && build != null) build.Invoke(boot, null);
                    else Debug.LogWarning("[Boot] Integration 物体缺少 IntegrationBootstrap，跳过 BP 装配");
                }
                else
                {
                    Debug.LogWarning("[Boot] 未找到 GuilinEye.Integration.IntegrationBootstrap，跳过 BP 装配");
                }
            }
            else
            {
                var integration = new GameObject("Integration");
                // Runtime 程序集不能反向引用 Integration（否则循环）；这里按类型名反射装配。
                if (integrationType != null)
                {
                    var boot = integration.AddComponent(integrationType);
                    var opsField = integrationType.GetField("buildOpsBridge");
                    if (opsField != null) opsField.SetValue(boot, false);
                    var build = integrationType.GetMethod("Build");
                    if (build != null) build.Invoke(boot, null);
                }
                else
                {
                    Debug.LogWarning("[Boot] 未找到 GuilinEye.Integration.IntegrationBootstrap，跳过 BP 装配");
                }
            }
            if (Application.isEditor || Debug.isDebugBuild)
            {
                if (FindAnyObjectByType<MockInput>() == null)
                    new GameObject("MockInput").AddComponent<MockInput>();
            }
            Debug.Log("[Boot] 场景就绪：18 模块，状态=模拟通过");
            if (VenueCapture.Requested() && FindAnyObjectByType<VenueCapture>() == null)
            {
                var capture = new GameObject("VenueCapture");
                capture.AddComponent<VenueCapture>();
            }
            if (buildAll)
            {
                if (FindAnyObjectByType<VenueHud>() == null)
                    new GameObject("VenueHUD").AddComponent<VenueHud>();
            }
            else if (!string.IsNullOrEmpty(onlyModule) && FindAnyObjectByType<ModuleHud>() == null)
            {
                var target = Modules.Count > 0 ? Modules[0] : null;
                if (target != null) target.gameObject.AddComponent<ModuleHud>();
            }
        }
    }
}

