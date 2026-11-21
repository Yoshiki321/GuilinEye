using UnityEngine;
using GuilinEye.Core;

namespace GuilinEye.Modules.A02
{
    /// <summary>A-02 水月洞门：实体拱门（甲方）。洞口 2200×2000，中心距北缘 3100，弧线误差 ≤15mm。</summary>
    public sealed class A02Installation : ModuleKit
    {
        protected override void BuildModule()
        {
            AddLayer("拱", new Color(0.24f, 0.47f, 0.50f), 1, 0.9f);
            AddLayer("月", new Color(0.95f, 0.92f, 0.72f), 2, 0.35f);
            if (Marker != null) Marker.Set(VenueStatus.FieldPending);
            Debug.Log("[A02] 拱门弧线基准：弧顶 y2000、跨度 x2000—4200；误差 ≤15mm");
        }

        protected override void OnSegment(SegmentConfig segment, float local)
        {
            var autumn = segment != null && segment.id == "秋月";
            SetLayer("月", autumn, autumn ? 0.8f : 0.25f);
        }

        protected override void OnModuleTick(float showSeconds, float deltaSeconds)
        {
            var t = showSeconds % 720f;
            var inAutumn = t >= 360f && t < 450f;
            SetLayer("月", inAutumn, inAutumn ? 0.85f : 0.22f);
        }
    }
}
