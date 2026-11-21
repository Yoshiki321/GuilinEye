using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace GuilinEye.Hardware
{
    /// <summary>
    /// 现场硬件端点配置。默认值与 03_硬件与对接清单 的 Gate v1.1 一致：
    /// S1/S2/S3 = ORBBEC Femto Bolt；R1/R2 = 北阳 10m 雷达（TUIO）；IR = 红外（TUIO）。
    /// 现场只改 config/hardware.json，不改代码。
    /// </summary>
    [Serializable]
    public class HardwareFile
    {
        public string version;
        public string note;
        public TuioEndpoint[] radar;
        public TuioEndpoint[] infrared;
        public FemtoEndpoint[] femto;
    }

    [Serializable]
    public class TuioEndpoint
    {
        public bool enabled = true;
        public string sensorId;
        public int port = 3333;
        public string source;
        public bool allowNoSource = true;
        public int idMin = -1;
        public int idMax = -1;

        // 调试软件不输出 source 时，用归一化 X 把同一端口的光标分给 R1/R2，避免两边同时触发。
        // -1 表示不启用该分段；R1 取 0.5—1.0，R2 取 0.0—0.499。
        public float fallbackMinX = -1f;
        public float fallbackMaxX = 1f;

        // TUIO 归一化坐标 -> 画布 mm。x/y 是左上原点，flipY 用于 TUIO 左下原点。
        public float originX;
        public float originY;
        public float spanX = 4000f;
        public float spanY = 3000f;
        public bool flipX;
        public bool flipY = true;

        public string defaultZone;
    }

    [Serializable]
    public class FemtoEndpoint
    {
        public bool enabled = true;
        public string sensorId;
        public string serial;
        public int index;
        public string zone;
        public int width = 640;
        public int height = 576;
        public int fps = 30;
        public int minDepthMm = 450;
        public int maxDepthMm = 6000;

        // 现场未接骨骼 SDK 时，用深度帧质心的左右往复做“挥手”启发式；
        // 只用于 C01/C02 的触发，不冒充 32 关节骨架。
        public bool gesture = true;
        public bool flipX;
    }

    public static class HardwareSettings
    {
        static HardwareFile _file;
        static string _loadedFrom;

        public static HardwareFile File
        {
            get
            {
                if (_file == null) Load();
                return _file;
            }
        }

        public static void Reload()
        {
            _file = null;
            _loadedFrom = null;
            Load();
        }

        public static string LoadedFrom { get { return _loadedFrom; } }

        static void Load()
        {
            var path = ResolvePath();
            if (path != null)
            {
                try
                {
                    _file = JsonConvert.DeserializeObject<HardwareFile>(System.IO.File.ReadAllText(path));
                    _loadedFrom = path;
                    if (_file != null)
                    {
                        Normalize(_file);
                        Debug.Log("[Hardware] 已读取 " + path);
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Hardware] 读取失败 " + path + "：" + e.Message + "，使用内置默认值");
                }
            }

            _file = Defaults();
            _loadedFrom = "(built-in defaults)";
            Debug.Log("[Hardware] 使用内置端点默认值（config/hardware.json 不存在）");
        }

        static string ResolvePath()
        {
            var candidates = new List<string>();
#if UNITY_EDITOR
            candidates.Add(Path.GetFullPath(Path.Combine(Application.dataPath, "../../../config/hardware.json")));
#endif
            candidates.Add(Path.Combine(Application.streamingAssetsPath, "config/hardware.json"));
            candidates.Add(Path.Combine(Application.dataPath, "StreamingAssets/config/hardware.json"));
            foreach (var c in candidates)
                if (System.IO.File.Exists(c)) return c;
            return null;
        }

        static void Normalize(HardwareFile f)
        {
            if (f.radar == null || f.radar.Length == 0) f.radar = Defaults().radar;
            if (f.infrared == null || f.infrared.Length == 0) f.infrared = Defaults().infrared;
            if (f.femto == null || f.femto.Length == 0) f.femto = Defaults().femto;
            foreach (var e in f.radar) if (e != null && string.IsNullOrEmpty(e.sensorId)) e.sensorId = "R1";
            foreach (var e in f.infrared) if (e != null && string.IsNullOrEmpty(e.sensorId)) e.sensorId = "IR";
            foreach (var e in f.femto) if (e != null && string.IsNullOrEmpty(e.sensorId)) e.sensorId = "S3";
        }

        public static TuioEndpoint Radar(string sensorId)
        {
            foreach (var e in File.radar)
                if (e != null && e.sensorId == sensorId) return e;
            return null;
        }

        public static TuioEndpoint[] Infrared()
        {
            return File.infrared;
        }

        public static FemtoEndpoint Femto(string sensorId)
        {
            foreach (var e in File.femto)
                if (e != null && e.sensorId == sensorId) return e;
            return null;
        }

        static HardwareFile Defaults()
        {
            return new HardwareFile
            {
                version = "1.0",
                note = "现场硬件端点默认值；以 config/hardware.json 覆盖。",
                radar = new[]
                {
                    new TuioEndpoint
                    {
                        sensorId = "R1", port = 3333, source = "R1",
                        originX = 3400f, originY = 0f, spanX = 4600f, spanY = 3000f,
                        flipY = true, defaultZone = "T8",
                        fallbackMinX = 0.5f, fallbackMaxX = 1f
                    },
                    new TuioEndpoint
                    {
                        sensorId = "R2", port = 3333, source = "R2",
                        originX = 1200f, originY = 0f, spanX = 3600f, spanY = 3000f,
                        flipY = true, defaultZone = "T7",
                        fallbackMinX = 0f, fallbackMaxX = 0.499f
                    }
                },
                infrared = new[]
                {
                    new TuioEndpoint
                    {
                        sensorId = "IR", port = 3334, source = "IR",
                        allowNoSource = true, defaultZone = "F5"
                    }
                },
                femto = new[]
                {
                    new FemtoEndpoint { sensorId = "S1", index = 0, zone = "C01", gesture = true },
                    new FemtoEndpoint { sensorId = "S2", index = 1, zone = "C02", gesture = true },
                    new FemtoEndpoint { sensorId = "S3", index = 2, zone = "S3-ENTRY", gesture = false }
                }
            };
        }
    }
}
