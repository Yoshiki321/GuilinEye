using UnityEngine;
using GuilinEye.Core;
using GuilinEye.Hardware;

namespace GuilinEye.Modules.D05
{
    /// <summary>D-05 群蝶：60s（20/30/8/2）；蝶道 3000；踩落花起蝶 3—5 只，最多 3 环；15,000/屏。</summary>
    public sealed class D05Installation : ModuleKit
    {
        int _rings;

        protected override void BuildModule()
        {
            AddLayer("蝶道", new Color(0.70f, 0.55f, 0.72f), 1, 0.9f);
            AddLayer("起蝶", new Color(0.98f, 0.90f, 0.64f), 2, 0.10f);
        }

        protected override void HandleSensor(InteractionConfig it, SensorEvent e)
        {
            if (it.type != "floorStep" && e.type != "presence" && e.type != "count") return;
            HandleAction("step", e);
        }

        protected override void OnAction(string action, SensorEvent e)
        {
            if (action != "step") return;
            if (_rings >= 3)
            {
                Log((e != null ? e.zoneId : "-") + " 蝶环已满 3 环，不再叠加");
                return;
            }
            _rings++;
            Pulse("起蝶", 1.0f);
            Log((e != null ? e.zoneId : "-") + " 踩到落花：3—5 只蝶自脚边升起，绕人成环（" + _rings + "/3）");
        }

        protected override void OnSegment(SegmentConfig segment, float local)
        {
            _rings = 0;
            SetLayer("蝶道", true, 0.8f);
        }
    }
}
