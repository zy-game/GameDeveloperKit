using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 输入上下文枚举，用于标识不同的输入场景
    /// </summary>
    [Flags]
    public enum InputContext
    {
        /// <summary>
        /// 无上下文
        /// </summary>
        None = 0,

        /// <summary>
        /// 游戏玩法上下文
        /// </summary>
        Gameplay = 1 << 0,

        /// <summary>
        /// UI上下文
        /// </summary>
        UI = 1 << 1,

        /// <summary>
        /// 所有上下文
        /// </summary>
        All = Gameplay | UI
    }
}
