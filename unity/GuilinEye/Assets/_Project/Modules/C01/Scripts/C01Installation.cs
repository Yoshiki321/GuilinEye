using UnityEngine;
using GuilinEye.Core;
using GuilinEye.Hardware;

namespace GuilinEye.Modules.C01
{
    /// <summary>C-01 灵象积分赛：110s（待机10+对局90+结算10）；S1 挥手赶象，摸到计分。</summary>
    public sealed class C01Installation : ModuleKit
    {
        int _score, _combo, _best;

        protected override void BuildModule()
        {
            AddLayer("灵象", new Color(0.85f, 0.59f, 0.45f), 1, 0.9f);
            AddLayer("积分", new Color(1f, 0.87f, 0.55f), 2, 0.12f);
        }

        protected override void HandleSensor(InteractionConfig it, SensorEvent e)
        {
            if (e == null || it == null) return;
            if (it.type != "gesture") return;
            // Femto 有人站立时每 100ms 发一次 point；只有真实 gesture/mock 才计分。
            if (e.type != "gesture" && e.type != "mock") return;
            if (Clock == null || Clock.Current == null || Clock.Current.id != "对局") return;
            HandleAction("driveElephant", e);
        }

        protected override void OnAction(string action, SensorEvent e)
        {
            if (action != "driveElephant") return;
            _score++;
            _combo++;
            _best = Mathf.Max(_best, _combo);
            Pulse("积分", 0.5f);
            Log("挥手赶象，得分 " + _score + "，连击 " + _combo);
            VenueBus.Publish("c01.score", Id, _score);
        }

        protected override void OnSegment(SegmentConfig segment, float local)
        {
            if (segment == null) return;
            if (segment.id == "结算")
            {
                SetLayer("积分", true, 1f);
                Log("结算：得分 " + _score + "，最高连击 " + _best + "，当日排名滚动");
                VenueBus.Publish("c01.result", Id, _score);
            }
            if (segment.id == "待机") { _score = 0; _combo = 0; SetLayer("积分", false, 0f); }
        }
    }
}
