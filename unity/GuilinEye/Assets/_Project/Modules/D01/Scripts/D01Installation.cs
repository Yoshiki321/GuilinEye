using UnityEngine;
using GuilinEye.Core;
using GuilinEye.Hardware;

namespace GuilinEye.Modules.D01
{
    /// <summary>D-01 四季：120s（30慢推/60春转夏/30夏转秋+定格10s）；地面九块踩踏涟漪 ≤0.3s。</summary>
    public sealed class D01Installation : ModuleKit
    {
        int _steps;
        int _season = -1;

        protected override void BuildModule()
        {
            AddLayer("春", new Color(0.36f, 0.55f, 0.42f), 1, 0.9f);
            AddLayer("夏", new Color(0.22f, 0.48f, 0.40f), 2, 0.2f);
            AddLayer("秋", new Color(0.72f, 0.53f, 0.28f), 3, 0.2f);
            AddLayer("冬", new Color(0.62f, 0.68f, 0.70f), 4, 0.2f);
            AddLayer("涟漪", new Color(1f, 1f, 1f), 5, 0.08f);
        }

        protected override void OnSegment(SegmentConfig segment, float local)
        {
            if (segment == null) return;
            if (segment.id == "D01") { if (local < 1f) { _season = -1; _steps = 0; } ApplySeason(local); }
            Log("四季段 " + segment.id + " @" + local);
        }

        protected override void OnModuleTick(float showSeconds, float deltaSeconds)
        {
            if (Clock != null && Clock.Current != null && Clock.Current.id == "D01")
                ApplySeason(Clock.SegmentLocal);
        }

        void ApplySeason(float local)
        {
            // D01 的段名是 D01（120s），不是春/夏/秋/冬；按段内时间驱动四季层。
            var t = Mathf.Max(0f, local);
            var idx = t < 30f ? 0 : (t < 90f ? 1 : 2);
            if (idx == _season) return;
            _season = idx;
            SetLayer("春", idx == 0, idx == 0 ? 0.9f : 0.15f);
            SetLayer("夏", idx == 1, idx == 1 ? 0.9f : 0.15f);
            SetLayer("秋", idx == 2, idx == 2 ? 0.9f : 0.15f);
            SetLayer("冬", false, 0.1f);
            Log("四季切换：春/夏/秋=" + (idx == 0) + "/" + (idx == 1) + "/" + (idx == 2));
        }

        protected override void HandleSensor(InteractionConfig it, SensorEvent e)
        {
            // 红外基线只给 presence/count；没有定位时把“有人进入 F 区”降级为一次踩踏触发。
            if (it.type != "floorStep" && e.type != "presence" && e.type != "count") return;
            HandleAction("step", e);
        }

        protected override void OnAction(string action, SensorEvent e)
        {
            if (action != "step") return;
            _steps++;
            Pulse("涟漪", 0.4f);
            Log("踩踏 " + (e != null ? e.zoneId : "-") + "：同心涟漪 ≤0.3s，2s 消散（累计 " + _steps + "）");
        }
    }
}
