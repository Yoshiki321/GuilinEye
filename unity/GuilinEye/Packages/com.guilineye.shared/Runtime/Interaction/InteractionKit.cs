using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuilinEye.Shared
{
    [Serializable]
    public struct InputSignal
    {
        public string sensorId;
        public string type;
        public string zoneId;
        public float x;
        public float y;
        public float confidence;
        public long timestampMs;
        public bool hasPosition;
    }

    [Serializable]
    public class InteractionRule
    {
        public string id;
        public string action;
        public string type;
        public string[] zones;
        public float cooldown;
        public float debounce;
        public bool requirePosition;
    }

    /// <summary>SP-07 传感器信号 → 动作路由；带类型/区域过滤、冷却、防抖。</summary>
    public sealed class InteractionRouter
    {
        sealed class Entry
        {
            public InteractionRule rule;
            public Action<InputSignal> handler;
        }

        readonly List<Entry> _entries = new List<Entry>();
        readonly CooldownGate _cooldowns = new CooldownGate();
        readonly DebounceGate _debounces = new DebounceGate();

        public event Action<InteractionRule, InputSignal> Dispatched;

        public void Register(InteractionRule rule, Action<InputSignal> handler)
        {
            if (rule == null || handler == null) return;
            if (string.IsNullOrEmpty(rule.id)) rule.id = "rule-" + _entries.Count;
            _entries.Add(new Entry { rule = rule, handler = handler });
        }

        public void Push(InputSignal signal, float now)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!Matches(e.rule, signal)) continue;
                var key = e.rule.id + "|" + (string.IsNullOrEmpty(signal.zoneId) ? "-" : signal.zoneId);
                if (!_cooldowns.TryPass(key, e.rule.cooldown, now)) continue;
                if (!_debounces.TryPass(key, e.rule.debounce, now)) continue;
                try { e.handler(signal); }
                catch (Exception ex) { LogRouter.Error("InteractionRouter", e.rule.id + ": " + ex.Message); }
                if (Dispatched != null) Dispatched(e.rule, signal);
            }
        }

        static bool Matches(InteractionRule rule, InputSignal signal)
        {
            if (!string.IsNullOrEmpty(rule.type) && rule.type != signal.type) return false;
            if (rule.requirePosition && !signal.hasPosition) return false;
            if (rule.zones == null || rule.zones.Length == 0) return true;
            if (string.IsNullOrEmpty(signal.zoneId)) return false;
            for (var i = 0; i < rule.zones.Length; i++)
                if (rule.zones[i] == signal.zoneId) return true;
            return false;
        }

        public void Clear()
        {
            _entries.Clear();
            _cooldowns.Clear();
            _debounces.Clear();
        }

        public int RuleCount { get { return _entries.Count; } }
    }

    /// <summary>SP-07 有序手势检测，例如 B03 起笔→行笔→落款。</summary>
    public sealed class SequenceDetector
    {
        readonly string[] _expected;
        readonly float _timeout;
        int _index;
        float _lastAt;

        public event Action Completed;

        public SequenceDetector(string[] expected, float timeoutSeconds = 3f)
        {
            _expected = expected ?? new string[0];
            _timeout = timeoutSeconds;
        }

        public int Progress { get { return _index; } }
        public bool IsComplete { get { return _expected.Length > 0 && _index >= _expected.Length; } }

        public bool Push(string token, float now)
        {
            if (_expected.Length == 0 || string.IsNullOrEmpty(token)) return false;
            if (_lastAt > 0f && now - _lastAt > _timeout) _index = 0;
            _lastAt = now;

            if (token == _expected[_index]) _index++;
            else if (token == _expected[0]) _index = 1;
            else _index = 0;

            if (_index >= _expected.Length)
            {
                _index = 0;
                if (Completed != null) Completed();
                return true;
            }
            return false;
        }

        public void Reset() { _index = 0; _lastAt = 0f; }
    }

    /// <summary>SP-07 停留计时；站定 3 s 聚花环、悬停二次特写等。</summary>
    public sealed class DwellTracker
    {
        readonly Dictionary<string, float> _start = new Dictionary<string, float>();

        public float Update(string key, bool present, float now)
        {
            if (string.IsNullOrEmpty(key)) return 0f;
            if (!present)
            {
                _start.Remove(key);
                return 0f;
            }
            float start;
            if (!_start.TryGetValue(key, out start)) { start = now; _start[key] = now; }
            return now - start;
        }

        public float Get(string key, float now)
        {
            float start;
            return key != null && _start.TryGetValue(key, out start) ? now - start : 0f;
        }

        public void Reset(string key) { if (key != null) _start.Remove(key); }
        public void Clear() { _start.Clear(); }
    }

    /// <summary>SP-07 区域人数/占位计数，处理并发与离开事件。</summary>
    public sealed class OccupancyCounter
    {
        readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

        public event Action<string, int> Changed;

        public void Enter(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) return;
            int count;
            _counts.TryGetValue(zoneId, out count);
            _counts[zoneId] = count + 1;
            if (Changed != null) Changed(zoneId, count + 1);
        }

        public void Exit(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) return;
            int count;
            if (!_counts.TryGetValue(zoneId, out count)) return;
            count = Mathf.Max(0, count - 1);
            _counts[zoneId] = count;
            if (Changed != null) Changed(zoneId, count);
        }

        public int Count(string zoneId)
        {
            int count;
            return zoneId != null && _counts.TryGetValue(zoneId, out count) ? count : 0;
        }

        public int Total
        {
            get
            {
                var sum = 0;
                foreach (var c in _counts.Values) sum += c;
                return sum;
            }
        }

        public void Reset(string zoneId) { if (zoneId != null) _counts.Remove(zoneId); }
        public void Clear() { _counts.Clear(); }
    }
}
