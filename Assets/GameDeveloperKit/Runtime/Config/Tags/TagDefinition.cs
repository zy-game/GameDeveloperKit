using System;
using UnityEngine;

namespace GameDeveloperKit.Config
{
    /// <summary>
    /// 定义 Tag Definition 类型。
    /// </summary>
    [Serializable]
    public sealed class TagDefinition
    {
        [SerializeField] private string m_Key;

        [SerializeField] private string m_DisplayName;

        [SerializeField] private string m_Description;

        public string Key
        {
            get => m_Key;
            set => m_Key = value;
        }

        public string DisplayName
        {
            get => m_DisplayName;
            set => m_DisplayName = value;
        }

        public string Description
        {
            get => m_Description;
            set => m_Description = value;
        }
    }
}
