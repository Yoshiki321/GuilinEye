using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuilinEye.Shared
{
    public class Signal
    {
        public string type;
        public string source;
        public object payload;
        public long atUnixMs;
        public long sequence;
    }

    /// <summary>SP-05 共享事件总线；与 L1 的 VenueBus 可通过桥接器互转。</summary>
    public static class SignalBus
    {
        static readonly Dictionary<string, List<Action<Signal>>> Handlers = new Dictionary<string, List<Action<Signal>>>();
        static long _sequence;

        public static void Publish(string type, string source, object payload = null)
        {
            var signal = new Signal
            {
                type = type,
                source = source,
                payload = payload,
                atUnixMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond,
                sequence = ++_sequence
            };
            List<Action<Signal>> list;
            if (!Handlers.TryGetValue(type, out list) || list == null) return;
            for (var i = 0; i < list.Count; i++)
            {
                try { list[i](signal); }
                catch (Exception e) { Debug.LogError("[SignalBus] " + type + " handler: " + e.Message); }
            }
        }

        public static IDisposable Subscribe(string type, Action<Signal> handler)
        {
            List<Action<Signal>> list;
            if (!Handlers.TryGetValue(type, out list)) { list = new List<Action<Signal>>(); Handlers[type] = list; }
            list.Add(handler);
            return new Token(type, handler);
        }

        public static void Clear()
        {
            Handlers.Clear();
            _sequence = 0;
        }

        public static int HandlerCount(string type)
        {
            List<Action<Signal>> list;
            return Handlers.TryGetValue(type, out list) && list != null ? list.Count : 0;
        }

        sealed class Token : IDisposable
        {
            readonly string _type;
            readonly Action<Signal> _handler;
            public Token(string type, Action<Signal> handler) { _type = type; _handler = handler; }
            public void Dispose()
            {
                List<Action<Signal>> list;
                if (Handlers.TryGetValue(_type, out list) && list != null) list.Remove(_handler);
            }
        }
    }
}
