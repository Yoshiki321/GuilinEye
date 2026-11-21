using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace GuilinEye.Core
{
    /// <summary>全馆唯一参数源加载器。Editor 读仓库 config，Player 读 StreamingAssets/config。</summary>
    public static class ConfigDatabase
    {
        public static CanvasFile Canvas { get; private set; }
        public static QualityFile Quality { get; private set; }
        public static SensorFile Sensors { get; private set; }
        public static ScheduleFile Schedule { get; private set; }
        public static InstallationsFile Installations { get; private set; }
        public static ArtSlotsFile Art { get; private set; }

        public static bool Loaded { get; private set; }
        public static string ConfigDir { get; private set; }

        static readonly List<string> Errors = new List<string>();
        static Dictionary<string, CanvasConfig> _canvas;
        static Dictionary<string, ModuleConfig> _modules;
        static Dictionary<string, ShowConfig> _shows;
        static Dictionary<string, ZoneConfig> _zones;
        static Dictionary<string, ArtSlotConfig> _artByModule;

        public static IReadOnlyList<string> ValidationErrors { get { return Errors; } }

        public static bool Load()
        {
            Errors.Clear();
            ConfigDir = ResolveDir();
            if (ConfigDir == null)
            {
                Debug.LogError("[Config] 找不到 config 目录");
                return false;
            }
            try
            {
                Canvas = Read<CanvasFile>("canvas.json");
                Quality = Read<QualityFile>("quality.json");
                Sensors = Read<SensorFile>("sensors.json");
                Schedule = Read<ScheduleFile>("schedule.json");
                Installations = Read<InstallationsFile>("installations.json");
                Art = Read<ArtSlotsFile>("art-slots.json");
            }
            catch (Exception e)
            {
                Errors.Add("读取失败：" + e.Message);
                Debug.LogError("[Config] 读取失败：" + e.Message);
                return false;
            }

            _canvas = new Dictionary<string, CanvasConfig>();
            foreach (var c in Canvas.canvases) _canvas[c.id] = c;
            _modules = new Dictionary<string, ModuleConfig>();
            foreach (var m in Installations.modules) _modules[m.id] = m;
            _shows = new Dictionary<string, ShowConfig>();
            foreach (var s in Schedule.shows) _shows[s.id] = s;
            _zones = new Dictionary<string, ZoneConfig>();
            foreach (var z in Sensors.zones) _zones[z.id] = z;
            _artByModule = new Dictionary<string, ArtSlotConfig>();
            foreach (var a in Art.elements) _artByModule[a.elementId] = a;

            Validate();
            Loaded = Errors.Count == 0;
            if (!Loaded)
            {
                foreach (var e in Errors) Debug.LogError("[Config] " + e);
            }
            return Loaded;
        }

        static string ResolveDir()
        {
            var candidates = new List<string>();
#if UNITY_EDITOR
            candidates.Add(Path.GetFullPath(Path.Combine(Application.dataPath, "../../../config")));
#endif
            candidates.Add(Path.Combine(Application.streamingAssetsPath, "config"));
            candidates.Add(Path.Combine(Application.dataPath, "StreamingAssets/config"));
            foreach (var c in candidates)
                if (Directory.Exists(c) && File.Exists(Path.Combine(c, "canvas.json"))) return c;
            return null;
        }

        static T Read<T>(string file)
        {
            var text = File.ReadAllText(Path.Combine(ConfigDir, file));
            return JsonConvert.DeserializeObject<T>(text);
        }

        public static CanvasConfig GetCanvas(string id)
        {
            CanvasConfig c = null;
            return id != null && _canvas != null && _canvas.TryGetValue(id, out c) ? c : null;
        }

        public static ModuleConfig GetModule(string id)
        {
            ModuleConfig m = null;
            return id != null && _modules != null && _modules.TryGetValue(id, out m) ? m : null;
        }

        public static ShowConfig GetShow(string id)
        {
            ShowConfig s = null;
            return id != null && _shows != null && _shows.TryGetValue(id, out s) ? s : null;
        }

        public static ZoneConfig GetZone(string id)
        {
            ZoneConfig z = null;
            return id != null && _zones != null && _zones.TryGetValue(id, out z) ? z : null;
        }

        public static ArtSlotConfig GetArtSlot(string elementId)
        {
            ArtSlotConfig a = null;
            return elementId != null && _artByModule != null && _artByModule.TryGetValue(elementId, out a) ? a : null;
        }

        public static QualityTier GetTier(string tier)
        {
            QualityTier t = null;
            return tier != null && Quality.tiers != null && Quality.tiers.TryGetValue(tier, out t) ? t : null;
        }

        static void Validate()
        {
            if (Canvas == null || Canvas.canvases == null || Canvas.canvases.Length == 0) Errors.Add("canvas.json 为空");
            if (Installations == null || Installations.modules == null) Errors.Add("installations.json 为空");
            if (Installations != null && Installations.modules != null && Installations.modules.Length != 18)
                Errors.Add("模块数应为 18，实际 " + Installations.modules.Length);
            if (Art != null && Art.total != 91) Errors.Add("美术元素应为 91，实际 " + (Art != null ? Art.total : 0));

            foreach (var m in Installations.modules)
            {
                if (m.tier == null || GetTier(m.tier) == null) Errors.Add(m.id + " 画质档无效 " + m.tier);
                if (m.particlesMax > (GetTier(m.tier) != null ? GetTier(m.tier).particlesMax : 0))
                    Errors.Add(m.id + " 粒子上限超档位");
                if (m.show == null || GetShow(m.show) == null) Errors.Add(m.id + " 时间轴不存在 " + m.show);
                foreach (var s in m.surfaces)
                {
                    var c = GetCanvas(s.canvas);
                    if (c == null) { Errors.Add(m.id + " 画布不存在 " + s.canvas); continue; }
                    if (s.rect.x < 0 || s.rect.y < 0 || s.rect.x + s.rect.w > c.width || s.rect.y + s.rect.h > c.height)
                        Errors.Add(m.id + " 矩形越界 " + s.canvas);
                }
                foreach (var it in m.interactions)
                {
                    if (it.zone != null && GetZone(it.zone) == null) Errors.Add(m.id + " 触点不存在 " + it.zone);
                    if (it.zones != null)
                        foreach (var z in it.zones)
                            if (GetZone(z) == null) Errors.Add(m.id + " 触点不存在 " + z);
                }
            }

            var d = GetShow("D-MASTER");
            if (d == null) Errors.Add("缺少 D-MASTER");
            else
            {
                var total = 0f;
                foreach (var seg in d.segments) total += seg.dur;
                if (Mathf.Abs(total - 720f) > 0.01f) Errors.Add("D-MASTER 总长不是 720s");
            }
        }
    }
}
