using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuilinEye.Shared
{
    /// <summary>SP-17 固定容量环形缓冲；0 号是最旧元素。</summary>
    public sealed class RingBuffer<T>
    {
        readonly T[] _items;
        int _start;
        int _count;

        public RingBuffer(int capacity)
        {
            _items = new T[Mathf.Max(1, capacity)];
        }

        public int Capacity { get { return _items.Length; } }
        public int Count { get { return _count; } }
        public bool IsFull { get { return _count == _items.Length; } }

        public void Add(T item)
        {
            if (_count < _items.Length)
            {
                _items[(_start + _count) % _items.Length] = item;
                _count++;
            }
            else
            {
                _items[_start] = item;
                _start = (_start + 1) % _items.Length;
            }
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _count) throw new IndexOutOfRangeException();
                return _items[(_start + index) % _items.Length];
            }
        }

        public T[] ToArray()
        {
            var result = new T[_count];
            for (var i = 0; i < _count; i++) result[i] = _items[(_start + i) % _items.Length];
            return result;
        }

        public void Clear()
        {
            for (var i = 0; i < _items.Length; i++) _items[i] = default(T);
            _start = 0;
            _count = 0;
        }
    }

    /// <summary>SP-17 通用对象池；粒子、文字、事件对象复用，减少 GC。</summary>
    public sealed class ObjectPool<T> where T : class
    {
        readonly List<T> _free = new List<T>();
        readonly Func<T> _create;
        readonly Action<T> _onGet;
        readonly Action<T> _onRelease;
        readonly int _maxFree;

        public ObjectPool(Func<T> create, Action<T> onGet = null, Action<T> onRelease = null, int maxFree = 0)
        {
            if (create == null) throw new ArgumentNullException("create");
            _create = create;
            _onGet = onGet;
            _onRelease = onRelease;
            _maxFree = maxFree;
        }

        public int FreeCount { get { return _free.Count; } }

        public T Get()
        {
            T item;
            if (_free.Count > 0)
            {
                var last = _free.Count - 1;
                item = _free[last];
                _free.RemoveAt(last);
            }
            else item = _create();
            if (_onGet != null) _onGet(item);
            return item;
        }

        public void Release(T item)
        {
            if (item == null) return;
            if (_onRelease != null) _onRelease(item);
            if (_maxFree > 0 && _free.Count >= _maxFree) return;
            _free.Add(item);
        }

        public void Clear()
        {
            for (var i = 0; i < _free.Count; i++) if (_onRelease != null) _onRelease(_free[i]);
            _free.Clear();
        }
    }
}
