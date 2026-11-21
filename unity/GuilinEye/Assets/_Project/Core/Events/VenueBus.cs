using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuilinEye.Core
{
    public class VenueEvent
    {
        public string type;
        public string source;
        public object payload;
        public DateTime occurredAtUtc = DateTime.UtcNow;
        public long sequence;
    }

    /// <summary>本机事件总线。跨进程由 venue-api 网关转发，不承诺丢失重放。</summary>
    public static class VenueBus
    {
        static readonly Dictionary<string, List<Action<VenueEvent>>> Handlers = new Dictionary<string, List<Action<VenueEvent>>>();
        static long _seq;

        public static void Publish(string type, string source, object payload = null)
        {
            var e = new VenueEvent { type = type, source = source, payload = payload, sequence = ++_seq };
            List<Action<VenueEvent>> list;
            if (!Handlers.TryGetValue(type, out list) || list == null) return;
            for (var i = 0; i < list.Count; i++)
            {
                try { list[i](e); }
                catch (Exception ex) { Debug.LogError("[Bus] " + type + " handler: " + ex.Message); }
            }
        }

        public static IDisposable Subscribe(string type, Action<VenueEvent> handler)
        {
            List<Action<VenueEvent>> list;
            if (!Handlers.TryGetValue(type, out list)) { list = new List<Action<VenueEvent>>(); Handlers[type] = list; }
            list.Add(handler);
            return new Token(type, handler);
        }

        class Token : IDisposable
        {
            readonly string _type;
            readonly Action<VenueEvent> _handler;
            public Token(string t, Action<VenueEvent> h) { _type = t; _handler = h; }
            public void Dispose()
            {
                List<Action<VenueEvent>> list;
                if (Handlers.TryGetValue(_type, out list)) list.Remove(_handler);
            }
        }
    }
}
