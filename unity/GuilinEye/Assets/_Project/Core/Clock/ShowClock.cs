using System;
using UnityEngine;

namespace GuilinEye.Core
{
    /// <summary>整馆唯一节目时钟。④ 720s 总轴由 EHE 校时，本地只做插值与段切换。</summary>
    public class ShowClock : MonoBehaviour
    {
        ShowConfig _show;
        double _seconds;
        bool _paused;
        int _index = -1;
        double _externalSeconds;
        bool _hasExternal;

        public event Action<SegmentConfig, float> SegmentChanged;

        public ShowConfig Show { get { return _show; } }
        public double ShowSeconds { get { return _seconds; } }
        public bool Paused { get { return _paused; } }
        public SegmentConfig Current { get { return _show != null && _index >= 0 ? _show.segments[_index] : null; } }
        public float SegmentLocal { get { return Current != null ? (float)(_seconds - Current.start) : 0f; } }

        public void Configure(ShowConfig show, double startSeconds = 0)
        {
            _show = show;
            _seconds = show != null ? Mathf.Clamp((float)startSeconds, 0f, show.cycle - 0.001f) : 0;
            _index = -1;
            Evaluate(true);
        }

        public void Tick(float deltaSeconds)
        {
            if (_show == null || _paused) return;
            if (_hasExternal && Mathf.Abs((float)(_externalSeconds - _seconds)) > 2f)
                _seconds = _externalSeconds;
            _hasExternal = false;
            _seconds = (_seconds + deltaSeconds) % _show.cycle;
            Evaluate(false);
        }

        public void ApplyExternalTime(double seconds)
        {
            _externalSeconds = seconds;
            _hasExternal = true;
        }

        public void Seek(double seconds)
        {
            if (_show == null) return;
            _seconds = Mathf.Clamp((float)seconds, 0f, _show.cycle - 0.001f);
            Evaluate(true);
        }

        public void SetPaused(bool paused) { _paused = paused; }

        public float CrossfadeWeight()
        {
            if (_show == null || _show.crossfade <= 0f) return 1f;
            var remain = _show.cycle - (float)_seconds;
            if (remain > _show.crossfade) return 1f;
            return Mathf.Clamp01(remain / _show.crossfade);
        }

        void Evaluate(bool force)
        {
            if (_show == null || _show.segments == null || _show.segments.Length == 0) return;
            var t = (float)_seconds;
            var idx = 0;
            for (var i = 0; i < _show.segments.Length; i++)
            {
                var s = _show.segments[i];
                if (t >= s.start && t < s.start + s.dur) { idx = i; break; }
                if (i == _show.segments.Length - 1) idx = i;
            }
            if (!force && idx == _index) return;
            _index = idx;
            if (SegmentChanged != null) SegmentChanged(Current, SegmentLocal);
        }
    }
}
