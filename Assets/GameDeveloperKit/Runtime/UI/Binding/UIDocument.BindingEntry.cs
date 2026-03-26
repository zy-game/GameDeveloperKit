using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    public partial class UIDocument
    {
        /// <summary>
        /// UI 文档绑定条目，描述键、目标对象与组件集合的映射关系。
        /// </summary>
        [Serializable]
        public sealed class BindingEntry
        {
            [SerializeField] private string key;
            [SerializeField] private GameObject target;
            [SerializeField] private List<Component> components = new();

            /// <summary>
            /// 获取或设置绑定键。
            /// </summary>
            public string Key
            {
                get => key;
                set => key = value;
            }

            /// <summary>
            /// 获取或设置绑定目标对象。
            /// </summary>
            public GameObject Target
            {
                get => target;
                set => target = value;
            }

            /// <summary>
            /// 获取绑定的组件列表。
            /// </summary>
            public List<Component> Components => components;
        }
    }
}
