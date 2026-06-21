using System;
using UnityEngine;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// UI 本地化文本绑定条目。
    /// </summary>
    [Serializable]
    public sealed class UILocalizedTextBinding
    {
        /// <summary>
        /// 存储 Component。
        /// </summary>
        public Component Component;

        /// <summary>
        /// 存储 Key。
        /// </summary>
        public string Key;
    }
}
