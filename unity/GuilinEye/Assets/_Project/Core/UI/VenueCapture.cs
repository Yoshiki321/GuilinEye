using System;
using System.Collections.Generic;
using System.IO;
using GuilinEye.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuilinEye.UI
{
    /// <summary>构建验收用：启动参数 -capture 时依次加载 22 个场景并截图，然后退出。</summary>
    public class VenueCapture : MonoBehaviour
    {
        static bool _started;
        readonly List<string> _order = new List<string>();

        void Awake()
        {
            if (_started) { Destroy(gameObject); return; }
            _started = true;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (ConfigDatabase.Installations == null) ConfigDatabase.Load();
            _order.Add("Entrance");
            _order.Add("Education");
            _order.Add("Family");
            _order.Add("Hall");
            if (ConfigDatabase.Installations != null)
                foreach (var m in ConfigDatabase.Installations.modules) _order.Add(m.id);
            StartCoroutine(CaptureAll());
        }

        System.Collections.IEnumerator CaptureAll()
        {
            var dir = CaptureDir();
            var doCapture = HasArg("-capture");
            var doInject = HasArg("-autoInject");
            if (doCapture) Directory.CreateDirectory(dir);
            yield return new WaitForSeconds(1.0f);
            for (var i = 0; i < _order.Count; i++)
            {
                var scene = _order[i];
                if (SceneManager.GetActiveScene().name != scene)
                {
                    SceneManager.LoadScene(scene);
                    yield return null;
                    yield return new WaitForSeconds(1.4f);
                }
                else yield return new WaitForSeconds(0.8f);
                if (doInject)
                {
                    var modules = FindObjectsByType<InstallationBase>();
                    var count = 0;
                    foreach (var m in modules)
                        foreach (var z in m.Zones)
                        {
                            m.InjectSensor(z, "autotest");
                            count++;
                        }
                    Debug.Log("[AutoInject] " + scene + " modules=" + modules.Length + " zones=" + count);
                    yield return new WaitForSeconds(0.35f);
                }
                if (doCapture)
                {
                    yield return new WaitForEndOfFrame();
                    var path = Path.Combine(dir, (i + 1).ToString("00") + "_" + scene + ".png");
                    ScreenCapture.CaptureScreenshot(path, 1);
                    yield return new WaitForSeconds(0.25f);
                    Debug.Log("[Capture] " + path);
                }
            }
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }

        static string CaptureDir()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == "-captureDir") return args[i + 1];
            return Path.Combine(Application.persistentDataPath, "captures");
        }

        public static bool Requested()
        {
            return HasArg("-capture") || HasArg("-autoInject");
        }

        static bool HasArg(string name)
        {
            foreach (var a in Environment.GetCommandLineArgs())
                if (a == name) return true;
            return false;
        }
    }
}

