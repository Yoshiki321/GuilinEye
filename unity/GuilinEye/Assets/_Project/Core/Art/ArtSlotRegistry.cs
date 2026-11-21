using System.Collections.Generic;
using UnityEngine;

namespace GuilinEye.Core
{
    /// <summary>91 个画面元素 → 模块 slot。缺素材显示占位并记录，不静默消失。</summary>
    public class ArtSlotRegistry : MonoBehaviour
    {
        readonly Dictionary<string, Transform> _slots = new Dictionary<string, Transform>();
        readonly List<string> _missing = new List<string>();

        public int Count { get { return _slots.Count; } }
        public IReadOnlyList<string> Missing { get { return _missing; } }

        public void Initialize(ModuleConfig module)
        {
            if (module == null || ConfigDatabase.Art == null) return;
            foreach (var a in ConfigDatabase.Art.elements)
            {
                if (a.module != module.id) continue;
                var go = new GameObject("Slot_" + a.elementId + "_" + a.slot);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                _slots[a.elementId] = go.transform;
                if (string.IsNullOrEmpty(a.asset)) _missing.Add(a.elementId);
            }
            if (_missing.Count > 0)
                Debug.LogWarning("[" + module.id + "] 缺少素材占位 " + _missing.Count + " 项：" + string.Join(",", _missing.ToArray()));
        }

        public Transform Get(string elementId)
        {
            Transform t = null;
            return _slots.TryGetValue(elementId, out t) ? t : null;
        }

        public void Bind(string elementId, Renderer renderer)
        {
            var t = Get(elementId);
            if (t != null && renderer != null) renderer.transform.SetParent(t, false);
        }
    }
}
