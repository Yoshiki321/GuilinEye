using System.Collections.Generic;
using GuilinEye.Core;
using UnityEngine;

namespace GuilinEye.UI
{
    /// <summary>单模块场景 HUD：状态、时间、传感器点、键盘模拟。无硬件也能验收每个展项。</summary>
    public class ModuleHud : MonoBehaviour
    {
        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { "A01", "STONE ELEPHANT / FOUR SEASONS" },
            { "A02", "WATER-MOON ARCH" },
            { "A03", "ANCIENT GREETER" },
            { "A04", "INTO THE SCROLL" },
            { "B01", "KARST / TIME DISSOLVES" },
            { "B02", "HERITAGE / RIVERSIDE MARKET" },
            { "B03", "CLIFF INSCRIPTIONS / WAR OF INK" },
            { "B04", "POETRY / FALLING WORDS" },
            { "C01", "SPIRIT ELEPHANT SCORE" },
            { "C02", "LANDSCAPE BODY GAME" },
            { "C03", "WISHING LANTERNS" },
            { "C04", "THOUSAND HANDS MURAL" },
            { "C05", "CREATIVE SHOP PICKUP" },
            { "D01", "FOUR SEASONS" },
            { "D02", "FLOWER FIELD" },
            { "D03", "MOONRISE" },
            { "D04", "MOUNTAIN BORN" },
            { "D05", "BUTTERFLY RING" }
        };

        InstallationBase _module;
        bool _hidden;
        bool _mock;
        float _nextMock;
        int _mockIndex;
        readonly List<string> _zones = new List<string>();
        GUIStyle _title;

        void Start()
        {
            _module = GetComponent<InstallationBase>();
            if (_module != null)
                foreach (var z in _module.Zones) if (!_zones.Contains(z)) _zones.Add(z);
        }

        void Update()
        {
            if (_module == null || _module.Clock == null) return;
            if (Input.GetKeyDown(KeyCode.H)) _hidden = !_hidden;
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (_module.Clock.Paused) _module.Resume(); else _module.Pause();
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Seek(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) Seek(1);
            if (Input.GetKeyDown(KeyCode.R)) _module.Clock.Seek(0);
            if (Input.GetKeyDown(KeyCode.M)) { _mock = !_mock; _nextMock = Time.time; }
            for (var i = 0; i < 9 && i < _zones.Count; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                    _module.InjectSensor(_zones[i], "touch");

            if (_mock && Time.time >= _nextMock && _zones.Count > 0)
            {
                _nextMock = Time.time + 2.4f;
                _module.InjectSensor(_zones[_mockIndex % _zones.Count], "mock");
                _mockIndex++;
            }
        }

        void Seek(int dir)
        {
            var show = _module.Clock.Show;
            if (show == null || show.segments == null || show.segments.Length == 0) return;
            var idx = 0;
            for (var i = 0; i < show.segments.Length; i++)
                if (_module.Clock.Current != null && show.segments[i].id == _module.Clock.Current.id) { idx = i; break; }
            idx = Mathf.Clamp(idx + dir, 0, show.segments.Length - 1);
            _module.Clock.Seek(show.segments[idx].start);
        }

        void OnGUI()
        {
            if (_hidden) return;
            if (_module == null) _module = GetComponent<InstallationBase>();
            if (_module == null) return;
            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label);
                _title.fontSize = 18;
                _title.fontStyle = FontStyle.Bold;
            }
            GUILayout.BeginArea(new Rect(14f, 14f, 390f, 280f), GUI.skin.box);
            GUILayout.Label(_module.Id + " · " + (Names.ContainsKey(_module.Id) ? Names[_module.Id] : _module.Cfg.name), _title);
            GUILayout.Label("STATE  " + (_module.GetComponent<ModuleKit>() != null ? _module.GetComponent<ModuleKit>().CurrentStateName : "-")
                + "   |   TIME " + (_module.Clock != null ? _module.Clock.ShowSeconds.ToString("0.0") : "-")
                + " / " + (_module.Clock != null && _module.Clock.Show != null ? _module.Clock.Show.cycle.ToString("0") : "-") + " s");
            var seg = _module.Clock != null ? _module.Clock.Current : null;
            GUILayout.Label("SEGMENT  " + (seg != null ? seg.id + "  局部 " + _module.Clock.SegmentLocal.ToString("0.0") + " s" : "-"));
            GUILayout.Label("CANVAS   " + (_module.Cfg.surfaces.Length > 0 ? _module.Cfg.surfaces[0].canvas : "-")
                + "   ZONES " + _zones.Count + (_mock ? "   [AUTO MOCK]" : ""));
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Inject")) _module.InjectSensor(_zones.Count > 0 ? _zones[0] : "", "touch");
            if (GUILayout.Button(_module.Clock.Paused ? "Resume" : "Pause")) { if (_module.Clock.Paused) _module.Resume(); else _module.Pause(); }
            if (GUILayout.Button("Next seg")) Seek(1);
            if (GUILayout.Button("Reset")) _module.Clock.Seek(0);
            GUILayout.EndHorizontal();
            if (_zones.Count > 0)
            {
                GUILayout.Space(2f);
                GUILayout.Label("ZONES  " + string.Join(" · ", _zones.GetRange(0, Mathf.Min(5, _zones.Count)).ToArray()));
            }
            GUILayout.Label("SPACE pause  ←/→ segment  M auto mock  1-9 inject  H hide", GUI.skin.label);
            GUILayout.EndArea();
        }
    }
}
