using System.Collections.Generic;

namespace GuilinEye.Shared
{
    /// <summary>SP-06 固定冷却：只有通过时才更新时间戳。</summary>
    public sealed class CooldownGate
    {
        readonly Dictionary<string, float> _last = new Dictionary<string, float>();

        public bool TryPass(string key, float cooldownSeconds, float now)
        {
            if (string.IsNullOrEmpty(key)) return true;
            float last;
            if (_last.TryGetValue(key, out last) && now - last < cooldownSeconds) return false;
            _last[key] = now;
            return true;
        }

        public void Reset(string key) { if (key != null) _last.Remove(key); }
        public void Clear() { _last.Clear(); }
    }

    /// <summary>SP-06 防抖/节流：两次通过之间至少间隔 interval。</summary>
    public sealed class DebounceGate
    {
        readonly Dictionary<string, float> _last = new Dictionary<string, float>();

        public bool TryPass(string key, float intervalSeconds, float now)
        {
            if (string.IsNullOrEmpty(key)) return true;
            float last;
            if (_last.TryGetValue(key, out last) && now - last < intervalSeconds) return false;
            _last[key] = now;
            return true;
        }

        public void Reset(string key) { if (key != null) _last.Remove(key); }
        public void Clear() { _last.Clear(); }
    }

    /// <summary>SP-06 进出滞回：高于 enter 才进，低于 exit 才出，避免边界抖动。</summary>
    public sealed class HysteresisGate
    {
        readonly float _enter;
        readonly float _exit;

        public bool IsHigh { get; private set; }

        public HysteresisGate(float enterThreshold, float exitThreshold)
        {
            _enter = enterThreshold;
            _exit = exitThreshold < enterThreshold ? exitThreshold : enterThreshold - 0.001f;
        }

        public bool Update(float value)
        {
            if (IsHigh)
            {
                if (value <= _exit) IsHigh = false;
            }
            else
            {
                if (value >= _enter) IsHigh = true;
            }
            return IsHigh;
        }

        public void Reset() { IsHigh = false; }
    }
}
