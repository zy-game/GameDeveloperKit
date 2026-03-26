using System;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 输入绑定数据模型，定义按键与动作的映射关系
    /// </summary>
    [Serializable]
    public sealed class InputBindingData
    {
        /// <summary>
        /// 动作名称
        /// </summary>
        public string ActionName;

        /// <summary>
        /// 按键码
        /// </summary>
        public KeyCode Key;

        /// <summary>
        /// 输入上下文
        /// </summary>
        public InputContext Context;
    }
}
