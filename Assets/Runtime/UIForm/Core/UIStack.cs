using System;
using System.Collections.Generic;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// UI 导航栈，管理 Window 的返回逻辑
    /// </summary>
    public class UIStack
    {
        private readonly Stack<IUIForm> _stack = new Stack<IUIForm>();

        /// <summary>
        /// 栈中元素数量
        /// </summary>
        public int Count => _stack.Count;

        /// <summary>
        /// 栈顶 UI
        /// </summary>
        public IUIForm Top => _stack.Count > 0 ? _stack.Peek() : null;

        /// <summary>
        /// 压入 UI 到栈
        /// </summary>
        public void Push(IUIForm form)
        {
            if (form == null)
            {
                return;
            }

            // 如果栈顶已经是这个 UI，不重复压入
            if (_stack.Count > 0 && _stack.Peek() == form)
            {
                return;
            }

            _stack.Push(form);
        }

        /// <summary>
        /// 弹出栈顶 UI
        /// </summary>
        public IUIForm Pop()
        {
            if (_stack.Count == 0)
            {
                return null;
            }

            return _stack.Pop();
        }

        /// <summary>
        /// 查看栈顶 UI（不弹出）
        /// </summary>
        public IUIForm Peek()
        {
            if (_stack.Count == 0)
            {
                return null;
            }

            return _stack.Peek();
        }

        /// <summary>
        /// 返回到指定 UI
        /// </summary>
        /// <typeparam name="T">UI 类型</typeparam>
        /// <returns>弹出的 UI 列表</returns>
        public List<IUIForm> BackTo<T>() where T : IUIForm
        {
            var poppedForms = new List<IUIForm>();

            while (_stack.Count > 0)
            {
                var top = _stack.Peek();

                // 如果找到目标类型，停止弹出
                if (top is T)
                {
                    break;
                }

                poppedForms.Add(_stack.Pop());
            }

            return poppedForms;
        }

        /// <summary>
        /// 清空栈
        /// </summary>
        public void Clear()
        {
            _stack.Clear();
        }

        /// <summary>
        /// 检查栈中是否包含指定类型的 UI
        /// </summary>
        public bool Contains<T>() where T : IUIForm
        {
            foreach (var form in _stack)
            {
                if (form is T)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 检查栈中是否包含指定类型名的 UI
        /// </summary>
        public bool Contains(string formTypeName)
        {
            foreach (var form in _stack)
            {
                if (form.GetType().FullName == formTypeName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 移除指定 UI（当 UI 被直接关闭时）
        /// </summary>
        public void Remove(IUIForm form)
        {
            if (form == null || _stack.Count == 0)
            {
                return;
            }

            // 如果是栈顶，直接 Pop
            if (_stack.Peek() == form)
            {
                _stack.Pop();
                return;
            }

            // 否则需要重建栈
            var temp = new List<IUIForm>();

            while (_stack.Count > 0)
            {
                var current = _stack.Pop();
                if (current != form)
                {
                    temp.Add(current);
                }
            }

            // 反向压回
            for (int i = temp.Count - 1; i >= 0; i--)
            {
                _stack.Push(temp[i]);
            }
        }
    }
}
