using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// UI窗口堆栈，用于管理UI窗口的打开、关闭和导航。
    /// </summary>
    public sealed class UIStack
    {
        private readonly List<UIWindow> _windows = new();

        /// <summary>
        /// 获取堆栈中的窗口数量。
        /// </summary>
        public int Count => _windows.Count;

        /// <summary>
        /// 获取堆栈是否为空。
        /// </summary>
        public bool IsEmpty => _windows.Count == 0;

        /// <summary>
        /// 获取堆栈中所有窗口的只读列表。
        /// </summary>
        public IReadOnlyList<UIWindow> Windows => _windows;

        /// <summary>
        /// 查看堆栈顶部的窗口，但不移除它。
        /// </summary>
        /// <returns>堆栈顶部的窗口，如果堆栈为空则返回null。</returns>
        public UIWindow Peek()
        {
            return _windows.Count == 0 ? null : _windows[_windows.Count - 1];
        }

        /// <summary>
        /// 尝试查看堆栈顶部的窗口。
        /// </summary>
        /// <param name="window">输出的窗口。</param>
        /// <returns>如果堆栈不为空则返回true，否则返回false。</returns>
        public bool TryPeek(out UIWindow window)
        {
            window = Peek();
            return window != null;
        }

        /// <summary>
        /// 获取是否可以返回到上一个窗口。
        /// </summary>
        public bool CanGoBack => _windows.Count > 1;

        /// <summary>
        /// 将窗口推入堆栈。如果窗口已存在于堆栈中，则先移除再推入。
        /// </summary>
        /// <param name="window">要推入的窗口。</param>
        /// <exception cref="ArgumentNullException">当窗口为null时抛出。</exception>
        public void Push(UIWindow window)
        {
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            _windows.Remove(window);
            _windows.Add(window);
        }

        /// <summary>
        /// 从堆栈顶部弹出窗口。
        /// </summary>
        /// <returns>弹出的窗口，如果堆栈为空则返回null。</returns>
        public UIWindow Pop()
        {
            if (_windows.Count == 0)
            {
                return null;
            }

            var index = _windows.Count - 1;
            var window = _windows[index];
            _windows.RemoveAt(index);
            return window;
        }

        /// <summary>
        /// 从堆栈中移除指定的窗口。
        /// </summary>
        /// <param name="window">要移除的窗口。</param>
        /// <returns>如果窗口被成功移除则返回true，否则返回false。</returns>
        public bool Remove(UIWindow window)
        {
            return window != null && _windows.Remove(window);
        }

        /// <summary>
        /// 检查是否可以返回到指定类型的窗口。
        /// </summary>
        /// <typeparam name="TWindow">窗口类型。</typeparam>
        /// <returns>如果可以返回则返回true，否则返回false。</returns>
        public bool CanGoBackTo<TWindow>()
            where TWindow : UIWindow
        {
            for (var i = _windows.Count - 1; i >= 0; i--)
            {
                if (_windows[i] is TWindow)
                {
                    return i < _windows.Count - 1;
                }
            }

            return false;
        }

        /// <summary>
        /// 返回到指定类型的窗口，获取所有需要关闭的窗口列表。
        /// </summary>
        /// <typeparam name="TWindow">目标窗口类型。</typeparam>
        /// <returns>需要关闭的窗口列表（从顶部到目标窗口之前的所有窗口）。</returns>
        public List<UIWindow> BackTo<TWindow>()
            where TWindow : UIWindow
        {
            var results = new List<UIWindow>();

            for (var i = _windows.Count - 1; i >= 0; i--)
            {
                if (_windows[i] is TWindow)
                {
                    break;
                }

                results.Add(_windows[i]);
            }

            return results;
        }

        /// <summary>
        /// 弹出窗口直到遇到指定类型的窗口。
        /// </summary>
        /// <typeparam name="TWindow">目标窗口类型。</typeparam>
        /// <param name="includeTarget">是否包含目标窗口。</param>
        /// <returns>被弹出的窗口列表。</returns>
        public List<UIWindow> PopUntil<TWindow>(bool includeTarget = false)
            where TWindow : UIWindow
        {
            var results = new List<UIWindow>();

            for (var i = _windows.Count - 1; i >= 0; i--)
            {
                var window = _windows[i];
                if (window is TWindow)
                {
                    if (includeTarget)
                    {
                        results.Add(window);
                        _windows.RemoveAt(i);
                    }

                    break;
                }

                results.Add(window);
                _windows.RemoveAt(i);
            }

            return results;
        }

        /// <summary>
        /// 清空堆栈中的所有窗口。
        /// </summary>
        public void Clear()
        {
            _windows.Clear();
        }
    }
}
