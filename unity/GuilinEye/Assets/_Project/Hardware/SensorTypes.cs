using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuilinEye.Hardware
{
    [Serializable]
    public class SensorEvent
    {
        public string sensorId;
        public string type;      // presence / enter / leave / point / touch / count
        public string zoneId;
        public float x, y;       // 画布 mm 坐标；无定位能力时为 0
        public float confidence = 1f;
        public string trackingId;
        public long timestampMs;
        public bool hasPosition;
        public int count;        // 仅 IR count 事件使用；其它事件为 0
    }

    [Serializable]
    public class SensorCapabilities
    {
        public bool presence, region, point2d, tracking, skeleton, gesture, count;
    }

    public interface ISensorProvider
    {
        string SensorId { get; }
        SensorCapabilities Capabilities { get; }
        bool Online { get; }
        void StartProvider(Action<SensorEvent> emit);
        void StopProvider();
    }

    public static class SensorCapabilityMap
    {
        public static SensorCapabilities FromStrings(string[] supports)
        {
            var c = new SensorCapabilities();
            if (supports == null) return c;
            foreach (var s in supports)
            {
                switch (s)
                {
                    case "presence": c.presence = true; break;
                    case "region": c.region = true; break;
                    case "point2d": c.point2d = true; break;
                    case "tracking": c.tracking = true; break;
                    case "skeleton": c.skeleton = true; break;
                    case "gesture": c.gesture = true; break;
                    case "count": c.count = true; break;
                }
            }
            return c;
        }
    }
}
