using System;
using UnityEngine;

namespace GameDeveloperKit.Input
{
    /// <summary>
    /// 输入动作绑定。
    /// </summary>
    public readonly struct InputBinding
    {
        /// <summary>
        /// 初始化输入动作绑定。
        /// </summary>
        /// <param name="kind">绑定类型。</param>
        /// <param name="keyCode">键盘按键。</param>
        /// <param name="mouseButtonIndex">鼠标按钮索引。</param>
        /// <param name="axisName">输入轴名称。</param>
        /// <param name="scale">输入值缩放。</param>
        private InputBinding(InputBindingKind kind, KeyCode keyCode, int mouseButtonIndex, string axisName, float scale)
        {
            Kind = kind;
            KeyCode = keyCode;
            MouseButtonIndex = mouseButtonIndex;
            AxisName = axisName;
            Scale = scale;
        }

        /// <summary>
        /// 绑定类型。
        /// </summary>
        public InputBindingKind Kind { get; }

        /// <summary>
        /// 键盘按键。
        /// </summary>
        public KeyCode KeyCode { get; }

        /// <summary>
        /// 鼠标按钮索引。
        /// </summary>
        public int MouseButtonIndex { get; }

        /// <summary>
        /// 输入轴名称。
        /// </summary>
        public string AxisName { get; }

        /// <summary>
        /// 输入值缩放。
        /// </summary>
        public float Scale { get; }

        /// <summary>
        /// 创建键盘按键绑定。
        /// </summary>
        /// <param name="key">键盘按键。</param>
        /// <returns>输入动作绑定。</returns>
        public static InputBinding Key(KeyCode key)
        {
            return new InputBinding(InputBindingKind.Key, key, -1, null, 1f);
        }

        /// <summary>
        /// 创建鼠标按钮绑定。
        /// </summary>
        /// <param name="mouseButton">鼠标按钮索引。</param>
        /// <returns>输入动作绑定。</returns>
        public static InputBinding MouseButton(int mouseButton)
        {
            if (mouseButton < 0)
            {
                throw new ArgumentException("Mouse button cannot be negative.", nameof(mouseButton));
            }

            return new InputBinding(InputBindingKind.MouseButton, KeyCode.None, mouseButton, null, 1f);
        }

        /// <summary>
        /// 创建输入轴绑定。
        /// </summary>
        /// <param name="axisName">输入轴名称。</param>
        /// <param name="scale">输入值缩放。</param>
        /// <returns>输入动作绑定。</returns>
        public static InputBinding Axis(string axisName, float scale = 1f)
        {
            if (axisName == null)
            {
                throw new ArgumentNullException(nameof(axisName));
            }

            if (string.IsNullOrWhiteSpace(axisName))
            {
                throw new ArgumentException("Axis name cannot be empty.", nameof(axisName));
            }

            return new InputBinding(InputBindingKind.Axis, KeyCode.None, -1, axisName, scale);
        }
    }
}
