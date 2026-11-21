using System;
using UnityEngine;
using GuilinEye.Core;

namespace GuilinEye.Modules.C05
{
    /// <summary>C-05 文创取件：60s 四步；印章位 900×900 三态常驻；4 类商品一一对应。</summary>
    public sealed class C05Installation : ModuleKit
    {
        IDisposable _sub;
        string _lastCode;

        protected override void OnConfigured()
        {
            base.OnConfigured();
            _sub = VenueBus.Subscribe("c05.order", e =>
            {
                var code = e.payload as string;
                _lastCode = code;
                Pulse("印章", 1.2f);
                Log("下单成功：纹样缩为印章停留 60s，取货码 " + code);
            });
        }

        protected override void BuildModule()
        {
            AddLayer("纹样", new Color(0.62f, 0.34f, 0.30f), 1, 0.9f);
            AddLayer("印章", new Color(0.96f, 0.82f, 0.55f), 2, 0.15f);
        }

        protected override void OnSegment(SegmentConfig segment, float local)
        {
            // 实际段名 选纹样/选色号/确认/成印；印章只在“成印”段常驻。
            SetLayer("印章", segment != null && segment.id == "成印", 0.9f);
        }

        protected override void OnDestroy()
        {
            if (_sub != null) _sub.Dispose();
            base.OnDestroy();
        }
    }
}
