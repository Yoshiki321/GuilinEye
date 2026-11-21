using UnityEngine;
using GuilinEye.Core;
using GuilinEye.Hardware;

namespace GuilinEye.Modules.C02
{
    /// <summary>C-02 山河三关：120s（5/40/40/30/5）；拨云、划江、唤雨。</summary>
    public sealed class C02Installation : ModuleKit
    {
        int _stage;

        protected override void BuildModule()
        {
            AddLayer("云", new Color(0.58f, 0.66f, 0.72f), 1, 0.8f);
            AddLayer("江", new Color(0.28f, 0.51f, 0.60f), 2, 0.8f);
            AddLayer("雨", new Color(0.72f, 0.78f, 0.86f), 3, 0.2f);
        }

        protected override void OnSegment(SegmentConfig segment, float local)
        {
            if (segment == null) return;
            switch (segment.id)
            {
                case "拨云": _stage = 1; break;
                case "划江": _stage = 2; break;
                case "唤雨": _stage = 3; break;
                case "结算": _stage = 0; break;
            }
            SetLayer("云", _stage == 1, _stage == 1 ? 0.9f : 0.2f);
            SetLayer("江", _stage == 2, _stage == 2 ? 0.9f : 0.2f);
            SetLayer("雨", _stage == 3, _stage == 3 ? 0.9f : 0.15f);
            Log("进入第 " + _stage + " 关");
        }

        protected override void HandleSensor(InteractionConfig it, SensorEvent e)
        {
            if (e == null || it == null) return;
            if (it.type != "gesture") return;
            // 忽略 FemtoBoltProvider 的 10Hz point，避免站着不动也过关。
            if (e.type != "gesture" && e.type != "mock") return;
            if (_stage <= 0) return;
            HandleAction("gesture." + _stage, e);
        }

        protected override void OnAction(string action, SensorEvent e)
        {
            switch (action)
            {
                case "gesture.1": Pulse("云", 0.8f); Log("第 1 关：挥臂拨云"); break;
                case "gesture.2": Pulse("江", 0.8f); Log("第 2 关：横向划江找石"); break;
                case "gesture.3": Pulse("雨", 0.8f); Log("第 3 关：双手上举唤雨"); break;
            }
        }
    }
}
