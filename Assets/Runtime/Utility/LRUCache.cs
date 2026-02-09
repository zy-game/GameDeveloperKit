using System.Collections.Generic;

namespace GameDeveloperKit
{
    /// <summary>
    /// LRU缓存（Least Recently Used）
    /// 用于VFS文件句柄缓存，自动淘汰最久未使用的项
    /// </summary>
    public class LRUCache<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<(TKey key, TValue value)>> _cache;
        private readonly LinkedList<(TKey key, TValue value)> _lruList;

        public LRUCache(int capacity)
        {
            _capacity = capacity;
            _cache = new Dictionary<TKey, LinkedListNode<(TKey, TValue)>>(capacity);
            _lruList = new LinkedList<(TKey, TValue)>();
        }

        public bool TryGet(TKey key, out TValue value)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                
                value = node.Value.value;
                return true;
            }
            
            value = default;
            return false;
        }

        public TValue Put(TKey key, TValue value)
        {
            TValue evicted = default;
            
            if (_cache.TryGetValue(key, out var existingNode))
            {
                _lruList.Remove(existingNode);
                _cache.Remove(key);
            }
            
            if (_cache.Count >= _capacity && _lruList.Last != null)
            {
                var lastNode = _lruList.Last;
                evicted = lastNode.Value.value;
                
                _lruList.RemoveLast();
                _cache.Remove(lastNode.Value.key);
            }
            
            var newNode = _lruList.AddFirst((key, value));
            _cache[key] = newNode;
            
            return evicted;
        }

        public bool Remove(TKey key)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _cache.Remove(key);
                return true;
            }
            return false;
        }

        public void Clear()
        {
            _cache.Clear();
            _lruList.Clear();
        }

        public int Count => _cache.Count;
        public int Capacity => _capacity;
    }
}
