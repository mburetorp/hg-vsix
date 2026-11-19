using System.Collections.Generic;

namespace HgVsix.Core
{
    // Small thread safe least recently used cache
    public sealed class LruCache<TKey, TValue>
        where TKey : notnull
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _map;
        private readonly LinkedList<KeyValuePair<TKey, TValue>> _order = new LinkedList<KeyValuePair<TKey, TValue>>();
        private readonly object _lock = new object();

        public LruCache(int capacity, IEqualityComparer<TKey>? comparer = null)
        {
            _capacity = capacity < 1 ? 1 : capacity;
            _map = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(comparer);
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _map.Count;
                }
            }
        }

        public bool TryGet(TKey key, out TValue value)
        {
            lock (_lock)
            {
                if (_map.TryGetValue(key, out var node))
                {
                    _order.Remove(node);
                    _order.AddFirst(node);
                    value = node.Value.Value;
                    return true;
                }
            }

            value = default!;
            return false;
        }

        public void Set(TKey key, TValue value)
        {
            lock (_lock)
            {
                if (_map.TryGetValue(key, out var existing))
                {
                    _order.Remove(existing);
                    _map.Remove(key);
                }

                var node = new LinkedListNode<KeyValuePair<TKey, TValue>>(new KeyValuePair<TKey, TValue>(key, value));
                _order.AddFirst(node);
                _map[key] = node;

                while (_map.Count > _capacity)
                {
                    var last = _order.Last!;
                    _order.RemoveLast();
                    _map.Remove(last.Value.Key);
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _map.Clear();
                _order.Clear();
            }
        }
    }
}
