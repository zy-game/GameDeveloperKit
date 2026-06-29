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
        public string Name;
        public GameObject Target;
        public Component[] Components;
    }
}
