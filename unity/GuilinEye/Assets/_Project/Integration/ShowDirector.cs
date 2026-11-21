using System;
using System.Collections.Generic;
using System.Text;
using GuilinEye.Core;
using UnityEngine;

namespace GuilinEye.Integration
{
    /// <summary>
    /// BP-02 总演出导演：维护 D-MASTER 720 s 总轴，统一 pause/resume/seek/volume，
    /// 并把总轴时间广播给运维与录制。各模块仍保留自己的 ShowClock 做本地相位。
    /// </summary>
    public class ShowDirector : MonoBehaviour
    {
        public static ShowDirector Instance { get; private set; }

        public ShowClock Master { get; private set; }
        public event Action<SegmentConfig, float> SegmentChanged;

        readonly List<InstallationBase> _modules = new List<InstallationBase>();
        float _lastBroadcastSecond = -1f;

        public int ModuleCount { get { return _modules.Count; } }
        public SegmentConfig Current { get { return Master != null ? Master.Current : null; } }
        public float MasterSeconds { get { return Master != null ? (float)Master.ShowSeconds : 0f; } }
        public bool Paused { get { return Master != null && Master.Paused; } }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Build()
        {
            if (Master == null)
            {
                Master = gameObject.AddComponent<ShowClock>();
                var show = ConfigDatabase.GetShow("D-MASTER");
                if (show == null)
                {
                    Debug.LogError("[BP-02] 缺少 D-MASTER，导演不启动");
                    return;
                }
                Master.Configure(show, 0);
                Master.SegmentChanged += OnMasterSegment;
            }

            _modules.Clear();
            var found = FindObjectsByType<InstallationBase>();
            for (var i = 0; i < found.Length; i++) _modules.Add(found[i]);
            Debug.Log("[BP-02] 总轴 720 s，接管模块 " + _modules.Count);
        }

        void OnMasterSegment(SegmentConfig seg, float local)
        {
            VenueBus.Publish("show.master.segment", "ShowDirector", seg != null ? seg.id : "");
            if (SegmentChanged != null) SegmentChanged(seg, local);
        }

        void Update()
        {
            if (Master == null) return;
            Master.Tick(Time.deltaTime);
            var whole = Mathf.Floor(MasterSeconds);
            if (whole != _lastBroadcastSecond)
            {
                _lastBroadcastSecond = whole;
                VenueBus.Publish("show.master.tick", "ShowDirector", MasterSeconds);
            }
        }

        public void PauseAll()
        {
            if (Master != null) Master.SetPaused(true);
            for (var i = 0; i < _modules.Count; i++) _modules[i].Pause();
            VenueBus.Publish("show.master.paused", "ShowDirector", true);
        }

        public void ResumeAll()
        {
            if (Master != null) Master.SetPaused(false);
            for (var i = 0; i < _modules.Count; i++) _modules[i].Resume();
            VenueBus.Publish("show.master.paused", "ShowDirector", false);
        }

        /// <summary>按 D-MASTER 段 ID（D01…D05）跳转；非 D-MASTER 模块按其自身段 ID 或不跳。</summary>
        public void SeekAll(string segmentId)
        {
            if (Master != null) Master.Seek(SegmentStart(segmentId));
            for (var i = 0; i < _modules.Count; i++)
            {
                var m = _modules[i];
                if (m == null || m.Clock == null || m.Clock.Show == null) continue;
                if (m.Clock.Show.id == "D-MASTER") m.SeekSegment(segmentId);
                else if (ContainsSegment(m.Clock.Show, segmentId)) m.SeekSegment(segmentId);
            }
            VenueBus.Publish("show.master.seek", "ShowDirector", segmentId);
        }

        float SegmentStart(string segmentId)
        {
            if (Master == null || Master.Show == null) return 0f;
            for (var i = 0; i < Master.Show.segments.Length; i++)
                if (Master.Show.segments[i].id == segmentId) return Master.Show.segments[i].start;
            return 0f;
        }

        static bool ContainsSegment(ShowConfig show, string segmentId)
        {
            if (show == null || show.segments == null) return false;
            for (var i = 0; i < show.segments.Length; i++)
                if (show.segments[i].id == segmentId) return true;
            return false;
        }

        public void SetVolumeAll(float volume)
        {
            for (var i = 0; i < _modules.Count; i++) _modules[i].SetVolume(volume);
            VenueBus.Publish("show.master.volume", "ShowDirector", volume);
        }

        public string SnapshotJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"seconds\":").Append(MasterSeconds.ToString("F2"))
              .Append(",\"segment\":\"").Append(Current != null ? Current.id : "")
              .Append("\",\"paused\":").Append(Paused ? "true" : "false")
              .Append(",\"modules\":").Append(_modules.Count).Append('}');
            return sb.ToString();
        }

        void OnDestroy()
        {
            if (Master != null) Master.SegmentChanged -= OnMasterSegment;
            if (Instance == this) Instance = null;
        }
    }
}

