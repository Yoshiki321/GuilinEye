using UnityEngine;

namespace GuilinEye.Hardware
{
    /// <summary>调试用键盘模拟：1 A01/S3 2 B01-T1 3 B02 米粉 4 B03 书写 5 D01 脚步。</summary>
    public class MockInput : MonoBehaviour
    {
        void Update()
        {
            if (SensorHub.Instance == null) return;
            var mock = SensorHub.Instance.Get("MOCK") as MockSensorProvider;
            if (mock == null) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) mock.Emit("S3-ENTRY", "presence");
            if (Input.GetKeyDown(KeyCode.Alpha2)) mock.Emit("T1", "touch", 3750, 1500);
            if (Input.GetKeyDown(KeyCode.Alpha3)) mock.Emit("T4-MIFEN", "point", 6020, 1500);
            if (Input.GetKeyDown(KeyCode.Alpha4)) mock.Emit("T5", "point", 2400, 1500);
            if (Input.GetKeyDown(KeyCode.Alpha5)) mock.Emit("F5", "presence");
        }
    }
}
