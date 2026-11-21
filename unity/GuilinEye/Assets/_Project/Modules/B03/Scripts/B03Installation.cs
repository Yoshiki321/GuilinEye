using System.Collections.Generic;
using UnityEngine;
using GuilinEye.Core;
using GuilinEye.Hardware;

namespace GuilinEye.Modules.B03
{
    /// <summary>B-03 石刻·官司：待机 294s（3×90+3×8）；对局 127s；1.2s 进/出溶解。</summary>
    public sealed class B03Installation : ModuleKit
    {
        bool _dueling;
        float _duelEnd;
        double _standbySeconds;
        string _team;
        readonly Dictionary<string, float> _holdStart = new Dictionary<string, float>();
        readonly Dictionary<string, float> _lastPoint = new Dictionary<string, float>();
        readonly HashSet<string> _triggered = new HashSet<string>();

        protected override void BuildModule()
        {
            AddLayer("石刻", new Color(0.48f, 0.44f, 0.36f), 1, 0.9f);
            AddLayer("对局", new Color(0.88f, 0.56f, 0.34f), 2, 0.0f);
            AddLayer("题名", new Color(0.94f, 0.86f, 0.66f), 3, 0.12f);
            if (Marker != null) Marker.Set(VenueStatus.Simulated);
            Debug.Log("[B03] 题名石 4450—4850；R2 覆盖至 4800，4800—4850 标现场待验证");
        }

        protected override void HandleSensor(InteractionConfig it, SensorEvent e)
        {
            if (e == null || it == null) return;
            var tid = string.IsNullOrEmpty(e.trackingId) ? "" : e.trackingId;
            var zone = string.IsNullOrEmpty(it.zone) ? e.zoneId : it.zone;
            var key = zone + "|" + tid;
            if (e.type != "point") return;
            if (it.hold <= 0f) { base.HandleSensor(it, e); return; }
            float last;
            if (_lastPoint.TryGetValue(key, out last) && Time.time - last > 0.45f)
            {
                _holdStart.Remove(key);
                _triggered.Remove(key);
            }
            _lastPoint[key] = Time.time;
            if (_triggered.Contains(key)) return;
            float start;
            if (!_holdStart.TryGetValue(key, out start)) { _holdStart[key] = Time.time; return; }
            if (Time.time - start < it.hold) return;
            _holdStart.Remove(key);
            _triggered.Add(key);
            HandleAction(it.action, e);
        }

        protected override void OnAction(string action, SensorEvent e)
        {
            switch (action)
            {
                case "duel.teamA": _team = "朝阳（朱砂）"; EnterDuel(); break;
                case "duel.teamB": _team = "水月（石青）"; EnterDuel(); break;
                case "enterDuel": EnterDuel(); break;
                case "titleStone":
                    Pulse("题名");
                    Log("T7 长按 1.5s：题名石逐字累积" + (_dueling ? "（对局中）" : "（需先开局）"));
                    break;
            }
        }

        void EnterDuel()
        {
            if (_dueling) return;
            _dueling = true;
            _standbySeconds = Clock.ShowSeconds;
            Clock.Configure(ConfigDatabase.GetShow("B03-DUEL"), 0);
            _duelEnd = Time.time + 127f;
            SetLayer("石刻", true, 0.35f);
            SetLayer("对局", true, 0.9f);
            if (Marker != null) Marker.Set(VenueStatus.FieldPending);
            Log("开局，1.2s 石刻文字淡出、肌理保留；队伍 " + (_team ?? "单人/范成大"));
        }

        protected override void OnSensorLeave(InteractionConfig it, SensorEvent e)
        {
            if (e == null) return;
            var zone = string.IsNullOrEmpty(e.zoneId) ? (it != null ? it.zone : "") : e.zoneId;
            if (string.IsNullOrEmpty(zone)) return;
            var key = zone + "|" + (string.IsNullOrEmpty(e.trackingId) ? "" : e.trackingId);
            _holdStart.Remove(key);
            _lastPoint.Remove(key);
            _triggered.Remove(key);
        }

        protected override void OnModuleTick(float showSeconds, float deltaSeconds)
        {
            if (!_dueling) return;
            if (Time.time < _duelEnd) return;
            _dueling = false;
            Clock.Configure(ConfigDatabase.GetShow("B03-STANDBY"), _standbySeconds);
            SetLayer("石刻", true, 0.9f);
            SetLayer("对局", false, 0f);
            if (Marker != null) Marker.Set(VenueStatus.Simulated);
            Log("对局结束，1.2s 复原，接当前幕下一相位");
        }
    }
}
