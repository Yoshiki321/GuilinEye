using System;
using UnityEngine;

namespace GuilinEye.Hardware
{
    /// <summary>Mock 传感器：键盘与脚本事件。未接硬件时默认启用。</summary>
    public class MockSensorProvider : ISensorProvider
    {
        Action<SensorEvent> _emit;
        public string SensorId { get { return "MOCK"; } }
        public SensorCapabilities Capabilities { get { return SensorCapabilityMap.FromStrings(new[] { "presence", "region", "point2d", "tracking" }); } }
        public bool Online { get { return true; } }

        public void StartProvider(Action<SensorEvent> emit)
        {
            _emit = emit;
            Debug.Log("[Mock] 在线。按键：1=A01迎客 2=B01-T1 3=B02米粉 4=B03书写 5=D01脚步");
        }

        public void StopProvider() { _emit = null; }

        public void Emit(string zone, string type = "touch", float x = 0, float y = 0)
        {
            if (_emit == null) return;
            _emit(new SensorEvent
            {
                sensorId = "MOCK", type = type, zoneId = zone, x = x, y = y,
                hasPosition = true, confidence = 1f,
                timestampMs = (long)(Time.realtimeSinceStartup * 1000)
            });
        }
    }
}
