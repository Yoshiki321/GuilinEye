using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuilinEye.Shared
{
    /// <summary>模块统一状态。禁止各项目自造状态字符串。</summary>
    public enum ModuleState
    {
        Uninitialized,
        Initializing,
        Standby,
        Active,
        Interrupted,
        Degraded,
        Fault,
        Disposed
    }

    /// <summary>SP-02 轻量状态机；只允许表中转换，Fault 可 Reset 回 Standby。</summary>
    public sealed class ModuleStateMachine
    {
        static readonly Dictionary<ModuleState, ModuleState[]> Allowed = new Dictionary<ModuleState, ModuleState[]>
        {
            { ModuleState.Uninitialized, new[] { ModuleState.Initializing, ModuleState.Disposed } },
            { ModuleState.Initializing, new[] { ModuleState.Standby, ModuleState.Fault, ModuleState.Disposed } },
            { ModuleState.Standby, new[] { ModuleState.Active, ModuleState.Fault, ModuleState.Disposed } },
            { ModuleState.Active, new[] { ModuleState.Standby, ModuleState.Interrupted, ModuleState.Degraded, ModuleState.Fault, ModuleState.Disposed } },
            { ModuleState.Interrupted, new[] { ModuleState.Active, ModuleState.Standby, ModuleState.Fault, ModuleState.Disposed } },
            { ModuleState.Degraded, new[] { ModuleState.Active, ModuleState.Standby, ModuleState.Fault, ModuleState.Disposed } },
            { ModuleState.Fault, new[] { ModuleState.Standby, ModuleState.Disposed } },
            { ModuleState.Disposed, new ModuleState[0] }
        };

        public readonly string ModuleId;
        public ModuleState State { get; private set; } = ModuleState.Uninitialized;
        public DateTime EnteredAtUtc { get; private set; } = DateTime.UtcNow;
        public event Action<ModuleState, ModuleState> Changed;

        public ModuleStateMachine(string moduleId) { ModuleId = moduleId; }

        public bool CanTransition(ModuleState to)
        {
            var list = Allowed[State];
            for (var i = 0; i < list.Length; i++) if (list[i] == to) return true;
            return false;
        }

        public bool TryTransition(ModuleState to)
        {
            if (!CanTransition(to)) return false;
            Force(to);
            return true;
        }

        public void Force(ModuleState to)
        {
            var from = State;
            State = to;
            EnteredAtUtc = DateTime.UtcNow;
            if (Changed != null) Changed(from, to);
        }

        public bool Reset()
        {
            if (State != ModuleState.Fault && State != ModuleState.Degraded) return false;
            Force(ModuleState.Standby);
            return true;
        }
    }

    /// <summary>SP-01 极轻量服务定位；模块不靠 FindObjectOfType 拿全局服务。</summary>
    public sealed class ServiceRegistry
    {
        readonly Dictionary<Type, object> _map = new Dictionary<Type, object>();

        public void Register<T>(T service) { _map[typeof(T)] = service; }

        public void RegisterInstance<T>(object service)
        {
            if (service is T) _map[typeof(T)] = service;
            else throw new ArgumentException(service == null ? "service is null" : service.GetType().Name + " 不是 " + typeof(T).Name);
        }

        public T Get<T>()
        {
            object v;
            if (!_map.TryGetValue(typeof(T), out v) || v == null)
                throw new InvalidOperationException("服务未注册：" + typeof(T).Name);
            return (T)v;
        }

        public bool TryGet<T>(out T service)
        {
            object v;
            if (_map.TryGetValue(typeof(T), out v) && v != null) { service = (T)v; return true; }
            service = default(T);
            return false;
        }

        public bool Contains<T>() { return _map.ContainsKey(typeof(T)); }
        public void Remove<T>() { _map.Remove(typeof(T)); }
        public void Clear() { _map.Clear(); }
        public int Count { get { return _map.Count; } }
    }

    [Serializable]
    public class ModuleHealth
    {
        public string moduleId;
        public string status = "模拟通过";
        public string state = "Standby";
        public float fps;
        public float frameMs;
        public int errors;
        public string lastError;
        public long updatedAtUnixMs;
        public string note;
    }

    [Serializable]
    public class HealthSnapshot
    {
        public string version = "1.0";
        public ModuleHealth[] modules;
    }

    /// <summary>SP-20 健康注册表；写 JSON 快照给诊断面板/OpsBridge。</summary>
    public static class HealthRegistry
    {
        static readonly Dictionary<string, ModuleHealth> Map = new Dictionary<string, ModuleHealth>();

        public static void Report(ModuleHealth health)
        {
            if (health == null || string.IsNullOrEmpty(health.moduleId)) return;
            if (health.updatedAtUnixMs == 0) health.updatedAtUnixMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            Map[health.moduleId] = health;
        }

        public static ModuleHealth Get(string moduleId)
        {
            ModuleHealth h = null;
            return moduleId != null && Map.TryGetValue(moduleId, out h) ? h : null;
        }

        public static ModuleHealth[] All()
        {
            var list = new List<ModuleHealth>(Map.Values);
            return list.ToArray();
        }

        public static string SnapshotJson()
        {
            return JsonUtility.ToJson(new HealthSnapshot { modules = All() }, true);
        }

        public static void Clear() { Map.Clear(); }
    }
}
