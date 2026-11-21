using System;
using System.Collections.Generic;
using System.IO;
using GuilinEye.Core;
using GuilinEye.Installations;
using GuilinEye.Integration;
using UnityEditor;
using UnityEngine;

namespace GuilinEye.Editor
{
    /// <summary>
    /// Unity 内冒烟检查：不进入 Play，先验证参数源、18 个控制器、共享包、BP 与场景清单。
    /// 菜单：桂林之眼/4. 冒烟检查。
    /// </summary>
    public static class VenueSmokeTest
    {
        [MenuItem("桂林之眼/4. 冒烟检查（配置/控制器/场景）", false, 4)]
        public static void Run()
        {
            var result = Evaluate();
            Show(result.errors, result.message);
        }

        /// <summary>批处理入口：Unity.exe -batchmode -quit -executeMethod GuilinEye.Editor.VenueSmokeTest.RunBatch</summary>
        public static void RunBatch()
        {
            var result = Evaluate();
            foreach (var e in result.errors) Debug.LogError("[Smoke] " + e);
            Debug.Log("[Smoke] " + result.message);
            EditorApplication.Exit(result.errors.Count == 0 ? 0 : 1);
        }

        class SmokeResult
        {
            public List<string> errors = new List<string>();
            public string message;
        }

        static SmokeResult Evaluate()
        {
            var result = new SmokeResult();
            var errors = result.errors;
            var ok = ConfigDatabase.Load();
            if (!ok)
            {
                foreach (var e in ConfigDatabase.ValidationErrors) errors.Add("CONFIG " + e);
                result.message = "参数源未通过，冒烟中止";
                return result;
            }

            Check(errors, ConfigDatabase.Canvas != null && ConfigDatabase.Canvas.canvases.Length == 11, "画布应为 11");
            Check(errors, ConfigDatabase.Installations != null && ConfigDatabase.Installations.modules.Length == 18, "模块应为 18");
            Check(errors, CountChannels() == 24, "通道应为 24，实际 " + CountChannels());
            Check(errors, ConfigDatabase.Art != null && ConfigDatabase.Art.total == 91, "美术元素应为 91");
            Check(errors, ConfigDatabase.Sensors != null && ConfigDatabase.Sensors.zones.Length == 25, "触点应为 25");
            Check(errors, ConfigDatabase.Schedule != null && ConfigDatabase.Schedule.shows.Length == 15, "时间轴应为 15");
            Check(errors, ConfigDatabase.GetShow("D-MASTER") != null, "缺少 D-MASTER");
            Check(errors, TotalSeconds(ConfigDatabase.GetShow("D-MASTER")) > 719.9f, "D-MASTER 应 720s");
            Check(errors, typeof(ProjectionRig) != null, "BP-01 ProjectionRig 缺失");
            Check(errors, typeof(ShowDirector) != null, "BP-02 ShowDirector 缺失");
            Check(errors, typeof(SensorSupervisor) != null, "BP-03 SensorSupervisor 缺失");
            Check(errors, typeof(OpsBridge) != null, "BP-04 OpsBridge 缺失");
            Check(errors, typeof(CalibrationTool) != null, "BP-05 CalibrationTool 缺失");

            foreach (var module in ConfigDatabase.Installations.modules)
            {
                var typeName = "GuilinEye.Modules." + module.id + "." + module.id + "Installation";
                var type = FindType(typeName);
                Check(errors, type != null, module.id + " 控制器缺失 " + typeName);
                Check(errors, module.surfaces != null && module.surfaces.Length > 0, module.id + " 无画面");
                if (module.surfaces != null && module.surfaces.Length > 0)
                    Check(errors, ConfigDatabase.GetCanvas(module.surfaces[0].canvas) != null, module.id + " 画布不存在");
            }

            var sceneRoot = Path.Combine(Application.dataPath, "_Project/Modules");
            var scenes = Directory.Exists(sceneRoot)
                ? Directory.GetFiles(sceneRoot, "*.unity", SearchOption.AllDirectories).Length : 0;
            Check(errors, scenes >= 18, "模块场景应 ≥18，实际 " + scenes);

            var shared = Path.Combine(Application.dataPath, "../Packages/com.guilineye.shared/package.json");
            Check(errors, File.Exists(shared), "共享包 package.json 缺失");

            result.message = errors.Count == 0
                ? "SMOKE OK\n18 控制器｜11 画布｜24 通道｜25 触点｜15 时间轴｜91 元素｜BP-01—05｜场景 " + scenes
                : "SMOKE FAILED\n" + string.Join("\n", errors);
            return result;
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

        static int CountChannels()
        {
            var n = 0;
            if (ConfigDatabase.Canvas == null) return 0;
            foreach (var c in ConfigDatabase.Canvas.canvases)
                if (c.channels != null) n += c.channels.Length;
            return n;
        }

        static float TotalSeconds(ShowConfig show)
        {
            if (show == null || show.segments == null) return 0f;
            var total = 0f;
            foreach (var s in show.segments) total += s.dur;
            return total;
        }

        static void Check(List<string> errors, bool condition, string message)
        {
            if (!condition) errors.Add(message);
        }

        static void Show(List<string> errors, string message)
        {
            foreach (var e in errors) Debug.LogError("[Smoke] " + e);
            Debug.Log("[Smoke] " + message);
            EditorUtility.DisplayDialog(errors.Count == 0 ? "冒烟通过" : "冒烟失败", message, "关闭");
        }
    }
}
