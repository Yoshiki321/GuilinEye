using GuilinEye.Integration;
using GuilinEye.Shared;
using UnityEditor;
using UnityEngine;

namespace GuilinEye.Editor
{
    /// <summary>BP-05 的 Editor 面板：选通道、调偏移/缩放/旋转/亮度、保存到 persistentDataPath。</summary>
    public class CalibrationToolWindow : EditorWindow
    {
        CalibrationTool _tool;
        ProjectionRig _rig;
        string _channelId;
        Vector2 _scroll;
        float _ox, _oy, _sx = 1f, _sy = 1f, _rot, _bright = 1f;

        [MenuItem("桂林之眼/5. 打开 24 通道校准台")]
        public static void Open()
        {
            var w = GetWindow<CalibrationToolWindow>("24 通道校准台");
            w.minSize = new Vector2(420, 360);
            w.Refresh();
        }

        void OnFocus() { Refresh(); }

        void Refresh()
        {
            _tool = FindAnyObjectByType<CalibrationTool>();
            _rig = _tool != null ? _tool.rig : FindAnyObjectByType<ProjectionRig>();
            if (_channelId == null && _rig != null && _rig.ChannelCount > 0)
                _channelId = _rig.GetChannelIds()[0];
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("BP-05 校准台", EditorStyles.boldLabel);
            if (_rig == null)
            {
                EditorGUILayout.HelpBox("场景中未找到 ProjectionRig。先运行 VenueBootstrap 或进入 Play。", MessageType.Warning);
                if (GUILayout.Button("重新查找")) Refresh();
                return;
            }

            EditorGUILayout.LabelField("通道 " + _rig.ChannelCount + " / 画布 " + _rig.CanvasCount);
            if (_rig.ChannelCount == 0) return;

            var ids = _rig.GetChannelIds();
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(90));
            for (var i = 0; i < ids.Length; i++)
            {
                if (GUILayout.Button((_channelId == ids[i] ? "▶ " : "  ") + ids[i], EditorStyles.miniButton))
                {
                    _channelId = ids[i];
                    LoadSelected();
                }
            }
            EditorGUILayout.EndScrollView();

            if (string.IsNullOrEmpty(_channelId)) return;
            EditorGUILayout.Space();
            _ox = EditorGUILayout.FloatField("偏移 X (mm)", _ox);
            _oy = EditorGUILayout.FloatField("偏移 Y (mm)", _oy);
            _sx = EditorGUILayout.Slider("缩放 X", _sx, 0.5f, 1.5f);
            _sy = EditorGUILayout.Slider("缩放 Y", _sy, 0.5f, 1.5f);
            _rot = EditorGUILayout.Slider("旋转 (deg)", _rot, -5f, 5f);
            _bright = EditorGUILayout.Slider("亮度", _bright, 0f, 1f);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("应用")) Apply();
                if (GUILayout.Button("保存")) { Apply(); if (_tool != null) _tool.Save(); else SaveStore(); }
                if (GUILayout.Button("复位全部")) { if (_tool != null) _tool.ResetAll(); }
            }
            if (_tool == null)
                EditorGUILayout.HelpBox("未找到 CalibrationTool；应用只改 ProjectionRig，保存不可用。", MessageType.Info);

            EditorGUILayout.Space();
            if (GUILayout.Button(_rig.previewEnabled ? "关闭预览" : "开启预览"))
            {
                _rig.SetPreviewEnabled(!_rig.previewEnabled);
                SceneView.RepaintAll();
            }
        }

        void LoadSelected()
        {
            var c = _tool != null ? _tool.Get(_channelId) : _rig.CaptureCalibration(_channelId);
            if (c == null) return;
            _ox = c.offsetX; _oy = c.offsetY;
            _sx = c.scaleX; _sy = c.scaleY;
            _rot = c.rotationDeg; _bright = c.brightness;
        }

        void Apply()
        {
            if (_channelId == null) return;
            var cal = new ChannelCalibration
            {
                channelId = _channelId,
                offsetX = _ox, offsetY = _oy,
                scaleX = _sx, scaleY = _sy,
                rotationDeg = _rot, brightness = _bright
            };
            if (_tool != null)
            {
                _tool.SetOffset(_channelId, _ox, _oy);
                _tool.SetScale(_channelId, _sx, _sy);
                _tool.SetRotation(_channelId, _rot);
                _tool.SetBrightness(_channelId, _bright);
            }
            else _rig.ApplyCalibration(cal);
        }

        void SaveStore()
        {
            var store = FindAnyObjectByType<ProjectionCalibration>();
            if (store == null) return;
            var cal = _rig.CaptureCalibration(_channelId);
            if (cal != null) store.Set(cal);
            store.Save();
        }
    }
}

