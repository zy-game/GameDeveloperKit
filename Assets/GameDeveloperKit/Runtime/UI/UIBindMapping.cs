using System;
using UnityEngine;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// UI 绑定条目。
    /// </summary>
    [Serializable]
    public sealed class UIBindMapping
    {
        /// <summary>
        /// 存储 Name。
        /// </summary>
        public string Name;
        /// <summary>
        /// 存储 Target。
        /// </summary>
        public GameObject Target;
        /// <summary>
        /// 存储 Components。
        /// </summary>
        public Component[] Components;
    }
}
