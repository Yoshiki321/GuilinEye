using System;
using System.Collections.Generic;
using System.Text;
using GuilinEye.Core;
using GuilinEye.Shared;
using UnityEngine;

namespace GuilinEye.Integration
{
    /// <summary>
    /// BP-05 校准台：把 24 通道的偏移/缩放/旋转/亮度写入 ProjectionCalibration，
    /// 并应用到 BP-01 的预览相机。Editor 窗口和现场快捷键共用本组件。
    /// </summary>
    public class CalibrationTool : MonoBehaviour
    {
        public static CalibrationTool Instance { get; private set; }

        public ProjectionRig rig;
        public ProjectionCalibration store;
        public bool autoApply = true;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Build()
        {
            if (rig == null) rig = ProjectionRig.Instance;
            if (rig == null) rig = FindAnyObjectByType<ProjectionRig>();
            if (store == null) store = GetComponent<ProjectionCalibration>();
            if (store == null) store = gameObject.AddComponent<ProjectionCalibration>();
            if (store.Data == null) store.Load();
            if (autoApply) ApplyAll();
            Debug.Log("[BP-05] 校准台就绪，通道 " + (rig != null ? rig.ChannelCount : 0));
        }

        public void ApplyAll()
        {
            if (rig == null || store == null) return;
            var channels = rig.Channels;
            for (var i = 0; i < channels.Count; i++)
            {
                var cal = store.Get(channels[i].id);
                if (cal != null) rig.ApplyCalibration(cal);
            }
        }

        public void CaptureAll()
        {
            if (rig == null || store == null) return;
            var channels = rig.Channels;
            for (var i = 0; i < channels.Count; i++)
            {
                var cal = rig.CaptureCalibration(channels[i].id);
                if (cal != null) store.Set(cal);
            }
        }

        public ChannelCalibration Get(string channelId)
        {
            if (store != null)
            {
                var c = store.Get(channelId);
                if (c != null) return c;
            }
            return rig != null ? rig.CaptureCalibration(channelId) : null;
        }

        public void SetOffset(string channelId, float offsetX, float offsetY)
        {
            var c = Ensure(channelId);
            c.offsetX = offsetX;
            c.offsetY = offsetY;
            Commit(c);
        }

        public void SetScale(string channelId, float scaleX, float scaleY)
        {
            var c = Ensure(channelId);
            c.scaleX = Mathf.Max(0.01f, scaleX);
            c.scaleY = Mathf.Max(0.01f, scaleY);
            Commit(c);
        }

        public void SetRotation(string channelId, float rotationDeg)
        {
            var c = Ensure(channelId);
            c.rotationDeg = rotationDeg;
            Commit(c);
        }

        public void SetBrightness(string channelId, float brightness)
        {
            var c = Ensure(channelId);
            c.brightness = Mathf.Clamp01(brightness);
            Commit(c);
        }

        public void Nudge(string channelId, float dx, float dy)
        {
            var c = Ensure(channelId);
            c.offsetX += dx;
            c.offsetY += dy;
            Commit(c);
        }

        ChannelCalibration Ensure(string channelId)
        {
            var c = Get(channelId);
            if (c == null) c = new ChannelCalibration { channelId = channelId, scaleX = 1f, scaleY = 1f, brightness = 1f };
            return c;
        }

        void Commit(ChannelCalibration cal)
        {
            if (store != null) store.Set(cal);
            if (rig != null) rig.ApplyCalibration(cal);
        }

        public void Save()
        {
            if (store != null) store.Save();
            Debug.Log("[BP-05] 校准已保存：" + (store != null ? store.FilePath : "无 store"));
        }

        public void ResetAll()
        {
            if (store == null) return;
            store.ResetAll();
            ApplyAll();
        }

        public string SnapshotJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"channels\":").Append(rig != null ? rig.ChannelCount : 0)
              .Append(",\"file\":\"").Append(store != null ? store.FilePath.Replace("\\", "/") : "")
              .Append("\",\"items\":[");
            if (rig != null)
            {
                var list = rig.Channels;
                for (var i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var c = Get(list[i].id);
                    sb.Append("{\"id\":\"").Append(list[i].id).Append("\",\"ox\":").Append(c != null ? c.offsetX : 0f)
                      .Append(",\"oy\":").Append(c != null ? c.offsetY : 0f)
                      .Append(",\"sx\":").Append(c != null ? c.scaleX : 1f)
                      .Append(",\"sy\":").Append(c != null ? c.scaleY : 1f)
                      .Append(",\"rot\":").Append(c != null ? c.rotationDeg : 0f)
                      .Append(",\"bright\":").Append(c != null ? c.brightness : 1f).Append('}');
                }
            }
            sb.Append("]}");
            return sb.ToString();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}

