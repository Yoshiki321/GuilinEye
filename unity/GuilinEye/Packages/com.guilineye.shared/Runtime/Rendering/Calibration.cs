using System;
using System.IO;
using UnityEngine;

namespace GuilinEye.Shared
{
    [Serializable]
    public class ChannelCalibration
    {
        public string channelId;
        public float offsetX;
        public float offsetY;
        public float scaleX = 1f;
        public float scaleY = 1f;
        public float rotationDeg;
        public float brightness = 1f;
        public string note;
    }

    [Serializable]
    public class CalibrationFile
    {
        public string version = "1.0";
        public ChannelCalibration[] channels = new ChannelCalibration[0];
    }

    /// <summary>SP-10 24 通道校准档：mm 偏移、缩放、旋转、亮度；存本地 JSON。</summary>
    public class ProjectionCalibration : MonoBehaviour
    {
        public string fileName = "projection-calibration.json";
        public bool loadOnAwake = true;

        public CalibrationFile Data { get; private set; }

        public string FilePath
        {
            get { return Path.Combine(Application.persistentDataPath, fileName); }
        }

        void Awake()
        {
            if (loadOnAwake) Load();
        }

        public void Load()
        {
            Data = JsonStore.Load<CalibrationFile>(FilePath, null);
            if (Data == null) Data = new CalibrationFile();
            if (Data.channels == null) Data.channels = new ChannelCalibration[0];
        }

        public void Save()
        {
            if (Data == null) Data = new CalibrationFile();
            JsonStore.Save(FilePath, Data, true);
        }

        public ChannelCalibration Get(string channelId)
        {
            if (Data == null || Data.channels == null) return null;
            for (var i = 0; i < Data.channels.Length; i++)
                if (Data.channels[i] != null && Data.channels[i].channelId == channelId) return Data.channels[i];
            return null;
        }

        public void Set(ChannelCalibration calibration)
        {
            if (calibration == null || string.IsNullOrEmpty(calibration.channelId)) return;
            if (Data == null) Data = new CalibrationFile();
            var existing = Get(calibration.channelId);
            if (existing != null)
            {
                existing.offsetX = calibration.offsetX;
                existing.offsetY = calibration.offsetY;
                existing.scaleX = calibration.scaleX;
                existing.scaleY = calibration.scaleY;
                existing.rotationDeg = calibration.rotationDeg;
                existing.brightness = calibration.brightness;
                existing.note = calibration.note;
                return;
            }
            var list = new ChannelCalibration[Data.channels.Length + 1];
            Array.Copy(Data.channels, list, Data.channels.Length);
            list[list.Length - 1] = calibration;
            Data.channels = list;
        }

        public bool Apply(string channelId, Transform target, bool applyBrightness = true)
        {
            var c = Get(channelId);
            if (c == null || target == null) return false;
            target.localPosition = new Vector3(c.offsetX * LayoutMath.MmToMeters, c.offsetY * LayoutMath.MmToMeters, target.localPosition.z);
            target.localScale = new Vector3(c.scaleX, c.scaleY, 1f);
            target.localRotation = Quaternion.Euler(0f, 0f, c.rotationDeg);
            if (applyBrightness) BrightnessGuard.Apply(target.gameObject, null, false, 0f, c.brightness);
            return true;
        }

        public void ApplyAll(Transform root)
        {
            if (root == null || Data == null || Data.channels == null) return;
            for (var i = 0; i < Data.channels.Length; i++)
            {
                var c = Data.channels[i];
                if (c == null) continue;
                var child = root.Find(c.channelId);
                if (child != null) Apply(c.channelId, child, true);
            }
        }

        public void ResetAll()
        {
            Data = new CalibrationFile();
            Save();
        }
    }
}
