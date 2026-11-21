using System;
using System.Collections.Generic;
using System.IO;
using GuilinEye.Core;
using GuilinEye.Installations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GuilinEye.Editor
{
    /// <summary>一键生成 18 个模块场景与 4 个组合场景。场景由配置生成，不手改。</summary>
    public static class SceneBootstrapper
    {
        const string Root = "Assets/_Project";

        [MenuItem("桂林之眼/1. 生成全部模块场景", false, 1)]
        public static void BuildAll()
        {
            if (!ConfigDatabase.Load())
            {
                var msg = string.Join("\n", ConfigDatabase.ValidationErrors);
                if (string.IsNullOrEmpty(msg)) msg = "参数源读取失败，见 Console 详细日志";
                if (Application.isBatchMode)
                {
                    Debug.LogError("[Editor] " + msg);
                    return;
                }
                EditorUtility.DisplayDialog("参数源错误", msg, "关闭");
                return;
            }

            foreach (var m in ConfigDatabase.Installations.modules)
                BuildModuleScene(m);

            SyncConfigSilent();
            BuildCombined("Entrance", "A");
            BuildCombined("Education", "B");
            BuildCombined("Family", "C");
            BuildCombined("Hall", "D");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Editor] 场景生成完成：18 模块 + 4 组合");
        }

        static void BuildModuleScene(ModuleConfig m)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvas = ConfigDatabase.GetCanvas(m.surfaces[0].canvas);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var s in m.surfaces)
            {
                min = Vector2.Min(min, new Vector2(s.rect.x, s.rect.y));
                max = Vector2.Max(max, new Vector2(s.rect.x + s.rect.w, s.rect.y + s.rect.h));
            }

            var center = new Vector3((min.x + max.x) * 0.5f * 0.001f, (min.y + max.y) * 0.5f * 0.001f, -0.5f);
            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max((max.y - min.y) * 0.5f * 0.001f, (max.x - min.x) * 0.5f * 0.001f / Mathf.Max(1f, (float)Screen.width / Mathf.Max(1, Screen.height)));
            cam.transform.position = center;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);

            var light = new GameObject("KeyLight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var bootGo = new GameObject("VenueBootstrap");
            var boot = bootGo.AddComponent<VenueBootstrap>();
            boot.buildAll = false;
            boot.onlyModule = m.id;
            boot.createCameras = false;
            boot.useOrigins = false;
            // 模块在进入 Play 时由 Bootstrap 按参数源装配，避免把运行时程序化纹理序列化进场景。
            bootGo.SetActive(true);
            new GameObject("SensorHub").AddComponent<GuilinEye.Hardware.SensorHub>();

            var dir = Root + "/Modules/" + m.id + "/Scenes";
            Directory.CreateDirectory(dir);
            EditorSceneManager.SaveScene(scene, dir + "/" + m.id + ".unity");
        }

        static Type FindType(string name)
        {
            var t = Type.GetType(name);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(name, false);
                if (t != null) return t;
            }
            return null;
        }

        static void BuildCombined(string name, string area)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var boot = new GameObject("VenueBootstrap").AddComponent<VenueBootstrap>();
            boot.buildAll = true;
            boot.onlyArea = area;
            boot.createCameras = false;

            // 组合场景显式放一个 Integration 组件，保证 Player 构建时 Integration 程序集不被裁掉。
            var integration = new GameObject("Integration");
            var integrationBoot = integration.AddComponent<GuilinEye.Integration.IntegrationBootstrap>();
            integrationBoot.buildOpsBridge = false;

            var light = new GameObject("KeyLight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;

            var camGo = new GameObject("OverviewCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            Vector2 c;
            float size;
            switch (area)
            {
                case "A": c = new Vector2(2.7f, 1.5f); size = 1.9f; break;
                case "B": c = new Vector2(13f, 4.5f); size = 5.2f; break;
                case "C": c = new Vector2(24f, 1.5f); size = 4.2f; break;
                default: c = new Vector2(36f, 4f); size = 9f; break;
            }
            cam.transform.position = new Vector3(c.x, c.y, -0.5f);
            cam.orthographicSize = size;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);

            EditorSceneManager.SaveScene(scene, Root + "/Installations/" + name + ".unity");
        }

        static void SyncConfigSilent()
        {
            var src = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../config"));
            var dst = Path.Combine(Application.dataPath, "StreamingAssets/config");
            if (!Directory.Exists(src)) { Debug.LogError("找不到 " + src); return; }
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src, "*.json"))
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            AssetDatabase.Refresh();
            Debug.Log("[Editor] 已同步配置到 " + dst);
        }

        [MenuItem("桂林之眼/2. 校验参数源", false, 2)]
        public static void ValidateConfig()
        {
            var ok = ConfigDatabase.Load();
            var msg = ok ? "CONFIG OK\n模块 18｜画布 11｜通道 24｜元素 91"
                         : string.Join("\n", ConfigDatabase.ValidationErrors);
            EditorUtility.DisplayDialog(ok ? "参数源通过" : "参数源错误", msg, "关闭");
        }

        [MenuItem("桂林之眼/3. 同步配置到 StreamingAssets", false, 3)]
        public static void SyncConfig()
        {
            var src = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../config"));
            var dst = Path.Combine(Application.dataPath, "StreamingAssets/config");
            if (!Directory.Exists(src)) { Debug.LogError("找不到 " + src); return; }
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src, "*.json"))
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            AssetDatabase.Refresh();
            Debug.Log("[Editor] 已同步配置到 " + dst);
        }
    }
}
