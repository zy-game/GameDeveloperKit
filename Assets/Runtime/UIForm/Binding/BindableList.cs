using System;
using System.Collections.Generic;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// 可绑定列表（用于 UIDataBase 中定义动态数据集合）
    /// 支持列表变化通知
    /// </summary>
    public class BindableList<T>
    {
        private List<T> _list = new List<T>();
        
        /// <summary>
        /// 添加项事件
        /// </summary>
        public event Action<T, int> OnItemAdded;
        
        /// <summary>
        /// 移除项事件
        /// </summary>
        public event Action<T, int> OnItemRemoved;
        
        /// <summary>
        /// 修改项事件
        /// </summary>
        public event Action<T, int> OnItemChanged;
        
        /// <summary>
        /// 清空列表事件
        /// </summary>
        public event Action OnListCleared;
        
        /// <summary>
        /// 刷新整个列表事件
        /// </summary>
        public event Action OnListRefreshed;
        
        /// <summary>
        /// 索引器
        /// </summary>
        public T this[int index]
        {
            get => _list[index];
            set
            {
                if (index >= 0 && index < _list.Count)
                {
                    _list[index] = value;
                    OnItemChanged?.Invoke(value, index);
                }
            }
        }
        
        /// <summary>
        /// 列表元素数量
        /// </summary>
        public int Count => _list.Count;
        
        /// <summary>
        /// 添加元素
        /// </summary>
        public void Add(T item)
        {
            _list.Add(item);
            OnItemAdded?.Invoke(item, _list.Count - 1);
        }
        
        /// <summary>
        /// 插入元素
        /// </summary>
        public void Insert(int index, T item)
        {
            _list.Insert(index, item);
            OnItemAdded?.Invoke(item, index);
        }
        
        /// <summary>
        /// 移除元素
        /// </summary>
        public bool Remove(T item)
        {
            int index = _list.IndexOf(item);
            if (index >= 0)
            {
                _list.RemoveAt(index);
                OnItemRemoved?.Invoke(item, index);
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// 移除指定索引的元素
        /// </summary>
        public void RemoveAt(int index)
        {
            if (index >= 0 && index < _list.Count)
            {
                T item = _list[index];
                _list.RemoveAt(index);
                OnItemRemoved?.Invoke(item, index);
            }
        }
        
        /// <summary>
        /// 清空列表
        /// </summary>
        public void Clear()
        {
            _list.Clear();
            OnListCleared?.Invoke();
        }
        
        /// <summary>
        /// 刷新整个列表（强制触发UI重建）
        /// </summary>
        public void Refresh()
        {
            OnListRefreshed?.Invoke();
        }
        
        /// <summary>
        /// 获取枚举器
        /// </summary>
        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
        
        /// <summary>
        /// 转换为数组
        /// </summary>
        public T[] ToArray() => _list.ToArray();
        
        /// <summary>
        /// 清理所有事件订阅
        /// </summary>
        public void ClearListeners()
        {
            OnItemAdded = null;
            OnItemRemoved = null;
            OnItemChanged = null;
            OnListCleared = null;
            OnListRefreshed = null;
        }
        
        /// <summary>
        /// 隐式转换：BindableList<T> → int（获取Count）
        /// </summary>
        public static implicit operator int(BindableList<T> list)
        {
            return list.Count;
        }
    }
}
