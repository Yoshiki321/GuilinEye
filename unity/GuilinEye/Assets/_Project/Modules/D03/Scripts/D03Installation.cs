using System.Collections.Generic;
using UnityEngine;
using GuilinEye.Core;
using GuilinEye.Hardware;

namespace GuilinEye.Modules.D03
{
    /// <summary>D-03 月升：180s；满月 4200；亮度 60%；踩碎月影；三人同踩 8s 内升 1/3；静止 20s。</summary>
    public sealed class D03Installation : ModuleKit
    {
        readonly Dictionary<string, float> _feet = new Dictionary<string, float>();
        bool _boosted;

        protected override void BuildModule()
        {
            AddLayer("月", new Color(0.94f, 0.90f, 0.73f), 1, 0.62f);
            AddLayer("月影", new Color(0.80f, 0.82f, 0.78f), 2, 0.08f);
        }

        protected override void HandleSensor(InteractionConfig it, SensorEvent e)
        {
            if (it.type != "floorStep" && e.type != "presence" && e.type != "count") return;
            HandleAction("step", e);
        }

        protected override void OnAction(string action, SensorEvent e)
        {
            if (action != "step" || e == null || string.IsNullOrEmpty(e.zoneId)) return;
            _feet[e.zoneId] = Time.time;
            Pulse("月影", 0.35f);
            Log(e.zoneId + " 踩下：月影碎为涟漪；抬脚又合");

            var together = 0;
            foreach (var z in _feet.Keys)
                if (Time.time - _feet[z] < 2f) together++;
            if (!_boosted && e.count >= 3) together = Mathf.Max(together, 3);
            if (together >= 3 && !_boosted)
            {
                _boosted = true;
                Log("三人同踩：月升加速，8s 内上升 1/3");
            }
        }

        protected override void OnSegment(SegmentConfig segment, float local)
        {
            _boosted = false;
            _feet.Clear();
            SetLayer("月", true, 0.6f);
        }
    }
}
