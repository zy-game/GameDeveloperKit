using System;
using System.Collections.Generic;
using GameDeveloperKit.EditorNodeGraph;
using UnityEngine.UIElements;

namespace GameDeveloperKit.StoryEditor.Logic
{
    public delegate VisualElement LogicParameterRenderer(
        string nodeId,
        EditorGraphFieldModel field,
        Action<string> valueChanged);

    public static class LogicParameterRendererRegistry
    {
        private static readonly Dictionary<string, LogicParameterRenderer> s_Renderers =
            new Dictionary<string, LogicParameterRenderer>(StringComparer.Ordinal);

        public static void Register(string rendererKey, LogicParameterRenderer renderer)
        {
            if (string.IsNullOrWhiteSpace(rendererKey))
            {
                throw new ArgumentException("Logic parameter renderer key cannot be empty.", nameof(rendererKey));
            }

            if (renderer == null)
            {
                throw new ArgumentNullException(nameof(renderer));
            }

            rendererKey = rendererKey.Trim();
            if (s_Renderers.ContainsKey(rendererKey))
            {
                throw new InvalidOperationException($"Logic parameter renderer is already registered. key:{rendererKey}");
            }

            s_Renderers.Add(rendererKey, renderer);
        }

        public static bool Unregister(string rendererKey)
        {
            return string.IsNullOrWhiteSpace(rendererKey) is false &&
                   s_Renderers.Remove(rendererKey.Trim());
        }

        public static bool IsRegistered(string rendererKey)
        {
            return string.IsNullOrWhiteSpace(rendererKey) is false &&
                   s_Renderers.ContainsKey(rendererKey.Trim());
        }

        internal static bool TryCreate(
            string rendererKey,
            string nodeId,
            EditorGraphFieldModel field,
            Action<string> valueChanged,
            out VisualElement element)
        {
            if (string.IsNullOrWhiteSpace(rendererKey) ||
                s_Renderers.TryGetValue(rendererKey.Trim(), out var renderer) is false)
            {
                element = null;
                return false;
            }

            element = renderer(nodeId, field, valueChanged);
            return element != null;
        }
    }
}
