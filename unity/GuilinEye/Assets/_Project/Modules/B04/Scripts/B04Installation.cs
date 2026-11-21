using System;
using System.Collections.Generic;
using UnityEngine;
using GuilinEye.Core;

namespace GuilinEye.Modules.B04
{
    /// <summary>B-04 诗词飞花：一期 120s 过渡；二期答题落字，30s 风化，残影 ≤12。</summary>
    public sealed class B04Installation : ModuleKit
    {
        IDisposable _sub;
        readonly List<string> _residual = new List<string>();
        string _lastChar;

        protected override void OnConfigured()
        {
            base.OnConfigured();
            _sub = VenueBus.Subscribe("b04.dropChar", e =>
            {
                var payload = e.payload as string;
                if (string.IsNullOrEmpty(payload)) return;
                _lastChar = payload;
                _residual.Add(payload);
                while (_residual.Count > 12) _residual.RemoveAt(0);
                Pulse("落字");
                Log("落字「" + payload + "」，30s 风化；残影 " + _residual.Count + "/12");
            });
        }

        protected override void BuildModule()
        {
            AddLayer("诗壁", new Color(0.36f, 0.35f, 0.28f), 1, 0.9f);
            AddLayer("落字", new Color(0.98f, 0.93f, 0.78f), 2, 0.10f);
        }

        protected override void OnSegment(SegmentConfig segment, float local)
        {
            SetLayer("诗壁", true, 0.8f);
        }

        protected override void OnDestroy()
        {
            if (_sub != null) _sub.Dispose();
            base.OnDestroy();
        }
    }
}
