using System;
using UnityEngine;
using GuilinEye.Core;

namespace GuilinEye.Modules.C04
{
    /// <summary>C-04 千人共绘：每 30min 的 0—12min 接管主体层；18 格；每日 500 幅；5min 收拢 8s。</summary>
    public sealed class C04Installation : ModuleKit
    {
        IDisposable _sub;
        int _works;
        bool _window;
        bool _initialized;

        protected override void OnConfigured()
        {
            base.OnConfigured();
            _sub = VenueBus.Subscribe("c04.work", e =>
            {
                _works++;
                if (_works > 500) { Log("超出每日 500 幅，仅存本地不投墙"); return; }
                Pulse("画格", 0.6f);
                Log("画作入 18 格，落款浮出；累计 " + _works + "/500");
            });
        }

        protected override void BuildModule()
        {
            AddLayer("长卷", new Color(0.45f, 0.37f, 0.28f), 1, 0.9f);
            AddLayer("画格", new Color(0.90f, 0.73f, 0.53f), 2, 0.15f);
        }

        protected override void OnModuleTick(float showSeconds, float deltaSeconds)
        {
            var t = showSeconds % 1800f;
            var open = t < 720f;
            if (!_initialized)
            {
                _initialized = true;
                _window = open;
                SetLayer("画格", open, open ? 0.9f : 0.12f);
                return;
            }
            if (open == _window) return;
            _window = open;
            SetLayer("画格", open, open ? 0.9f : 0.12f);
            Log(open ? "窗口开始：长卷格子接管主体层" : "窗口结束：回落祈愿，河灯回升");
        }

        protected override void OnDestroy()
        {
            if (_sub != null) _sub.Dispose();
            base.OnDestroy();
        }
    }
}
