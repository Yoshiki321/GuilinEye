using UnityEngine;
using GuilinEye.Core;
using GuilinEye.Hardware;

namespace GuilinEye.Modules.A01
{
    /// <summary>A-01 石象·四时八候：8×90s 时间轴；S3 门口 3m 迎客，90s 不重复。</summary>
    public sealed class A01Installation : ModuleKit
    {
        int _greets;

        protected override void BuildModule()
        {
            AddLayer("象", new Color(0.20f, 0.55f, 0.56f), 1, 0.92f);
            AddLayer("候", new Color(0.82f, 0.74f, 0.48f), 2, 0.55f);
            AddLayer("迎客", new Color(1f, 1f, 1f), 4, 0.12f);
        }

        protected override void OnSegment(SegmentConfig segment, float local)
        {
            SetLayer("候", true, 0.35f + 0.15f * (SegmentIndex % 3));
            Log("候：" + segment.id + " local=" + local);
        }

        protected override void OnAction(string action, SensorEvent e)
        {
            if (action != "greet") return;
            _greets++;
            Pulse("迎客");
            Log("迎客作揖 #" + _greets + "，0.5s 响应");
            VenueBus.Publish("a03.greet", "A01", null);
        }

        protected override float CooldownFor(string action)
        {
            return action == "greet" ? 90f : 0f;
        }
    }
}
