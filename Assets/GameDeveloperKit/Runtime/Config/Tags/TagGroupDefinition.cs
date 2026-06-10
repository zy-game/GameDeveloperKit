using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Config
{
    /// <summary>
    /// 定义 Tag Group Definition 类型。
    /// </summary>
    [Serializable]
    public sealed class TagGroupDefinition
    {
        [SerializeField] private string m_Key;

        [SerializeField] private string m_DisplayName;

        [SerializeField] private bool m_Fixed;

        [SerializeField] private List<TagDefinition> m_Tags = new List<TagDefinition>();

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

        public bool Fixed
        {
            get => m_Fixed;
            set => m_Fixed = value;
        }

        public List<TagDefinition> Tags
        {
            get
            {
                m_Tags ??= new List<TagDefinition>();
                return m_Tags;
            }
        }
    }
}
