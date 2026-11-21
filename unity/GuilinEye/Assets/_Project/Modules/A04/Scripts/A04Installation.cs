using UnityEngine;
using GuilinEye.Core;
using GuilinEye.Hardware;

namespace GuilinEye.Modules.A04
{
    /// <summary>A-04 古人入卷：20s/3min；R1 检测到人时行至其侧后方 1.5m 招手一次。</summary>
    public sealed class A04Installation : ModuleKit
    {
        int _runs;

        protected override void BuildModule()
        {
            AddLayer("卷", new Color(0.42f, 0.55f, 0.40f), 1, 0.85f);
            AddLayer("入卷", new Color(1f, 0.95f, 0.75f), 2, 0.12f);
        }

        protected override void OnSegment(SegmentConfig segment, float local)
        {
            if (segment != null && segment.id == "行走") _runs++;
            Log("卷轴段：" + (segment != null ? segment.id : "-"));
        }

        protected override void HandleSensor(InteractionConfig it, SensorEvent e)
        {
            // A04 由 T1 进入点触发；只接受 point/presence，不让 leave 再触发一次。
            if (e.type != "point" && e.type != "presence") return;
            base.HandleSensor(it, e);
        }

        protected override void OnAction(string action, SensorEvent e)
        {
            if (action != "greetAt15m" && action != "a04.presence") return;
            Pulse("入卷", 1.0f);
            Log("R1 存在检测：行至侧后方 1.5m 招手一次后隐入石壁（第 " + _runs + " 轮）");
        }
    }
}
