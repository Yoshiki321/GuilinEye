using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuilinEye.Shared
{
    [Serializable]
    public class TimelineSegment
    {
        public string id;
        public float start;
        public float dur;

        public float End { get { return start + dur; } }
    }

    /// <summary>SP-03 通用段时间轴：循环、暂停、seek、段事件、交叉淡化权重。</summary>
    public sealed class Timeline
    {
        readonly List<TimelineSegment> _segments;
        float _seconds;
        bool _paused;
        int _index = -1;

        public readonly float CycleSeconds;
        public readonly float CrossfadeSeconds;
        public readonly bool Loop;

        public event Action<TimelineSegment, float> SegmentChanged;

        public Timeline(IEnumerable<TimelineSegment> segments, float cycleSeconds = 0f, float crossfadeSeconds = 3f, bool loop = true)
        {
            _segments = new List<TimelineSegment>();
            if (segments != null)
            {
                foreach (var s in segments)
                    if (s != null && !string.IsNullOrEmpty(s.id)) _segments.Add(s);
            }
            _segments.Sort((a, b) => a.start.CompareTo(b.start));
            var max = 0f;
            foreach (var s in _segments) if (s.End > max) max = s.End;
            CycleSeconds = cycleSeconds > 0f ? cycleSeconds : max;
            CrossfadeSeconds = Mathf.Max(0f, crossfadeSeconds);
            Loop = loop;
            Evaluate(true);
        }

        public IReadOnlyList<TimelineSegment> Segments { get { return _segments; } }
        public float Seconds { get { return _seconds; } }
        public bool Paused { get { return _paused; } }
        public TimelineSegment Current { get { return _index >= 0 && _index < _segments.Count ? _segments[_index] : null; } }
        public int CurrentIndex { get { return _index; } }
        public float LocalSeconds { get { return Current != null ? _seconds - Current.start : 0f; } }
        public float Normalized { get { return CycleSeconds > 0f ? Mathf.Clamp01(_seconds / CycleSeconds) : 0f; } }
        public bool Finished { get { return !Loop && CycleSeconds > 0f && _seconds >= CycleSeconds; } }

        public void Tick(float deltaSeconds)
        {
            if (_paused || _segments.Count == 0 || CycleSeconds <= 0f) return;
            _seconds += Mathf.Max(0f, deltaSeconds);
            if (Loop) _seconds = Mathf.Repeat(_seconds, CycleSeconds);
            else _seconds = Mathf.Min(_seconds, CycleSeconds);
            Evaluate(false);
        }

        public void Seek(float seconds)
        {
            if (CycleSeconds <= 0f) return;
            _seconds = Loop ? Mathf.Repeat(seconds, CycleSeconds) : Mathf.Clamp(seconds, 0f, CycleSeconds);
            Evaluate(true);
        }

        public void SetPaused(bool paused) { _paused = paused; }
        public void Pause() { _paused = true; }
        public void Resume() { _paused = false; }

        public float CrossfadeWeight()
        {
            if (CrossfadeSeconds <= 0f || CycleSeconds <= 0f) return 1f;
            var remain = CycleSeconds - _seconds;
            if (remain >= CrossfadeSeconds) return 1f;
            return Mathf.Clamp01(remain / CrossfadeSeconds);
        }

        void Evaluate(bool force)
        {
            if (_segments.Count == 0) { _index = -1; return; }
            var t = _seconds;
            var idx = _segments.Count - 1;
            for (var i = 0; i < _segments.Count; i++)
            {
                var s = _segments[i];
                if (t >= s.start && t < s.End) { idx = i; break; }
                if (i == 0 && t < s.start) { idx = i; break; }
            }
            if (!force && idx == _index) return;
            _index = idx;
            if (SegmentChanged != null) SegmentChanged(Current, LocalSeconds);
        }
    }
}
