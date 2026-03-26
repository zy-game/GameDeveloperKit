using System;
using System.Collections;
using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 可绑定列表，支持列表变更通知。
    /// </summary>
    /// <typeparam name="T">列表项类型。</typeparam>
    public sealed class BindableList<T> : IReadOnlyList<T>
    {
        private readonly List<T> _items = new();

        /// <summary>
        /// 当列表内容变更时触发的事件。
        /// </summary>
        public event Action Changed;

        /// <summary>
        /// 获取列表项数量。
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// 获取指定索引的项。
        /// </summary>
        /// <param name="index">索引。</param>
        /// <returns>指定索引的项。</returns>
        public T this[int index] => _items[index];

        /// <summary>
        /// 向列表添加项。
        /// </summary>
        /// <param name="item">要添加的项。</param>
        public void Add(T item)
        {
            _items.Add(item);
            Changed?.Invoke();
        }

        /// <summary>
        /// 向列表添加项集合。
        /// </summary>
        /// <param name="items">要添加的项集合。</param>
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null)
            {
                return;
            }

            _items.AddRange(items);
            Changed?.Invoke();
        }

        /// <summary>
        /// 从列表中移除指定项。
        /// </summary>
        /// <param name="item">要移除的项。</param>
        /// <returns>如果项被移除则为true，否则为false。</returns>
        public bool Remove(T item)
        {
            var removed = _items.Remove(item);
            if (removed)
            {
                Changed?.Invoke();
            }

            return removed;
        }

        /// <summary>
        /// 移除指定索引处的项。
        /// </summary>
        /// <param name="index">要移除的项的索引。</param>
        public void RemoveAt(int index)
        {
            _items.RemoveAt(index);
            Changed?.Invoke();
        }

        /// <summary>
        /// 清空列表中的所有项。
        /// </summary>
        public void Clear()
        {
            if (_items.Count == 0)
            {
                return;
            }

            _items.Clear();
            Changed?.Invoke();
        }

        /// <summary>
        /// 确定列表中是否包含指定项。
        /// </summary>
        /// <param name="item">要查找的项。</param>
        /// <returns>如果列表中包含该项则为true，否则为false。</returns>
        public bool Contains(T item)
        {
            return _items.Contains(item);
        }

        /// <summary>
        /// 查找指定项的索引。
        /// </summary>
        /// <param name="item">要查找的项。</param>
        /// <returns>项的索引，如果未找到则为-1。</returns>
        public int IndexOf(T item)
        {
            return _items.IndexOf(item);
        }

        /// <summary>
        /// 返回循环访问列表的枚举器。
        /// </summary>
        /// <returns>枚举器。</returns>
        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        /// <summary>
        /// 返回循环访问集合的枚举器。
        /// </summary>
        /// <returns>枚举器。</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
