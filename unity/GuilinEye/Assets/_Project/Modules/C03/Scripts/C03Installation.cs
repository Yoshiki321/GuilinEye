using System;
using UnityEngine;
using GuilinEye.Core;

namespace GuilinEye.Modules.C03
{
    /// <summary>C-03 祈愿河灯：40s 四步；并发 12 盏、第 13 排队；30min 集月、整点齐放。</summary>
    public sealed class C03Installation : ModuleKit
    {
        IDisposable _sub;
        int _active;
        int _total;
        int _lastSecond = -1;

        protected override void OnConfigured()
        {
            base.OnConfigured();
            _sub = VenueBus.Subscribe("c03.wish", e =>
            {
                var text = e.payload as string;
                _total++;
                if (_active >= 12)
                {
                    Log("第 " + _total + " 盏排队（并发 12 已满）：" + text);
                    return;
                }
                _active++;
                Pulse("河灯", 1.0f);
                Log("放灯：" + text + "；河灯自码头升起、漂向东侧，并发 " + _active + "/12");
            });
        }

        protected override void BuildModule()
        {
            AddLayer("河", new Color(0.18f, 0.34f, 0.45f), 1, 0.9f);
            AddLayer("河灯", new Color(0.98f, 0.78f, 0.42f), 2, 0.12f);
            AddLayer("月", new Color(0.92f, 0.90f, 0.72f), 3, 0.35f);
        }

        protected override void OnSegment(SegmentConfig segment, float local)
        {
            // schedule.json 的实际段名是 灯现/写愿/升起/汇入；“集月”是 30min 集月概念，不是段名。
            SetLayer("月", segment != null && segment.id == "汇入", 0.7f);
            SetLayer("河", true, 0.85f);
        }

        protected override void OnModuleTick(float showSeconds, float deltaSeconds)
        {
            var sec = (int)(showSeconds % 40f);
            if (_lastSecond == 39 && sec == 0) _active = 0;
            _lastSecond = sec;
        }

        protected override void OnDestroy()
        {
            if (_sub != null) _sub.Dispose();
            base.OnDestroy();
        }
    }
}
