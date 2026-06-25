using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GameDeveloperKit;
using GameDeveloperKit.UI;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.UIEditor
{
    /// <summary>
    /// 生成 UI 窗口 partial 和设计绑定脚本。
    /// </summary>
    internal static partial class UIDocumentGenerator
    {
        /// <summary>
        /// 根据 UI 文档生成绑定脚本。
        /// </summary>
        /// <param name="document">UI 文档。</param>
        /// <param name="className">生成类型名前缀。</param>
        /// <param name="outputFolder">输出目录。</param>
        /// <param name="uiPath">UI 资源路径。</param>
        /// <param name="layer">UI 层级。</param>
        public static void Generate(UIDocument document, string className, string outputFolder, string uiPath, UILayer layer)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (string.IsNullOrWhiteSpace(className))
            {
                throw new ArgumentException("Class name cannot be empty.", nameof(className));
            }

            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                throw new ArgumentException("Output folder cannot be empty.", nameof(outputFolder));
            }

            if (string.IsNullOrWhiteSpace(uiPath))
            {
                throw new ArgumentException("UI path cannot be empty.", nameof(uiPath));
            }

            var bindings = CollectBindings(document);
            var localizedTextBindings = CollectLocalizedTextBindings(document, bindings);
            var names = ResolveNames(className);
            var targetOutputFolder = ResolveOutputFolder(outputFolder, uiPath);

            Directory.CreateDirectory(targetOutputFolder);
            CleanupLegacyRootOutputs(outputFolder, targetOutputFolder, names);

            var logicPath = Path.Combine(targetOutputFolder, names.LogicFileName);
            var legacyLogicPath = Path.Combine(outputFolder, names.LogicFileName);
            if (ShouldWriteLogic(logicPath, legacyLogicPath, names.WindowName))
            {
                WriteAllText(logicPath, GenerateLogic(names.WindowName));
            }

            WriteAllText(Path.Combine(targetOutputFolder, names.DesignFileName), GenerateDesign(names.WindowName, uiPath, layer, bindings, localizedTextBindings));
            WriteAllText(Path.Combine(targetOutputFolder, names.ModelFileName), GenerateModel(names.WindowName));
            if (string.IsNullOrEmpty(names.LegacyModelFileName) is false)
            {
                DeleteGeneratedFile(Path.Combine(targetOutputFolder, names.LegacyModelFileName));
            }

            AssetDatabase.Refresh();
        }

        private static string ResolveOutputFolder(string outputFolder, string uiPath)
        {
            var prefabName = Path.GetFileNameWithoutExtension(uiPath);
            if (string.IsNullOrWhiteSpace(prefabName))
            {
                throw new ArgumentException("UI path must point to a prefab file.", nameof(uiPath));
            }

            return Path.Combine(outputFolder, prefabName);
        }

        private static void CleanupLegacyRootOutputs(string outputFolder, string targetOutputFolder, GeneratedTypeNames names)
        {
            if (string.Equals(
                    Path.GetFullPath(outputFolder),
                    Path.GetFullPath(targetOutputFolder),
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            DeleteGeneratedFile(Path.Combine(outputFolder, names.DesignFileName));
            DeleteGeneratedFile(Path.Combine(outputFolder, names.ModelFileName));
            if (string.IsNullOrEmpty(names.LegacyModelFileName) is false)
            {
                DeleteGeneratedFile(Path.Combine(outputFolder, names.LegacyModelFileName));
            }
        }

        private static bool ShouldWriteLogic(string logicPath, string legacyLogicPath, string windowName)
        {
            if (System.IO.File.Exists(logicPath))
            {
                return false;
            }

            if (string.Equals(
                    Path.GetFullPath(legacyLogicPath),
                    Path.GetFullPath(logicPath),
                    StringComparison.OrdinalIgnoreCase) ||
                System.IO.File.Exists(legacyLogicPath) is false)
            {
                return true;
            }

            if (AreEquivalentSourceTexts(System.IO.File.ReadAllText(legacyLogicPath), GenerateLogic(windowName)))
            {
                DeleteFileWithMeta(legacyLogicPath);
                return true;
            }

            return false;
        }

        private static GeneratedTypeNames ResolveNames(string className)
        {
            var typeName = IsIdentifier(className) ? className : ToPascalIdentifier(className);
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentException("Class name cannot be converted to a valid UI window type name.", nameof(className));
            }

            var windowName = typeName.EndsWith("Window", StringComparison.Ordinal)
                ? typeName
                : typeName + "Window";
            if (IsIdentifier(windowName) is false)
            {
                throw new GameException($"UI window type name '{windowName}' is not a valid C# identifier.");
            }

            var legacyModelFileName = ToPascalIdentifier(windowName) + ".Model.g.cs";
            if (string.Equals(legacyModelFileName, windowName + ".Model.g.cs", StringComparison.Ordinal))
            {
                legacyModelFileName = string.Empty;
            }

            return new GeneratedTypeNames(
                windowName,
                windowName + ".cs",
                windowName + ".Design.g.cs",
                windowName + ".Model.g.cs",
                legacyModelFileName);
        }

        /// <summary>
        /// 收集并校验 UI 文档中的绑定配置。
        /// </summary>
        /// <param name="document">UI 文档。</param>
        /// <returns>绑定信息列表。</returns>
        private static List<BindingInfo> CollectBindings(UIDocument document)
        {
            var result = new List<BindingInfo>();
            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (var mapping in document.Mappings)
            {
                if (mapping == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(mapping.Name))
                {
                    throw new GameException("UI binding name cannot be empty.");
                }

                if (mapping.Target == null)
                {
                    throw new GameException($"UI binding '{mapping.Name}' target is missing.");
                }

                if (mapping.Components == null)
                {
                    continue;
                }

                foreach (var component in mapping.Components)
                {
                    if (component == null)
                    {
                        throw new GameException($"UI binding '{mapping.Name}' contains a missing component reference.");
                    }

                    var componentType = component.GetType();
                    var fieldName = CreateFieldName(mapping.Name, componentType);
                    if (IsIdentifier(fieldName) is false)
                    {
                        throw new GameException($"UI binding field name '{fieldName}' is not a valid C# identifier.");
                    }

                    if (names.Add(fieldName) is false)
                    {
                        throw new GameException($"Duplicate UI binding field name: {fieldName}");
                    }

                    if (mapping.Target.GetComponents<Component>().Contains(component) is false)
                    {
                        throw new GameException($"UI binding '{mapping.Name}' component '{componentType.Name}' does not belong to target '{mapping.Target.name}'.");
                    }

                    result.Add(new BindingInfo(mapping.Name, fieldName, ToQualifiedName(componentType), component));
                }
            }

            return result;
        }

        /// <summary>
        /// 收集并校验 UI 文档中的本地化文本绑定。
        /// </summary>
        /// <param name="document">UI 文档。</param>
        /// <param name="bindings">组件绑定列表。</param>
        /// <returns>本地化文本绑定信息列表。</returns>
        private static List<LocalizedTextBindingInfo> CollectLocalizedTextBindings(UIDocument document, List<BindingInfo> bindings)
        {
            var result = new List<LocalizedTextBindingInfo>();
            var componentBindings = new Dictionary<Component, List<BindingInfo>>();
            foreach (var binding in bindings)
            {
                if (componentBindings.TryGetValue(binding.Component, out var values) is false)
                {
                    values = new List<BindingInfo>();
                    componentBindings.Add(binding.Component, values);
                }

                values.Add(binding);
            }

            var localizedComponents = new HashSet<Component>();
            for (var i = 0; i < document.LocalizedTexts.Count; i++)
            {
                var localizedText = document.LocalizedTexts[i];
                if (localizedText == null)
                {
                    throw new GameException($"UI localized text binding #{i} is missing.");
                }

                var component = localizedText.Component;
                if (component == null)
                {
                    throw new GameException($"UI localized text binding #{i} component is missing.");
                }

                if (string.IsNullOrWhiteSpace(localizedText.Key))
                {
                    throw new GameException($"UI localized text binding '{FormatComponent(component)}' key cannot be empty.");
                }

                if (localizedComponents.Add(component) is false)
                {
                    throw new GameException($"Duplicate UI localized text binding for component '{FormatComponent(component)}'.");
                }

                if (UIDocumentLocalizationDrawer.IsLocalizableComponent(component) is false)
                {
                    throw new GameException($"UI localized text binding '{FormatComponent(component)}' type is not supported.");
                }

                if (componentBindings.TryGetValue(component, out var matchedBindings) is false)
                {
                    throw new GameException(DescribeUnboundLocalizedComponent(document, component));
                }

                if (UIDocumentLocalizationDrawer.IsLocalizableTextComponent(component) is false)
                {
                    throw new GameException($"UI localized text binding '{FormatComponent(component)}' type is not supported.");
                }

                if (matchedBindings.Count > 1)
                {
                    throw new GameException($"UI localized text binding '{FormatComponent(component)}' is selected by multiple UI bindings and cannot be localized unambiguously.");
                }

                var binding = matchedBindings[0];
                result.Add(new LocalizedTextBindingInfo(binding.MappingName, binding.FieldName, binding.ComponentType, localizedText.Key));
            }

            return result;
        }

        /// <summary>
        /// 使用 UTF-8 写入脚本文本。
        /// </summary>
        /// <param name="path">写入路径。</param>
        /// <param name="contents">脚本文本。</param>
        private static void WriteAllText(string path, string contents)
        {
            System.IO.File.WriteAllText(path, contents, Encoding.UTF8);
        }

        private static void DeleteGeneratedFile(string path)
        {
            if (System.IO.File.Exists(path) && IsGeneratedFile(path) is false)
            {
                return;
            }

            DeleteFileWithMeta(path);
        }

        private static void DeleteFileWithMeta(string path)
        {
            var metaPath = path + ".meta";
            var fileExists = System.IO.File.Exists(path);
            var metaExists = System.IO.File.Exists(metaPath);
            if (fileExists is false && metaExists is false)
            {
                return;
            }

            if (fileExists)
            {
                System.IO.File.Delete(path);
            }

            if (metaExists)
            {
                System.IO.File.Delete(metaPath);
            }
        }

        private static bool IsGeneratedFile(string path)
        {
            using (var reader = new StreamReader(path, Encoding.UTF8, true))
            {
                return string.Equals(reader.ReadLine(), "// <auto-generated />", StringComparison.Ordinal);
            }
        }

        private static bool AreEquivalentSourceTexts(string lhs, string rhs)
        {
            return string.Equals(
                NormalizeLineEndings(lhs),
                NormalizeLineEndings(rhs),
                StringComparison.Ordinal);
        }

        private static string NormalizeLineEndings(string value)
        {
            return value.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        /// <summary>
        /// 判断字符串是否是合法的 C# 标识符。
        /// </summary>
        /// <param name="value">待检查的字符串。</param>
        /// <returns>是合法标识符时返回 true。</returns>
        private static bool IsIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || (char.IsLetter(value[0]) || value[0] == '_') is false)
            {
                return false;
            }

            for (var i = 1; i < value.Length; i++)
            {
                if ((char.IsLetterOrDigit(value[i]) || value[i] == '_') is false)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 根据绑定名和组件类型创建字段名。
        /// </summary>
        /// <param name="mappingName">绑定名。</param>
        /// <param name="componentType">组件类型。</param>
        /// <returns>字段名。</returns>
        internal static string CreateFieldName(string mappingName, Type componentType)
        {
            var suffix = mappingName.StartsWith("b_", StringComparison.Ordinal) ? mappingName.Substring(2) : mappingName;
            suffix = ToSnakeCase(suffix);
            var prefix = GetComponentPrefix(componentType);
            return string.IsNullOrWhiteSpace(suffix) ? prefix : prefix + "_" + suffix;
        }

        /// <summary>
        /// 获取常见 UI 组件的字段名前缀。
        /// </summary>
        /// <param name="componentType">组件类型。</param>
        /// <returns>字段名前缀。</returns>
        private static string GetComponentPrefix(Type componentType)
        {
            switch (componentType.Name)
            {
                case "Button":
                    return "btn";
                case "Text":
                case "TMP_Text":
                case "TextMeshProUGUI":
                    return "text";
                case "Image":
                case "RawImage":
                    return "img";
                case "Toggle":
                    return "toggle";
                case "Slider":
                    return "slider";
                case "InputField":
                case "TMP_InputField":
                    return "input";
                default:
                    return ToSnakeCase(componentType.Name);
            }
        }

        /// <summary>
        /// 把字符串转换为 snake_case。
        /// </summary>
        /// <param name="value">待转换字符串。</param>
        /// <returns>snake_case 字符串。</returns>
        private static string ToSnakeCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var previousWasSeparator = false;
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (char.IsLetterOrDigit(ch) is false)
                {
                    if (sb.Length > 0 && previousWasSeparator is false)
                    {
                        sb.Append('_');
                        previousWasSeparator = true;
                    }

                    continue;
                }

                if (char.IsUpper(ch) && sb.Length > 0 && previousWasSeparator is false)
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(ch));
                previousWasSeparator = false;
            }

            if (sb.Length > 0 && sb[sb.Length - 1] == '_')
            {
                sb.Length--;
            }

            return sb.ToString();
        }

        private static string ToPascalIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var upperNext = true;
            foreach (var ch in value)
            {
                if (char.IsLetterOrDigit(ch) is false)
                {
                    upperNext = true;
                    continue;
                }

                sb.Append(upperNext ? char.ToUpperInvariant(ch) : ch);
                upperNext = false;
            }

            if (sb.Length == 0)
            {
                return string.Empty;
            }

            if (char.IsDigit(sb[0]))
            {
                sb.Insert(0, "UI");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 获取组件类型的全局限定类型名。
        /// </summary>
        /// <param name="type">组件类型。</param>
        /// <returns>全局限定类型名。</returns>
        private static string ToQualifiedName(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            return "global::" + type.FullName.Replace("+", ".");
        }

        /// <summary>
        /// 描述未绑定的本地化组件。
        /// </summary>
        /// <param name="document">UI 文档。</param>
        /// <param name="component">组件。</param>
        /// <returns>错误描述。</returns>
        private static string DescribeUnboundLocalizedComponent(UIDocument document, Component component)
        {
            foreach (var mapping in document.Mappings)
            {
                if (mapping?.Target == component.gameObject)
                {
                    return $"UI localized text binding '{FormatComponent(component)}' is not selected in UI binding '{mapping.Name}'. Select the component before setting a localization key.";
                }
            }

            return $"UI localized text binding '{FormatComponent(component)}' does not belong to any UI binding target.";
        }

        /// <summary>
        /// 格式化组件描述。
        /// </summary>
        /// <param name="component">组件。</param>
        /// <returns>组件描述。</returns>
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
        /// 转义字符串字面量。
        /// </summary>
        /// <param name="value">原始字符串。</param>
        /// <returns>C# 字符串字面量。</returns>
        private static string Quote(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        /// <summary>
        /// UI 绑定代码生成所需的字段信息。
        /// </summary>
        private readonly struct BindingInfo
        {
            /// <summary>
            /// 初始化 UI 绑定字段信息。
            /// </summary>
            /// <param name="mappingName">绑定名。</param>
            /// <param name="fieldName">字段名。</param>
            /// <param name="componentType">组件类型名。</param>
            /// <param name="component">组件。</param>
            public BindingInfo(string mappingName, string fieldName, string componentType, Component component)
            {
                MappingName = mappingName;
                FieldName = fieldName;
                ComponentType = componentType;
                Component = component;
            }

            /// <summary>
            /// 绑定名。
            /// </summary>
            public string MappingName { get; }

            /// <summary>
            /// 字段名。
            /// </summary>
            public string FieldName { get; }

            /// <summary>
            /// 组件类型名。
            /// </summary>
            public string ComponentType { get; }

            /// <summary>
            /// 组件。
            /// </summary>
            public Component Component { get; }
        }

        /// <summary>
        /// UI 本地化绑定代码生成所需的字段信息。
        /// </summary>
        private readonly struct LocalizedTextBindingInfo
        {
            /// <summary>
            /// 初始化 UI 本地化绑定字段信息。
            /// </summary>
            /// <param name="mappingName">绑定名。</param>
            /// <param name="fieldName">字段名。</param>
            /// <param name="componentType">组件类型名。</param>
            /// <param name="key">本地化 Key。</param>
            public LocalizedTextBindingInfo(string mappingName, string fieldName, string componentType, string key)
            {
                MappingName = mappingName;
                FieldName = fieldName;
                ComponentType = componentType;
                Key = key;
            }

            /// <summary>
            /// 绑定名。
            /// </summary>
            public string MappingName { get; }

            /// <summary>
            /// 字段名。
            /// </summary>
            public string FieldName { get; }

            /// <summary>
            /// 组件类型名。
            /// </summary>
            public string ComponentType { get; }

            /// <summary>
            /// 本地化 Key。
            /// </summary>
            public string Key { get; }
        }

        private readonly struct GeneratedTypeNames
        {
            public GeneratedTypeNames(
                string windowName,
                string logicFileName,
                string designFileName,
                string modelFileName,
                string legacyModelFileName)
            {
                WindowName = windowName;
                LogicFileName = logicFileName;
                DesignFileName = designFileName;
                ModelFileName = modelFileName;
                LegacyModelFileName = legacyModelFileName;
            }

            public string WindowName { get; }

            public string LogicFileName { get; }

            public string DesignFileName { get; }

            public string ModelFileName { get; }

            public string LegacyModelFileName { get; }
        }
    }
}
