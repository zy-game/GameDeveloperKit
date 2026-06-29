using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Input
{
    /// <summary>
    /// 输入动作组。
    /// </summary>
    public sealed class InputActionMap : IReference
    {
        /// <summary>
        /// 存储动作列表。
        /// </summary>
        private readonly List<InputAction> m_Actions = new List<InputAction>();
        /// <summary>
        /// 存储动作查找表。
        /// </summary>
        private readonly Dictionary<string, InputAction> m_ActionByName = new Dictionary<string, InputAction>(StringComparer.Ordinal);

        /// <summary>
        /// 初始化输入动作组。
        /// </summary>
        /// <param name="name">动作组名称。</param>
        public InputActionMap(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Action map name cannot be empty.", nameof(name));
            }

            Name = name;
            Enabled = true;
        }

        /// <summary>
        /// 动作组名称。
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 动作组是否启用。
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 动作列表。
        /// </summary>
        public IReadOnlyList<InputAction> Actions => m_Actions;

        /// <summary>
        /// 添加输入动作。
        /// </summary>
        /// <param name="action">输入动作。</param>
        public void AddAction(InputAction action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (string.IsNullOrWhiteSpace(action.Name))
            {
                throw new ArgumentException("Action name cannot be empty.", nameof(action));
            }

            if (m_ActionByName.ContainsKey(action.Name))
            {
                throw new GameException($"Input action '{action.Name}' has already been registered in map '{Name}'.");
            }

            m_Actions.Add(action);
            m_ActionByName.Add(action.Name, action);
        }

        /// <summary>
        /// 尝试按名称查找动作。
        /// </summary>
        /// <param name="name">动作名称。</param>
        /// <param name="action">输出动作。</param>
        /// <returns>找到时返回 true。</returns>
        public bool TryGetAction(string name, out InputAction action)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Action name cannot be empty.", nameof(name));
            }

            return m_ActionByName.TryGetValue(name, out action);
        }

        /// <summary>
        /// 释放动作组。
        /// </summary>
        public void Release()
        {
            foreach (var action in m_Actions)
            {
                action.Release();
            }

            Name = null;
            Enabled = false;
            m_Actions.Clear();
            m_ActionByName.Clear();
        }
    }
}
