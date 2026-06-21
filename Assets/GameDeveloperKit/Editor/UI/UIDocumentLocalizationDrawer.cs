using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.UIEditor
{
    /// <summary>
    /// 绘制 UIDocument 本地化文本绑定。
    /// </summary>
    internal sealed class UIDocumentLocalizationDrawer
    {
        /// <summary>
        /// 存储 Mappings。
        /// </summary>
        private readonly SerializedProperty m_Mappings;
        /// <summary>
        /// 存储 Localized Texts。
        /// </summary>
        private readonly SerializedProperty m_LocalizedTexts;
        private string[] m_KeyOptions;

        /// <summary>
        /// 初始化 UIDocument Localization Drawer。
        /// </summary>
        /// <param name="mappings">mappings 参数。</param>
        /// <param name="localizedTexts">localized Texts 参数。</param>
        public UIDocumentLocalizationDrawer(SerializedProperty mappings, SerializedProperty localizedTexts)
        {
            m_Mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
            m_LocalizedTexts = localizedTexts ?? throw new ArgumentNullException(nameof(localizedTexts));
        }

        /// <summary>
        /// 已配置 Binding 数量。
        /// </summary>
        public int ConfiguredBindingCount => m_LocalizedTexts.arraySize;

        /// <summary>
        /// 收集校验问题。
        /// </summary>
        /// <param name="issues">issues 参数。</param>
        public void CollectIssues(List<string> issues)
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            var activeComponents = new HashSet<Component>();
            foreach (var component in CollectLocalizableTextComponents())
            {
                activeComponents.Add(component.Component);
            }

            var seen = new HashSet<Component>();
            for (var i = 0; i < m_LocalizedTexts.arraySize; i++)
            {
                var binding = m_LocalizedTexts.GetArrayElementAtIndex(i);
                var component = binding.FindPropertyRelative("Component").objectReferenceValue as Component;
                var key = binding.FindPropertyRelative("Key").stringValue;
                if (component == null)
                {
                    issues.Add("Localization binding #" + i + " component is missing.");
                    continue;
                }

                if (activeComponents.Contains(component) is false)
                {
                    issues.Add("Localization binding '" + FormatComponent(component) + "' is not selected in bindings.");
                }

                if (seen.Add(component) is false)
                {
                    issues.Add("Duplicate localization binding for " + FormatComponent(component) + ".");
                }

                if (string.IsNullOrWhiteSpace(key))
                {
                    issues.Add("Localization binding '" + FormatComponent(component) + "' key is empty.");
                }
            }
        }

        /// <summary>
        /// 绘制本地化文本绑定入口。
        /// </summary>
        public void Draw()
        {
            var components = CollectLocalizableTextComponents();
            RemoveStaleBindings(components);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Localization", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            if (components.Count == 0)
            {
                EditorGUILayout.HelpBox("Select Text or TMP_Text components in bindings to edit localization keys.", MessageType.Info);
                EditorGUI.indentLevel--;
                return;
            }

            foreach (var component in components)
            {
                var key = GetKey(component.Component);
                EditorGUI.BeginChangeCheck();
                var nextKey = EditorGUILayout.TextField(GetLabel(component), key);
                if (EditorGUI.EndChangeCheck())
                {
                    SetKey(component.Component, nextKey);
                }
            }

            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// 绘制指定组件的本地化 Key。
        /// </summary>
        /// <param name="component">component 参数。</param>
        public void DrawComponentKey(Component component)
        {
            if (IsLocalizableComponent(component) is false)
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            var nextKey = EditorGUILayout.TextField("Localization Key", GetKey(component));
            if (EditorGUI.EndChangeCheck())
            {
                SetKey(component, nextKey);
            }
        }

        /// <summary>
        /// 绘制指定组件的本地化 Key 下拉。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <param name="component">component 参数。</param>
        /// <param name="fallbackKey">没有候选时使用的建议 key。</param>
        public void DrawComponentKeyPopup(Rect rect, Component component, string fallbackKey)
        {
            if (IsLocalizableComponent(component) is false)
            {
                return;
            }

            var options = GetKeyOptions(fallbackKey, component);
            var current = GetKey(component);
            var currentIndex = Array.IndexOf(options, string.IsNullOrWhiteSpace(current) ? string.Empty : current);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            EditorGUI.BeginChangeCheck();
            var nextIndex = EditorGUI.Popup(rect, currentIndex, options);
            if (EditorGUI.EndChangeCheck() && nextIndex >= 0 && nextIndex < options.Length)
            {
                SetKey(component, options[nextIndex]);
            }
        }

        /// <summary>
        /// 移除已经不再属于所选文本组件的绑定。
        /// </summary>
        public void RemoveStaleBindings()
        {
            RemoveStaleBindings(CollectLocalizableTextComponents());
        }

        /// <summary>
        /// 判断组件是否是可本地化文本组件。
        /// </summary>
        /// <param name="component">component 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        internal static bool IsLocalizableTextComponent(Component component)
        {
            if (component == null)
            {
                return false;
            }

            var type = component.GetType();
            while (type != null && type != typeof(Component))
            {
                switch (type.FullName)
                {
                    case "UnityEngine.UI.Text":
                    case "TMPro.TMP_Text":
                    case "TMPro.TextMeshProUGUI":
                        return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        /// <summary>
        /// 判断组件是否可配置本地化 Key。
        /// </summary>
        /// <param name="component">component 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        internal static bool IsLocalizableComponent(Component component)
        {
            return IsLocalizableTextComponent(component) ||
                   IsLocalizableImageComponent(component) ||
                   IsLocalizableAudioComponent(component);
        }

        /// <summary>
        /// 判断组件是否是可本地化图片组件。
        /// </summary>
        /// <param name="component">component 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        internal static bool IsLocalizableImageComponent(Component component)
        {
            if (component == null)
            {
                return false;
            }

            var type = component.GetType();
            while (type != null && type != typeof(Component))
            {
                switch (type.FullName)
                {
                    case "UnityEngine.UI.Image":
                    case "UnityEngine.UI.RawImage":
                        return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        /// <summary>
        /// 判断组件是否是可本地化音频组件。
        /// </summary>
        /// <param name="component">component 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        internal static bool IsLocalizableAudioComponent(Component component)
        {
            if (component == null)
            {
                return false;
            }

            var type = component.GetType();
            while (type != null && type != typeof(Component))
            {
                if (type.FullName == "UnityEngine.AudioSource")
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        /// <summary>
        /// 收集已选中的可本地化文本组件。
        /// </summary>
        /// <returns>执行结果。</returns>
        private List<LocalizableTextComponent> CollectLocalizableTextComponents()
        {
            var result = new List<LocalizableTextComponent>();
            var seen = new HashSet<Component>();
            for (var i = 0; i < m_Mappings.arraySize; i++)
            {
                var mapping = m_Mappings.GetArrayElementAtIndex(i);
                var mappingName = mapping.FindPropertyRelative("Name").stringValue;
                var target = mapping.FindPropertyRelative("Target").objectReferenceValue as GameObject;
                var components = mapping.FindPropertyRelative("Components");
                for (var componentIndex = 0; componentIndex < components.arraySize; componentIndex++)
                {
                    var component = components.GetArrayElementAtIndex(componentIndex).objectReferenceValue as Component;
                    if (IsLocalizableComponent(component) is false || seen.Add(component) is false)
                    {
                        continue;
                    }

                    result.Add(new LocalizableTextComponent(mappingName, target, component));
                }
            }

            return result;
        }

        /// <summary>
        /// 移除已经不再属于所选文本组件的绑定。
        /// </summary>
        /// <param name="components">components 参数。</param>
        private void RemoveStaleBindings(IReadOnlyCollection<LocalizableTextComponent> components)
        {
            var activeComponents = new HashSet<Component>();
            foreach (var component in components)
            {
                activeComponents.Add(component.Component);
            }

            for (var i = m_LocalizedTexts.arraySize - 1; i >= 0; i--)
            {
                var binding = m_LocalizedTexts.GetArrayElementAtIndex(i);
                var component = binding.FindPropertyRelative("Component").objectReferenceValue as Component;
                if (component == null || activeComponents.Contains(component) is false)
                {
                    DeleteArrayElement(m_LocalizedTexts, i);
                }
            }
        }

        /// <summary>
        /// 获取本地化 Key。
        /// </summary>
        /// <param name="component">component 参数。</param>
        /// <returns>执行结果。</returns>
        private string GetKey(Component component)
        {
            var index = FindBindingIndex(component);
            if (index < 0)
            {
                return string.Empty;
            }

            return m_LocalizedTexts.GetArrayElementAtIndex(index).FindPropertyRelative("Key").stringValue;
        }

        /// <summary>
        /// 设置本地化 Key。
        /// </summary>
        /// <param name="component">component 参数。</param>
        /// <param name="key">key 参数。</param>
        private void SetKey(Component component, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                RemoveBindings(component);
                return;
            }

            var index = FindBindingIndex(component);
            if (index < 0)
            {
                index = m_LocalizedTexts.arraySize;
                m_LocalizedTexts.InsertArrayElementAtIndex(index);
                m_LocalizedTexts.GetArrayElementAtIndex(index).FindPropertyRelative("Component").objectReferenceValue = component;
            }

            m_LocalizedTexts.GetArrayElementAtIndex(index).FindPropertyRelative("Key").stringValue = key;
        }

        /// <summary>
        /// 获取本地化 Key 候选。
        /// </summary>
        /// <param name="fallbackKey">建议 key。</param>
        /// <param name="component">组件。</param>
        /// <returns>候选列表。</returns>
        private string[] GetKeyOptions(string fallbackKey, Component component)
        {
            if (m_KeyOptions == null)
            {
                m_KeyOptions = CollectLocalizationKeys();
            }

            var result = new List<string> { string.Empty };
            result.AddRange(m_KeyOptions);
            var suggested = CreateSuggestedKey(fallbackKey, component);
            if (string.IsNullOrWhiteSpace(suggested) is false && result.Contains(suggested) is false)
            {
                result.Add(suggested);
            }

            var current = GetKey(component);
            if (string.IsNullOrWhiteSpace(current) is false && result.Contains(current) is false)
            {
                result.Add(current);
            }

            return result.ToArray();
        }

        private static string[] CollectLocalizationKeys()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) is false ||
                    path.IndexOf("Localization", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                CollectKeysFromJson(path, keys);
            }

            return keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
        }

        private static void CollectKeysFromJson(string path, ISet<string> keys)
        {
            try
            {
                var token = JToken.Parse(System.IO.File.ReadAllText(path));
                var rootObject = token as JObject;
                if (rootObject == null)
                {
                    return;
                }

                var entries = rootObject["entries"] as JObject ?? rootObject;
                foreach (var property in entries.Properties())
                {
                    if (property.Value.Type == JTokenType.Object || property.Value.Type == JTokenType.Array)
                    {
                        continue;
                    }

                    keys.Add(property.Name);
                }
            }
            catch
            {
            }
        }

        private static string CreateSuggestedKey(string fallbackKey, Component component)
        {
            var baseKey = string.IsNullOrWhiteSpace(fallbackKey)
                ? component != null ? component.gameObject.name : string.Empty
                : fallbackKey;
            if (baseKey.StartsWith("b_", StringComparison.Ordinal))
            {
                baseKey = baseKey.Substring(2);
            }

            var prefix = IsLocalizableImageComponent(component)
                ? "ui.image."
                : IsLocalizableAudioComponent(component)
                    ? "ui.audio."
                    : "ui.text.";
            return prefix + ToKeyPart(baseKey);
        }

        private static string ToKeyPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "binding";
            }

            var result = new List<char>();
            var previousWasSeparator = false;
            foreach (var ch in value)
            {
                if (char.IsLetterOrDigit(ch) is false)
                {
                    if (result.Count > 0 && previousWasSeparator is false)
                    {
                        result.Add('.');
                        previousWasSeparator = true;
                    }

                    continue;
                }

                if (char.IsUpper(ch) && result.Count > 0 && previousWasSeparator is false)
                {
                    result.Add('.');
                }

                result.Add(char.ToLowerInvariant(ch));
                previousWasSeparator = false;
            }

            while (result.Count > 0 && result[result.Count - 1] == '.')
            {
                result.RemoveAt(result.Count - 1);
            }

            return result.Count == 0 ? "binding" : new string(result.ToArray());
        }

        /// <summary>
        /// 移除指定组件的本地化绑定。
        /// </summary>
        /// <param name="component">component 参数。</param>
        public void RemoveBindings(Component component)
        {
            for (var i = m_LocalizedTexts.arraySize - 1; i >= 0; i--)
            {
                if (m_LocalizedTexts.GetArrayElementAtIndex(i).FindPropertyRelative("Component").objectReferenceValue == component)
                {
                    DeleteArrayElement(m_LocalizedTexts, i);
                }
            }
        }

        /// <summary>
        /// 查找绑定索引。
        /// </summary>
        /// <param name="component">component 参数。</param>
        /// <returns>执行结果。</returns>
        private int FindBindingIndex(Component component)
        {
            for (var i = 0; i < m_LocalizedTexts.arraySize; i++)
            {
                if (m_LocalizedTexts.GetArrayElementAtIndex(i).FindPropertyRelative("Component").objectReferenceValue == component)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 获取组件显示标签。
        /// </summary>
        /// <param name="component">component 参数。</param>
        /// <returns>执行结果。</returns>
        private static GUIContent GetLabel(LocalizableTextComponent component)
        {
            var targetName = component.Target == null ? "(Missing Target)" : component.Target.name;
            var bindingName = string.IsNullOrWhiteSpace(component.MappingName) ? targetName : component.MappingName;
            return new GUIContent(bindingName + " (" + component.Component.GetType().Name + ")");
        }

        /// <summary>
        /// 格式化组件。
        /// </summary>
        /// <param name="component">component 参数。</param>
        /// <returns>执行结果。</returns>
        private static string FormatComponent(Component component)
        {
            if (component == null)
            {
                return "(Missing Component)";
            }

            var targetName = component.gameObject == null ? "(Missing GameObject)" : component.gameObject.name;
            return component.GetType().Name + " on '" + targetName + "'";
        }

        /// <summary>
        /// 删除数组元素。
        /// </summary>
        /// <param name="array">array 参数。</param>
        /// <param name="index">index 参数。</param>
        private static void DeleteArrayElement(SerializedProperty array, int index)
        {
            var previousSize = array.arraySize;
            array.DeleteArrayElementAtIndex(index);
            if (array.arraySize == previousSize)
            {
                array.DeleteArrayElementAtIndex(index);
            }
        }

        /// <summary>
        /// 可本地化文本组件信息。
        /// </summary>
        private readonly struct LocalizableTextComponent
        {
            /// <summary>
            /// 初始化可本地化文本组件信息。
            /// </summary>
            /// <param name="mappingName">mapping Name 参数。</param>
            /// <param name="target">target 参数。</param>
            /// <param name="component">component 参数。</param>
            public LocalizableTextComponent(string mappingName, GameObject target, Component component)
            {
                MappingName = mappingName;
                Target = target;
                Component = component;
            }

            /// <summary>
            /// Mapping 名称。
            /// </summary>
            public string MappingName { get; }

            /// <summary>
            /// 目标对象。
            /// </summary>
            public GameObject Target { get; }

            /// <summary>
            /// 文本组件。
            /// </summary>
            public Component Component { get; }
        }
    }
}
