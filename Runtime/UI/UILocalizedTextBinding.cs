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
        public Component Component;
        public string Key;
    }
}
