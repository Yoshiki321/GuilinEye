using System.Collections.Generic;
using UnityEngine;
using GuilinEye.Core;
using GuilinEye.Hardware;

namespace GuilinEye.Modules.D04
{
    /// <summary>D-04 山生：180s（50/60/40/30）；脚步划开江纹留发光水线，10s 合拢；九块同踩象身全亮。</summary>
    public sealed class D04Installation : ModuleKit
    {
        readonly Dictionary<string, float> _lines = new Dictionary<string, float>();
        float _lastAll = -999f;

        protected override void BuildModule()
        {
            AddLayer("江面", new Color(0.13f, 0.35f, 0.45f), 1, 0.9f);
            AddLayer("水线", new Color(0.72f, 0.90f, 0.92f), 2, 0.08f);
        }

        protected override void HandleSensor(InteractionConfig it, SensorEvent e)
        {
            // 无定位时：presence 视为进入某一块地面；count>=3 视为多人同踩，走 allNine 近似。
            if (it.type != "floorSwipe" && e.type != "presence" && e.type != "count") return;
            HandleAction("swipe", e);
        }

        protected override void OnAction(string action, SensorEvent e)
        {
            if (action != "swipe" || e == null || string.IsNullOrEmpty(e.zoneId)) return;
            _lines[e.zoneId] = Time.time;
            Pulse("水线", 0.7f);
            if (e.count >= 3)
            {
                Pulse("水线", 1.5f);
                Log("红外 " + e.count + " 人：" + e.zoneId + " 九块同踩近似触发，象身光粒全亮");
            }
            else
            {
                Log(e.zoneId + " 划开江纹：留发光水线，花苞顺水线升起至 400mm；10s 合拢");
            }
        }

        protected override void OnModuleTick(float showSeconds, float deltaSeconds)
        {
            var recent = 0;
            foreach (var kv in _lines)
                if (Time.time - kv.Value <= 10f) recent++;
            if (recent >= 9 && Time.time - _lastAll > 5f)
            {
                _lastAll = Time.time;
                Log("九块同踩：江面完整、象身光粒全亮");
            }
        }
    }
}
