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
        [SerializeField] private int layerOrder = 200;
        [SerializeField] private UIBindMapping[] mappings;
        [SerializeField] private UILocalizedTextBinding[] localizedTexts;
        private Dictionary<string, UIBindMapping> m_MappingLookup;
        public RectTransform FullScreenRoot => fullScreenRoot;
        public UILayer Layer => UILayer.FromOrder(layerOrder);
        public IReadOnlyList<UIBindMapping> Mappings => mappings ?? Array.Empty<UIBindMapping>();
        public IReadOnlyList<UILocalizedTextBinding> LocalizedTexts => localizedTexts ?? Array.Empty<UILocalizedTextBinding>();

        /// <summary>
        /// 获取 Target。
        /// </summary>
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
        /// <param name="gameObject">game Object 参数。</param>
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
        public T GetComponent<T>(string key) where T : Component
        {
            if (TryGetComponent(key, out T component))
            {
                return component;
            }

            throw new GameException($"UI binding '{key}' does not contain component '{typeof(T).Name}'.");
        }

        /// <summary>
        /// 尝试获取 Component。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public bool TryGetComponent<T>(string key, out T component) where T : Component
        {
            var mapping = GetMapping(key);
            if (mapping?.Components != null)
            {
                for (var i = 0; i < mapping.Components.Length; i++)
                {
                    if (mapping.Components[i] is T typedComponent)
                    {
                        component = typedComponent;
                        return true;
                    }
                }
            }

            component = null;
            return false;
        }

        /// <summary>
        /// 获取 Mapping。
        /// </summary>
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
