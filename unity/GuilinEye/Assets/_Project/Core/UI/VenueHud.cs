using System.Collections.Generic;
using GuilinEye.Core;
using GuilinEye.Installations;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuilinEye.UI
{
    /// <summary>组合场景总 HUD：18 模块状态、总轴、切区、暂停/跳段。播放器首屏即可用。</summary>
    public class VenueHud : MonoBehaviour
    {
        VenueBootstrap _venue;
        bool _hidden;
        bool _paused;
        int _selected;
        float _nextRefresh;
        string _fps = "-";
        GUIStyle _title;
        GUIStyle _small;
        readonly List<string> _names = new List<string>();

        void Start()
        {
            _venue = FindAnyObjectByType<VenueBootstrap>();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.H)) _hidden = !_hidden;
            if (Input.GetKeyDown(KeyCode.F1)) SceneManager.LoadScene("Entrance");
            if (Input.GetKeyDown(KeyCode.F2)) SceneManager.LoadScene("Education");
            if (Input.GetKeyDown(KeyCode.F3)) SceneManager.LoadScene("Family");
            if (Input.GetKeyDown(KeyCode.F4)) SceneManager.LoadScene("Hall");
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _paused = !_paused;
                if (_venue != null) { if (_paused) _venue.PauseAll(); else _venue.ResumeAll(); }
            }
            if (Input.GetKeyDown(KeyCode.Tab) && _venue != null && _venue.Modules.Count > 0)
                _selected = (_selected + 1) % _venue.Modules.Count;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Step(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) Step(1);
        }

        void Step(int dir)
        {
            if (_venue == null || _venue.Modules.Count == 0) return;
            var master = _venue.Modules[0].Clock;
            if (master == null || master.Show == null || master.Show.segments == null) return;
            var idx = 0;
            for (var i = 0; i < master.Show.segments.Length; i++)
                if (master.Current != null && master.Show.segments[i].id == master.Current.id) { idx = i; break; }
            idx = Mathf.Clamp(idx + dir, 0, master.Show.segments.Length - 1);
            _venue.SeekAll(master.Show.segments[idx].id);
        }

        void OnGUI()
        {
            if (_hidden) return;
            if (_venue == null) _venue = FindAnyObjectByType<VenueBootstrap>();
            if (_venue == null) return;
            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label);
                _title.fontSize = 17;
                _title.fontStyle = FontStyle.Bold;
                _small = new GUIStyle(GUI.skin.label);
                _small.fontSize = 12;
            }
            if (Time.unscaledTime > _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.25f;
                _fps = (1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString("0");
            }
            GUILayout.BeginArea(new Rect(14f, 14f, 420f, 560f), GUI.skin.box);
            GUILayout.Label("GUILIN EYE · VENUE CONTROL", _title);
            if (_venue.Modules.Count > 0 && _venue.Modules[0].Clock != null && _venue.Modules[0].Clock.Show != null)
            {
                var c = _venue.Modules[0].Clock;
                var seg = c.Current;
                GUILayout.Label("MASTER " + c.Show.id + "  " + c.ShowSeconds.ToString("0.0") + " / " + c.Show.cycle.ToString("0") + " s"
                    + (seg != null ? "   [" + seg.id + "]" : "") + "   FPS " + _fps, _small);
            }
            GUILayout.Space(4f);
            for (var i = 0; i < _venue.Modules.Count; i++)
            {
                var m = _venue.Modules[i];
                var selected = i == _selected;
                var label = (selected ? "▶ " : "   ") + m.Id + "  " + m.Cfg.name + "   " + (m.GetComponent<ModuleKit>() != null ? m.GetComponent<ModuleKit>().CurrentStateName : "-");
                if (GUILayout.Button(label, selected ? GUI.skin.box : GUI.skin.label))
                {
                    _selected = i;
                    if (m.Clock != null && m.Clock.Show != null && m.Clock.Show.segments != null && m.Clock.Show.segments.Length > 0)
                        m.Clock.Seek(m.Clock.Show.segments[0].start);
                }
            }
            GUILayout.Space(4f);
            GUILayout.Label("SPACE pause   ←/→ master segment   TAB next module   H hide", _small);
            GUILayout.Label("F1 Entrance(A)   F2 Education(B)   F3 Family(C)   F4 Hall(D)", _small);
            GUILayout.EndArea();
        }
    }
}

