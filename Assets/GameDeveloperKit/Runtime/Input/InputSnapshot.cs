using System.Collections.Generic;

namespace GameDeveloperKit.Input
{
    /// <summary>
    /// 输入模块快照。
    /// </summary>
    public readonly struct InputSnapshot
    {
        /// <summary>
        /// 初始化输入模块快照。
        /// </summary>
        /// <param name="enabled">模块是否启用。</param>
        /// <param name="maps">动作组快照列表。</param>
        public InputSnapshot(bool enabled, IReadOnlyList<InputActionMapSnapshot> maps)
        {
            Enabled = enabled;
            Maps = maps;
        }

        /// <summary>
        /// 模块是否启用。
        /// </summary>
        public bool Enabled { get; }

        /// <summary>
        /// 动作组快照列表。
        /// </summary>
        public IReadOnlyList<InputActionMapSnapshot> Maps { get; }
    }

    /// <summary>
    /// 输入动作组快照。
    /// </summary>
    public readonly struct InputActionMapSnapshot
    {
        /// <summary>
        /// 初始化输入动作组快照。
        /// </summary>
        /// <param name="name">动作组名称。</param>
        /// <param name="enabled">是否启用。</param>
        /// <param name="actions">动作快照列表。</param>
        public InputActionMapSnapshot(string name, bool enabled, IReadOnlyList<InputActionSnapshot> actions)
        {
            Name = name;
            Enabled = enabled;
            Actions = actions;
        }

        /// <summary>
        /// 动作组名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 动作组是否启用。
        /// </summary>
        public bool Enabled { get; }

        /// <summary>
        /// 动作快照列表。
        /// </summary>
        public IReadOnlyList<InputActionSnapshot> Actions { get; }
    }

    /// <summary>
    /// 输入动作快照。
    /// </summary>
    public readonly struct InputActionSnapshot
    {
        /// <summary>
        /// 初始化输入动作快照。
        /// </summary>
        /// <param name="name">动作名称。</param>
        /// <param name="state">动作状态。</param>
        public InputActionSnapshot(string name, InputActionState state)
        {
            Name = name;
            State = state;
        }

        /// <summary>
        /// 动作名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 动作状态。
        /// </summary>
        public InputActionState State { get; }
    }
}
