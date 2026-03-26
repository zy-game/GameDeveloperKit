using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// UI 文档组件，用于维护界面节点绑定与代码生成配置。
    /// </summary>
    public partial class UIDocument : MonoBehaviour
    {
        [SerializeField] private RectTransform fullScreenBackground;
        [SerializeField] private List<BindingEntry> bindings = new();
        [SerializeField] private GenerationSettings generation = new();

        private readonly Dictionary<string, Node> _nodes = new(StringComparer.Ordinal);

        /// <summary>
        /// 获取全屏背景节点。
        /// </summary>
        public RectTransform FullScreenBackground => fullScreenBackground;

        /// <summary>
        /// 获取绑定条目列表。
        /// </summary>
        public IReadOnlyList<BindingEntry> Bindings => bindings;

        /// <summary>
        /// 获取代码生成设置。
        /// </summary>
        public GenerationSettings Generation => generation ??= new GenerationSettings();

        /// <summary>
        /// 根据键获取文档节点。
        /// </summary>
        /// <param name="key">节点键。</param>
        /// <returns>对应的文档节点。</returns>
        public Node this[string key] => GetNode(key);

        /// <summary>
        /// 获取运行时代码元数据与预制体元数据之间的不一致信息。
        /// </summary>
        /// <param name="metadata">窗口元数据。</param>
        /// <returns>不一致描述；如果一致则返回空字符串。</returns>
        public string GetRuntimeMetadataMismatch(UIWindowAttribute metadata)
        {
            return string.Empty;
        }

        private void Awake()
        {
            Rebuild();
        }

        private void OnValidate()
        {
            Rebuild();
        }

        /// <summary>
        /// 检查是否包含指定键的节点。
        /// </summary>
        /// <param name="key">节点键。</param>
        /// <returns>如果存在对应节点则返回 <c>true</c>；否则返回 <c>false</c>。</returns>
        public bool Has(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && _nodes.ContainsKey(key);
        }

        /// <summary>
        /// 获取指定键对应的文档节点。
        /// </summary>
        /// <param name="key">节点键。</param>
        /// <returns>对应的文档节点。</returns>
        public Node GetNode(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("UIDocument key can not be empty.", nameof(key));
            }

            if (_nodes.TryGetValue(key, out var node))
            {
                return node;
            }

            throw new InvalidOperationException($"UIDocument key '{key}' does not exist on '{name}'.");
        }

        /// <summary>
        /// 根据当前绑定条目重建节点缓存。
        /// </summary>
        public void Rebuild()
        {
            _nodes.Clear();

            for (var i = 0; i < bindings.Count; i++)
            {
                var entry = bindings[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                _nodes[entry.Key] = new Node(entry.Key, entry.Target, entry.Components);
            }
        }

        /// <summary>
        /// 获取绑定数据的校验错误信息。
        /// </summary>
        /// <returns>校验错误信息；如果校验通过则返回空字符串。</returns>
        public string GetBindingValidationError()
        {
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < bindings.Count; i++)
            {
                var entry = bindings[i];
                if (entry == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    return $"UIDocument '{name}' contains an empty binding key at index {i}.";
                }

                if (!seenKeys.Add(entry.Key))
                {
                    return $"UIDocument '{name}' contains duplicate binding key '{entry.Key}'.";
                }

                if (entry.Target == null)
                {
                    return $"UIDocument '{name}' binding '{entry.Key}' does not reference a target GameObject.";
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 获取代码生成设置的校验错误信息。
        /// </summary>
        /// <returns>校验错误信息；如果校验通过则返回空字符串。</returns>
        public string GetGenerationValidationError()
        {
            var generationSettings = Generation;
            if (string.IsNullOrWhiteSpace(generationSettings.OutputDirectoryPath))
            {
                return $"UIDocument '{name}' generation output directory path is empty.";
            }

            if (!generationSettings.OutputDirectoryPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return $"UIDocument '{name}' generation output directory must stay under Assets.";
            }

            var identifierError = ValidateIdentifier(generationSettings.WindowClassName, "window class name");
            if (!string.IsNullOrWhiteSpace(identifierError))
            {
                return identifierError;
            }

            return ValidateNamespace(generationSettings.WindowNamespace);
        }

        private static string ValidateIdentifier(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (!IsValidIdentifier(value))
            {
                return $"UIDocument {label} '{value}' is not a valid C# identifier.";
            }

            return string.Empty;
        }

        private static string ValidateNamespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var segments = value.Split('.');
            for (var i = 0; i < segments.Length; i++)
            {
                if (!IsValidIdentifier(segments[i]))
                {
                    return $"UIDocument namespace '{value}' contains invalid identifier '{segments[i]}'.";
                }
            }

            return string.Empty;
        }

        private static bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!(char.IsLetter(value[0]) || value[0] == '_'))
            {
                return false;
            }

            for (var i = 1; i < value.Length; i++)
            {
                var character = value[i];
                if (!(char.IsLetterOrDigit(character) || character == '_'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
