using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// UI窗口组，用于管理同一层的多个窗口及其排序顺序。
    /// </summary>
    public sealed class UIGroup
    {
        private readonly Dictionary<string, UIWindow> _windows = new();
        private int _nextSortingOffset;

        /// <summary>
        /// 初始化UI窗口组的新实例。
        /// </summary>
        /// <param name="layer">UI层。</param>
        /// <param name="root">根RectTransform。</param>
        /// <param name="baseSortingOrder">基础排序顺序。</param>
        public UIGroup(UILayer layer, RectTransform root, int baseSortingOrder)
        {
            Layer = layer;
            Root = root;
            BaseSortingOrder = baseSortingOrder;
        }

        /// <summary>
        /// 获取UI层。
        /// </summary>
        public UILayer Layer { get; }

        /// <summary>
        /// 获取根RectTransform。
        /// </summary>
        public RectTransform Root { get; }

        /// <summary>
        /// 获取基础排序顺序。
        /// </summary>
        public int BaseSortingOrder { get; }

        /// <summary>
        /// 获取组内所有窗口的只读字典。
        /// </summary>
        public IReadOnlyDictionary<string, UIWindow> Windows => _windows;

        /// <summary>
        /// 预留排序顺序，为窗口分配一个新的排序值。
        /// </summary>
        /// <param name="requestedOffset">请求的偏移量。</param>
        /// <returns>分配的排序顺序。</returns>
        public int ReserveSortingOrder(int requestedOffset = 0)
        {
            var sortingOrder = BaseSortingOrder + _nextSortingOffset + requestedOffset;
            _nextSortingOffset += 10;
            return sortingOrder;
        }

        /// <summary>
        /// 向组中添加窗口。
        /// </summary>
        /// <param name="window">要添加的窗口。</param>
        public void Add(UIWindow window)
        {
            _windows[window.WindowKey] = window;
        }

        /// <summary>
        /// 从组中移除窗口。
        /// </summary>
        /// <param name="window">要移除的窗口。</param>
        /// <returns>如果窗口被成功移除则返回true，否则返回false。</returns>
        public bool Remove(UIWindow window)
        {
            return window != null && _windows.Remove(window.WindowKey);
        }

        /// <summary>
        /// 显示UI组。
        /// </summary>
        public void Show()
        {
            if (Root != null)
            {
                Root.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 隐藏UI组。
        /// </summary>
        public void Hide()
        {
            if (Root != null)
            {
                Root.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 清理UI组，移除所有窗口并重置排序偏移。
        /// </summary>
        public void Clearup()
        {
            _windows.Clear();
            _nextSortingOffset = 0;
        }
    }
}
