using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Input
{
    /// <summary>
    /// 输入动作。
    /// </summary>
    public sealed class InputAction : IReference
    {
        /// <summary>
        /// 存储输入绑定。
        /// </summary>
        private readonly List<InputBinding> m_Bindings = new List<InputBinding>();
        /// <summary>
        /// 存储输入动作状态。
        /// </summary>
        private InputActionState m_State;

        /// <summary>
        /// 初始化输入动作。
        /// </summary>
        /// <param name="name">动作名称。</param>
        public InputAction(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Action name cannot be empty.", nameof(name));
            }

            Name = name;
        }

        /// <summary>
        /// 动作名称。
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 动作绑定列表。
        /// </summary>
        public IReadOnlyList<InputBinding> Bindings => m_Bindings;

        /// <summary>
        /// 当前动作状态。
        /// </summary>
        public InputActionState State => m_State;

        /// <summary>
        /// 添加输入绑定。
        /// </summary>
        /// <param name="binding">输入绑定。</param>
        public void AddBinding(InputBinding binding)
        {
            ValidateBinding(binding);
            m_Bindings.Add(binding);
        }

        /// <summary>
        /// 释放输入动作。
        /// </summary>
        public void Release()
        {
            Name = null;
            m_Bindings.Clear();
            m_State.Reset();
        }

        /// <summary>
        /// 设置动作状态。
        /// </summary>
        /// <param name="pressed">是否按住。</param>
        /// <param name="value">输入值。</param>
        internal void SetState(bool pressed, float value)
        {
            m_State.Set(pressed, value);
        }

        /// <summary>
        /// 清空当前状态。
        /// </summary>
        internal void ClearState()
        {
            m_State.Clear();
        }

        /// <summary>
        /// 重置状态。
        /// </summary>
        internal void ResetState()
        {
            m_State.Reset();
        }

        private static void ValidateBinding(InputBinding binding)
        {
            switch (binding.Kind)
            {
                case InputBindingKind.Key:
                    return;
                case InputBindingKind.MouseButton:
                    if (binding.MouseButtonIndex < 0)
                    {
                        throw new ArgumentException("Mouse button cannot be negative.", nameof(binding));
                    }

                    return;
                case InputBindingKind.Axis:
                    if (binding.AxisName == null)
                    {
                        throw new ArgumentNullException(nameof(binding), "Axis name cannot be null.");
                    }

                    if (string.IsNullOrWhiteSpace(binding.AxisName))
                    {
                        throw new ArgumentException("Axis name cannot be empty.", nameof(binding));
                    }

                    return;
                default:
                    throw new ArgumentException("Input binding kind is not supported.", nameof(binding));
            }
        }
    }
}
