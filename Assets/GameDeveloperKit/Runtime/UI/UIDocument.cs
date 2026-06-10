using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.UI
{
    /// <summary>
    /// UI 设计绑定文档
    /// </summary>
    public sealed class UIDocument : MonoBehaviour
    {
        [SerializeField] private RectTransform fullScreenRoot;
        [SerializeField] private RectTransform safeAreaRoot;
        [SerializeField] private UIBindMapping[] mappings;

        /// <summary>
        /// 存储 Mapping Lookup。
        /// </summary>
        private Dictionary<string, UIBindMapping> m_MappingLookup;

        /// <summary>
        /// 存储 Full Screen Root。
        /// </summary>
        public RectTransform FullScreenRoot => fullScreenRoot;

        /// <summary>
        /// 存储 Safe Area Root。
        /// </summary>
        public RectTransform SafeAreaRoot => safeAreaRoot;

        /// <summary>
        /// 存储 Mappings。
        /// </summary>
        public IReadOnlyList<UIBindMapping> Mappings => mappings ?? Array.Empty<UIBindMapping>();

        /// <summary>
        /// 获取 Target。
        /// </summary>
        /// <param name="key">key 参数。</param>
        /// <returns>执行结果。</returns>
        public GameObject GetTarget(string key)
        {
            if (TryGetObject(key, out var gameObject))
            {
                return gameObject;
            }

            throw new GameException($"UI binding '{key}' was not found.");
        }

        /// <summary>
        /// 尝试获取 Object。
        /// </summary>
        /// <param name="key">key 参数。</param>
        /// <param name="gameObject">game Object 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        public bool TryGetObject(string key, out GameObject gameObject)
        {
            var mapping = GetMapping(key);
            gameObject = mapping?.Target;
            return gameObject != null;
        }

        /// <summary>
        /// 获取 Component。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        /// <param name="key">key 参数。</param>
        /// <returns>执行结果。</returns>
        public T GetComponent<T>(string key) where T : Component
        {
            var gameObject = GetTarget(key);
            var component = gameObject.GetComponent<T>();
            if (component == null)
            {
                throw new GameException($"UI binding '{key}' does not contain component '{typeof(T).Name}'.");
            }

            return component;
        }

        /// <summary>
        /// 获取 Mapping。
        /// </summary>
        /// <param name="key">key 参数。</param>
        /// <returns>执行结果。</returns>
        private UIBindMapping GetMapping(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Binding key cannot be empty.", nameof(key));
            }

            BuildLookup();
            m_MappingLookup.TryGetValue(key, out var mapping);
            return mapping;
        }

        /// <summary>
        /// 构建 Lookup。
        /// </summary>
        private void BuildLookup()
        {
            if (m_MappingLookup != null)
            {
                return;
            }

            m_MappingLookup = new Dictionary<string, UIBindMapping>(StringComparer.Ordinal);
            if (mappings == null)
            {
                return;
            }

            foreach (var mapping in mappings)
            {
                if (mapping == null || string.IsNullOrWhiteSpace(mapping.Name))
                {
                    continue;
                }

                if (m_MappingLookup.ContainsKey(mapping.Name))
                {
                    throw new GameException($"Duplicate UI binding name: {mapping.Name}");
                }

                m_MappingLookup.Add(mapping.Name, mapping);
            }
        }
    }
}
