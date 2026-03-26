using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    public partial class UIDocument
    {
        /// <summary>
        /// UI 文档节点，封装单个绑定目标及其组件集合。
        /// </summary>
        public sealed class Node
        {
            private readonly string _key;
            private readonly GameObject _target;
            private readonly List<Component> _components;

            /// <summary>
            /// 初始化 UI 文档节点的新实例。
            /// </summary>
            /// <param name="key">节点键。</param>
            /// <param name="target">节点目标对象。</param>
            /// <param name="components">节点组件列表。</param>
            internal Node(string key, GameObject target, List<Component> components)
            {
                _key = key;
                _target = target;
                _components = components ?? new List<Component>();
            }

            /// <summary>
            /// 获取节点键。
            /// </summary>
            public string Key => _key;

            /// <summary>
            /// 获取节点目标对象。
            /// </summary>
            public GameObject Target => _target;

            /// <summary>
            /// 获取节点绑定的组件列表。
            /// </summary>
            public IReadOnlyList<Component> Components => _components;

            /// <summary>
            /// 获取指定类型的目标对象或组件。
            /// </summary>
            /// <typeparam name="T">对象或组件类型。</typeparam>
            /// <returns>匹配的对象或组件。</returns>
            public T Get<T>()
                where T : UnityEngine.Object
            {
                if (typeof(T) == typeof(GameObject))
                {
                    return Target as T;
                }

                for (var i = 0; i < _components.Count; i++)
                {
                    if (_components[i] is T typedComponent)
                    {
                        return typedComponent;
                    }
                }

                throw new InvalidOperationException($"UIDocument node '{Key}' does not contain component '{typeof(T).FullName}'.");
            }

            /// <summary>
            /// 尝试获取指定类型的目标对象或组件。
            /// </summary>
            /// <typeparam name="T">对象或组件类型。</typeparam>
            /// <param name="value">输出的对象或组件。</param>
            /// <returns>如果获取成功则返回 <c>true</c>；否则返回 <c>false</c>。</returns>
            public bool TryGet<T>(out T value)
                where T : UnityEngine.Object
            {
                if (typeof(T) == typeof(GameObject) && Target is T typedTarget)
                {
                    value = typedTarget;
                    return true;
                }

                for (var i = 0; i < _components.Count; i++)
                {
                    if (_components[i] is T typedComponent)
                    {
                        value = typedComponent;
                        return true;
                    }
                }

                value = null;
                return false;
            }
        }
    }
}
