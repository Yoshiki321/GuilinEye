using System;
using UnityEngine;
using GuilinEye.Core;
using GuilinEye.Hardware;
using GuilinEye.Shared;

namespace GuilinEye.Modules.A03
{
    /// <summary>A-03 古人招手：四段 15s / 60s 循环；S3 触发立即转作揖，结束后回原相位。</summary>
    public sealed class A03Installation : ModuleKit
    {
        IDisposable _greetSub;
        float _interruptUntil = -1f;

        protected override void OnConfigured()
        {
            base.OnConfigured();
            _greetSub = VenueBus.Subscribe("a03.greet", e => HandleAction("greet", null));
        }

        protected override void BuildModule()
        {
            AddLayer("古人", new Color(0.34f, 0.56f, 0.43f), 1, 0.9f);
            AddLayer("作揖", new Color(1f, 0.92f, 0.72f), 2, 0.18f);
        }

        protected override void OnAction(string action, SensorEvent e)
        {
            if (action != "greet") return;
            if (Time.time < _interruptUntil) return;
            _interruptUntil = Time.time + 15f;
            SetState(ModuleState.Interrupted);
            Pulse("作揖", 1.2f);
            Log("插播作揖迎客 15s，动作结束回 60s 循环相位");
        }

        protected override void OnModuleTick(float showSeconds, float deltaSeconds)
        {
            if (_interruptUntil > 0f && Time.time > _interruptUntil)
            {
                _interruptUntil = -1f;
                SetState(ModuleState.Active);
                Log("插播结束，回到循环相位");
            }
        }

        protected override void OnDestroy()
        {
            if (_greetSub != null) _greetSub.Dispose();
            base.OnDestroy();
        }
    }
}
