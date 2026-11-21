using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuilinEye.Shared
{
    [Serializable]
    public class ContentSlotDef
    {
        public string id;
        public string resourcePath;
        public bool required = true;
        public string note;
    }

    /// <summary>SP-11 内容槽：按 ID 建节点、加载 Resources 资源、缺素材可上报。</summary>
    public class ContentSlotRegistry : MonoBehaviour
    {
        public List<ContentSlotDef> slots = new List<ContentSlotDef>();

        readonly Dictionary<string, Transform> _nodes = new Dictionary<string, Transform>();
        readonly List<string> _missing = new List<string>();

        public event Action<string, string> Missing;

        public int Count { get { return _nodes.Count; } }
        public IReadOnlyList<string> MissingIds { get { return _missing; } }

        public void Initialize(IEnumerable<ContentSlotDef> defs)
        {
            Clear();
            if (defs == null) return;
            foreach (var d in defs) Register(d);
        }

        public void Register(ContentSlotDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return;
            var go = new GameObject("Slot_" + def.id);
            go.transform.SetParent(transform, false);
            _nodes[def.id] = go.transform;
            slots.Add(def);
            if (string.IsNullOrEmpty(def.resourcePath))
            {
                _missing.Add(def.id);
                if (Missing != null) Missing(def.id, "resourcePath 为空");
            }
        }

        public T Load<T>(string id) where T : UnityEngine.Object
        {
            var def = Find(id);
            if (def == null || string.IsNullOrEmpty(def.resourcePath)) return null;
            var asset = Resources.Load<T>(def.resourcePath);
            if (asset == null && def.required && !_missing.Contains(id))
            {
                _missing.Add(id);
                if (Missing != null) Missing(id, "Resources 未找到 " + def.resourcePath);
            }
            return asset;
        }

        public Transform Get(string id)
        {
            Transform t = null;
            return id != null && _nodes.TryGetValue(id, out t) ? t : null;
        }

        public ContentSlotDef Find(string id)
        {
            for (var i = 0; i < slots.Count; i++) if (slots[i] != null && slots[i].id == id) return slots[i];
            return null;
        }

        public bool Bind(string id, Renderer renderer)
        {
            var t = Get(id);
            if (t == null || renderer == null) return false;
            renderer.transform.SetParent(t, false);
            return true;
        }

        public bool Bind(string id, GameObject instance)
        {
            var t = Get(id);
            if (t == null || instance == null) return false;
            instance.transform.SetParent(t, false);
            return true;
        }

        public void Clear()
        {
            slots.Clear();
            _nodes.Clear();
            _missing.Clear();
        }
    }
}
