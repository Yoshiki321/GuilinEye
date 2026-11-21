using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuilinEye.Shared
{
    public enum LogLevel { Info, Warn, Error }

    [Serializable]
    public class LogEntry
    {
        public string channel;
        public string message;
        public LogLevel level;
        public long atUnixMs;
    }

    /// <summary>SP-20 分通道日志；不替代 Debug.Log，补“能查最近 N 条”的能力。</summary>
    public static class LogRouter
    {
        static readonly RingBuffer<LogEntry> RecentLogs = new RingBuffer<LogEntry>(256);
        static readonly Dictionary<string, bool> ChannelEnabled = new Dictionary<string, bool>();

        public static event Action<LogEntry> OnLog;

        public static void Info(string channel, string message) { Write(channel, message, LogLevel.Info); }
        public static void Warn(string channel, string message) { Write(channel, message, LogLevel.Warn); }
        public static void Error(string channel, string message) { Write(channel, message, LogLevel.Error); }

        public static void Write(string channel, string message, LogLevel level)
        {
            bool enabled;
            if (ChannelEnabled.TryGetValue(channel, out enabled) && !enabled) return;
            var entry = new LogEntry
            {
                channel = channel,
                message = message,
                level = level,
                atUnixMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond
            };
            RecentLogs.Add(entry);
            if (OnLog != null) OnLog(entry);

            var line = "[" + channel + "] " + message;
            if (level == LogLevel.Error) Debug.LogError(line);
            else if (level == LogLevel.Warn) Debug.LogWarning(line);
            else Debug.Log(line);
        }

        public static LogEntry[] Recent(int count = 0)
        {
            var all = RecentLogs.ToArray();
            if (count <= 0 || count >= all.Length) return all;
            var slice = new LogEntry[count];
            Array.Copy(all, all.Length - count, slice, 0, count);
            return slice;
        }

        public static void SetChannelEnabled(string channel, bool enabled) { ChannelEnabled[channel] = enabled; }
        public static void Clear() { RecentLogs.Clear(); ChannelEnabled.Clear(); }
    }

    /// <summary>SP-19 帧预算监控；默认 30 fps（33.3 ms），D 区可切 60 fps（16.7 ms）。</summary>
    public class FrameBudgetMonitor : MonoBehaviour
    {
        public static FrameBudgetMonitor Instance { get; private set; }

        public float BudgetMs = 33.3f;
        public int SampleCapacity = 120;
        public int WarnAfterFrames = 30;

        readonly object _sync = new object();
        RingBuffer<float> _samples;
        float _overCount;
        bool _warned;
        float _statsClock;

        public float AverageFrameMs { get; private set; }
        public float P95FrameMs { get; private set; }
        public float AverageFps { get; private set; }
        public bool IsOverBudget { get { return AverageFrameMs > BudgetMs; } }

        public event Action<float> BudgetExceeded;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _samples = new RingBuffer<float>(Mathf.Max(32, SampleCapacity));
        }

        void Update()
        {
            if (_samples == null) _samples = new RingBuffer<float>(Mathf.Max(32, SampleCapacity));
            var ms = Time.unscaledDeltaTime * 1000f;
            _samples.Add(ms);
            if (ms > BudgetMs) _overCount += 1f;
            else _overCount = 0f;
            if (_overCount >= WarnAfterFrames && !_warned)
            {
                _warned = true;
                LogRouter.Warn("FrameBudget", "连续 " + (int)_overCount + " 帧超预算 " + BudgetMs.ToString("0.0") + " ms");
                if (BudgetExceeded != null) BudgetExceeded(ms);
            }
            _statsClock += Time.unscaledDeltaTime;
            if (_statsClock >= 0.5f) { _statsClock = 0f; Recompute(); }
        }

        void Recompute()
        {
            var arr = _samples.ToArray();
            if (arr.Length == 0) return;
            var sum = 0f;
            for (var i = 0; i < arr.Length; i++) sum += arr[i];
            AverageFrameMs = sum / arr.Length;
            AverageFps = AverageFrameMs > 0.001f ? 1000f / AverageFrameMs : 0f;
            var sorted = (float[])arr.Clone();
            Array.Sort(sorted);
            var idx = Mathf.Clamp(Mathf.RoundToInt(sorted.Length * 0.95f) - 1, 0, sorted.Length - 1);
            P95FrameMs = sorted[idx];
            if (AverageFrameMs <= BudgetMs) _warned = false;
        }

        public void Reset()
        {
            if (_samples != null) _samples.Clear();
            _overCount = 0f; _warned = false;
            AverageFrameMs = 0f; P95FrameMs = 0f; AverageFps = 0f;
        }

        public string Snapshot()
        {
            return "fps=" + AverageFps.ToString("0.0") + " avg=" + AverageFrameMs.ToString("0.00") +
                   " p95=" + P95FrameMs.ToString("0.00") + " budget=" + BudgetMs.ToString("0.0");
        }
    }

    /// <summary>SP-20 模块心跳：每 N 秒写一次健康快照。</summary>
    public class HealthProbe : MonoBehaviour
    {
        public string moduleId = "UNKNOWN";
        public float interval = 5f;
        public string status = "模拟通过";
        public string state = "Standby";
        public string note;

        float _last;
        int _errors;
        string _lastError;

        void Update()
        {
            if (Time.unscaledTime - _last < interval) return;
            _last = Time.unscaledTime;
            var fps = Time.smoothDeltaTime > 0.0001f ? 1f / Time.smoothDeltaTime : 0f;
            HealthRegistry.Report(new ModuleHealth
            {
                moduleId = moduleId,
                status = status,
                state = state,
                fps = fps,
                frameMs = Time.smoothDeltaTime * 1000f,
                errors = _errors,
                lastError = _lastError,
                note = note
            });
        }

        public void SetStatus(string value) { status = value; }
        public void SetState(string value) { state = value; }

        public void ReportError(string message)
        {
            _errors++;
            _lastError = message;
            LogRouter.Error(moduleId, message);
        }
    }
}
