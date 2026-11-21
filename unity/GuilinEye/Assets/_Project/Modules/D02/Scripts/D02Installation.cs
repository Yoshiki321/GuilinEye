using UnityEngine;
using GuilinEye.Core;
using GuilinEye.Hardware;

namespace GuilinEye.Modules.D02
{
    /// <summary>D-02 花境：180s（40/80/40/20）；脚步生花扩散 600mm；跳跃花爆 2s 重聚。</summary>
    public sealed class D02Installation : ModuleKit
    {
        int _bursts;

        protected override void BuildModule()
        {
            AddLayer("花境", new Color(0.68f, 0.42f, 0.52f), 1, 0.9f);
            AddLayer("生花", new Color(0.96f, 0.72f, 0.68f), 2, 0.08f);
        }

        protected override void OnSegment(SegmentConfig segment, float local)
        {
            if (segment != null && segment.id == "D02" && local < 1f) _bursts = 0;
            SetLayer("花境", true, 0.78f + 0.12f * (SegmentIndex % 3));
        }

        protected override void HandleSensor(InteractionConfig it, SensorEvent e)
        {
            // 无定位时：presence 抬脚一次；count>=3 视为多人同时踩踏，触发花爆。
            if (it.type != "floorStep" && e.type != "presence" && e.type != "count") return;
            // 单人/普通 presence 走“脚步生花”；只有多人同踩且配置允许时才花爆。
            var jump = e.type == "presence" && e.count >= 3 && it.jumpBurst;
            HandleAction(jump ? "jump" : "step", e);
        }

        protected override void OnAction(string action, SensorEvent e)
        {
            if (action == "jump")
            {
                _bursts++;
                Pulse("生花", 1.4f);
                Log((e != null ? e.zoneId : "-") + " 跳跃：花爆为上升光点，2s 重聚（第 " + _bursts + " 次）");
            }
            else if (action == "step")
            {
                Pulse("生花", 0.5f);
                Log((e != null ? e.zoneId : "-") + " 脚步生花：以踩点为中心扩散 600mm");
            }
        }
    }
}
