using System.Collections.Generic;
using UnityEngine;
using GuilinEye.Core;
using GuilinEye.Hardware;

namespace GuilinEye.Modules.B02
{
    /// <summary>B-02 非遗市井：4min 六段；T4 五热点悬停 0.8s，名称牌 6s，再悬停播特写。</summary>
    public sealed class B02Installation : ModuleKit
    {
        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { "b02.hotspot.mifen", "桂林米粉" },
            { "b02.hotspot.guiju", "桂剧/彩调" },
            { "b02.hotspot.tuanshan", "桂林团扇" },
            { "b02.hotspot.youcha", "油茶" },
            { "b02.hotspot.tianqin", "天琴" }
        };
        readonly Dictionary<string, int> _hoverCount = new Dictionary<string, int>();
        readonly Dictionary<string, float> _hoverStart = new Dictionary<string, float>();
        readonly Dictionary<string, float> _lastPoint = new Dictionary<string, float>();
        readonly HashSet<string> _triggered = new HashSet<string>();
        float _cardHideAt = -1f;
        float _cardHold = 6f;

        protected override void BuildModule()
        {
            AddLayer("长卷", new Color(0.42f, 0.38f, 0.26f), 1, 0.9f);
            AddLayer("名称牌", new Color(0.96f, 0.83f, 0.52f), 2, 0.15f);
        }

        protected override void OnSegment(SegmentConfig segment, float local)
        {
            SetLayer("长卷", true, 0.75f + 0.08f * (SegmentIndex % 4));
        }

        protected override void HandleSensor(InteractionConfig it, SensorEvent e)
        {
            if (e == null || it == null) return;
            if (it.type != "hotspot") return;
            var zone = string.IsNullOrEmpty(e.zoneId) ? it.zone : e.zoneId;
            var tid = string.IsNullOrEmpty(e.trackingId) ? "" : e.trackingId;
            var key = zone + "|" + tid;
            var action = ActionForZone(zone);
            if (e.type != "point") return;
            if (string.IsNullOrEmpty(action)) return;

            // B02 契约字段是 hover，不是 hold；hold 仅作为缺省兜底。
            var hold = it.hover > 0f ? it.hover : (it.hold > 0f ? it.hold : 0.8f);
            _cardHold = it.cardHold > 0f ? it.cardHold : 6f;
            float last;
            if (_lastPoint.TryGetValue(key, out last) && Time.time - last > 0.45f)
            {
                _hoverStart.Remove(key);
                _triggered.Remove(key);
            }
            _lastPoint[key] = Time.time;
            if (_triggered.Contains(key)) return;
            float start;
            if (!_hoverStart.TryGetValue(key, out start)) { _hoverStart[key] = Time.time; return; }
            if (Time.time - start < hold) return;
            _hoverStart.Remove(key);
            _triggered.Add(key);
            HandleAction(action, e);
        }

        protected override void OnSensorLeave(InteractionConfig it, SensorEvent e)
        {
            if (e == null) return;
            var zone = string.IsNullOrEmpty(e.zoneId) ? (it != null ? it.zone : "") : e.zoneId;
            if (string.IsNullOrEmpty(zone)) return;
            var key = zone + "|" + (string.IsNullOrEmpty(e.trackingId) ? "" : e.trackingId);
            _hoverStart.Remove(key);
            _lastPoint.Remove(key);
            _triggered.Remove(key);
        }

        protected override void OnModuleTick(float showSeconds, float deltaSeconds)
        {
            if (_cardHideAt > 0f && Time.time >= _cardHideAt)
            {
                _cardHideAt = -1f;
                SetLayer("名称牌", false, 0.18f);
                Log("名称牌停留 " + _cardHold + "s 后收为小标");
            }
        }

        static string ActionForZone(string zone)
        {
            switch (zone)
            {
                case "T4-MIFEN": return "b02.hotspot.mifen";
                case "T4-GUIJU": return "b02.hotspot.guiju";
                case "T4-TUANSHAN": return "b02.hotspot.tuanshan";
                case "T4-YOUCHA": return "b02.hotspot.youcha";
                case "T4-TIANQIN": return "b02.hotspot.tianqin";
                default: return null;
            }
        }

        protected override void OnAction(string action, SensorEvent e)
        {
            if (!Names.ContainsKey(action)) return;
            int n;
            _hoverCount.TryGetValue(action, out n);
            _hoverCount[action] = ++n;
            if (n % 2 == 1)
            {
                SetLayer("名称牌", true, 1f);
                Pulse("名称牌", 1.2f);
                _cardHideAt = Time.time + _cardHold;
                Log("弹出名称牌：" + Names[action] + "（列入年份/级别/一句简介），停 " + _cardHold + "s 收为小标");
            }
            else
            {
                Pulse("长卷", 1.0f);
                Log("同点位再次悬停：播 8—15s 动作特写，播毕回落长卷");
            }
        }
    }
}
