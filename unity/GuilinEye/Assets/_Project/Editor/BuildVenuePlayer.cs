using System;
using System.Collections.Generic;
using System.IO;
using GuilinEye.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GuilinEye.Editor
{
    /// <summary>一键构建 Windows x64 播放器：4 个分区场景 + 18 个模块场景。</summary>
    public static class BuildVenuePlayer
    {
        [MenuItem("桂林之眼/6. 构建 Windows 播放器", false, 6)]
        public static void BuildFromMenu()
        {
            var ok = BuildInternal();
            EditorUtility.DisplayDialog(ok ? "构建完成" : "构建失败", ok ? "播放器已输出到 build/GuilinEye_Windows" : "查看 Console 构建日志", "关闭");
        }

        /// <summary>批处理入口：-executeMethod GuilinEye.Editor.BuildVenuePlayer.BuildBatch</summary>
        public static void BuildBatch()
        {
            var ok = BuildInternal();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool BuildInternal()
        {
            SceneBootstrapper.BuildAll();
            var scenes = CollectScenes();
            if (scenes.Count < 22)
            {
                Debug.LogError("[Build] 场景不足 22，实际 " + scenes.Count);
                return false;
            }

            PlayerSettings.productName = "Guilin Eye";
            PlayerSettings.companyName = "Guilin Eye Studio";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../build/GuilinEye_Windows"));
            Directory.CreateDirectory(outDir);
            var exe = Path.Combine(outDir, "GuilinEye.exe");
            var dev = Environment.GetEnvironmentVariable("GUILINEYE_DEV_BUILD") == "1";
            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = dev ? BuildOptions.Development : BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log("[Build] " + summary.result + "  " + exe + "  bytes=" + summary.totalSize
                + "  errors=" + summary.totalErrors + "  development=" + dev);
            if (summary.result != BuildResult.Succeeded)
            {
                foreach (var step in report.steps)
                    foreach (var message in step.messages)
                        if (message.type == LogType.Error) Debug.LogError("[Build] " + message.content);
                return false;
            }
            File.WriteAllText(Path.Combine(outDir, "BUILD_OK.txt"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" + exe + "\nscenes=" + scenes.Count +
                "\ndevelopment=" + (dev ? "True" : "False") + "\n");
            return true;
        }

        static List<string> CollectScenes()
        {
            var list = new List<string>();
            foreach (var area in new[] { "Entrance", "Education", "Family", "Hall" })
            {
                var path = "Assets/_Project/Installations/" + area + ".unity";
                if (File.Exists(Path.Combine(Application.dataPath, "../", path))) list.Add(path);
            }
            if (ConfigDatabase.Installations != null)
            {
                foreach (var m in ConfigDatabase.Installations.modules)
                {
                    var path = "Assets/_Project/Modules/" + m.id + "/Scenes/" + m.id + ".unity";
                    if (File.Exists(Path.Combine(Application.dataPath, "../", path))) list.Add(path);
                }
            }
            return list;
        }
    }
}
