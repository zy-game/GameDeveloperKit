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

        private Dictionary<string, UIBindMapping> m_MappingLookup;

        public RectTransform FullScreenRoot => fullScreenRoot;

        public RectTransform SafeAreaRoot => safeAreaRoot;

        public IReadOnlyList<UIBindMapping> Mappings => mappings ?? Array.Empty<UIBindMapping>();

        public GameObject GetGameObject(string key)
        {
            if (TryGetGameObject(key, out var gameObject))
            {
                return gameObject;
            }

            throw new GameException($"UI binding '{key}' was not found.");
        }

        public bool TryGetGameObject(string key, out GameObject gameObject)
        {
            var mapping = GetMapping(key);
            gameObject = mapping?.Target;
            return gameObject != null;
        }

        public T GetComponent<T>(string key) where T : Component
        {
            var gameObject = GetGameObject(key);
            var component = gameObject.GetComponent<T>();
            if (component == null)
            {
                throw new GameException($"UI binding '{key}' does not contain component '{typeof(T).Name}'.");
            }

            return component;
        }

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
