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
    /// 生成 UI 窗口、模型、模块和控制器脚本。
    /// </summary>
    internal static class UIDocumentGenerator
    {
        /// <summary>
        /// 自动生成脚本文件后缀。
        /// </summary>
        private const string GeneratedSuffix = ".g.cs";

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

            Directory.CreateDirectory(outputFolder);
            var bindings = CollectBindings(document);
            var windowName = className + "Window";
            var controllerName = className + "Controller";
            var moduleName = className + "Module";
            var modelName = className + "Model";

            WriteAllText(Path.Combine(outputFolder, windowName + GeneratedSuffix), GenerateWindow(windowName, controllerName, modelName, uiPath, layer, bindings));
            WriteAllText(Path.Combine(outputFolder, modelName + GeneratedSuffix), GenerateModel(modelName, bindings));
            WriteAllText(Path.Combine(outputFolder, moduleName + GeneratedSuffix), GenerateModule(moduleName, windowName));

            var controllerPath = Path.Combine(outputFolder, controllerName + ".cs");
            if (System.IO.File.Exists(controllerPath) is false)
            {
                WriteAllText(controllerPath, GenerateController(controllerName, windowName, modelName));
            }

            AssetDatabase.Refresh();
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

                    result.Add(new BindingInfo(mapping.Name, fieldName, ToQualifiedName(componentType)));
                }
            }

            return result;
        }

        /// <summary>
        /// 生成 UI 窗口脚本内容。
        /// </summary>
        /// <param name="windowName">窗口类型名。</param>
        /// <param name="controllerName">控制器类型名。</param>
        /// <param name="modelName">模型类型名。</param>
        /// <param name="uiPath">UI 资源路径。</param>
        /// <param name="layer">UI 层级。</param>
        /// <param name="bindings">绑定信息列表。</param>
        /// <returns>脚本文本。</returns>
        private static string GenerateWindow(string windowName, string controllerName, string modelName, string uiPath, UILayer layer, List<BindingInfo> bindings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("using Cysharp.Threading.Tasks;");
            sb.AppendLine("using GameDeveloperKit.UI;");
            sb.AppendLine();
            sb.AppendLine("[UIOption(" + Quote(uiPath) + ", UILayer." + layer + ")]");
            sb.AppendLine("public sealed partial class " + windowName + " : UIWindow");
            sb.AppendLine("{");
            sb.AppendLine("    private readonly " + controllerName + " m_Controller = new " + controllerName + "();");
            sb.AppendLine();
            sb.AppendLine("    public " + modelName + " Model { get; private set; }");
            sb.AppendLine();
            sb.AppendLine("    public override async UniTask OnAwakeAsync()");
            sb.AppendLine("    {");
            sb.AppendLine("        Model = new " + modelName + "();");
            foreach (var binding in bindings)
            {
                sb.AppendLine("        Model." + binding.FieldName + " = Document.GetComponent<" + binding.ComponentType + ">(" + Quote(binding.MappingName) + ");");
            }

            sb.AppendLine("        await m_Controller.OnAwakeAsync(this, Model);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public override UniTask OnOpenAsync()");
            sb.AppendLine("    {");
            sb.AppendLine("        return m_Controller.OnOpenAsync(this);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public override void OnEnable()");
            sb.AppendLine("    {");
            sb.AppendLine("        m_Controller.OnEnable(this);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public override void OnDisable()");
            sb.AppendLine("    {");
            sb.AppendLine("        m_Controller.OnDisable(this);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public override void Release()");
            sb.AppendLine("    {");
            sb.AppendLine("        m_Controller.Release(this);");
            sb.AppendLine("        Model = null;");
            sb.AppendLine("        base.Release();");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// 生成 UI 模型脚本内容。
        /// </summary>
        /// <param name="modelName">模型类型名。</param>
        /// <param name="bindings">绑定信息列表。</param>
        /// <returns>脚本文本。</returns>
        private static string GenerateModel(string modelName, List<BindingInfo> bindings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine();
            sb.AppendLine("public sealed class " + modelName);
            sb.AppendLine("{");
            foreach (var binding in bindings)
            {
                sb.AppendLine("    public " + binding.ComponentType + " " + binding.FieldName + ";");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// 生成 UI 模块脚本内容。
        /// </summary>
        /// <param name="moduleName">模块类型名。</param>
        /// <param name="windowName">窗口类型名。</param>
        /// <returns>脚本文本。</returns>
        private static string GenerateModule(string moduleName, string windowName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("using Cysharp.Threading.Tasks;");
            sb.AppendLine("using GameDeveloperKit;");
            sb.AppendLine();
            sb.AppendLine("public static partial class " + moduleName);
            sb.AppendLine("{");
            sb.AppendLine("    public static UniTask<" + windowName + "> OpenAsync()");
            sb.AppendLine("    {");
            sb.AppendLine("        return Super.UI.OpenAsync<" + windowName + ">();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public static void Close()");
            sb.AppendLine("    {");
            sb.AppendLine("        Super.UI.Close<" + windowName + ">();");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// 生成可由用户继续扩展的 UI 控制器脚本内容。
        /// </summary>
        /// <param name="controllerName">控制器类型名。</param>
        /// <param name="windowName">窗口类型名。</param>
        /// <param name="modelName">模型类型名。</param>
        /// <returns>脚本文本。</returns>
        private static string GenerateController(string controllerName, string windowName, string modelName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using Cysharp.Threading.Tasks;");
            sb.AppendLine();
            sb.AppendLine("public sealed partial class " + controllerName);
            sb.AppendLine("{");
            sb.AppendLine("    public UniTask OnAwakeAsync(" + windowName + " window, " + modelName + " model)");
            sb.AppendLine("    {");
            sb.AppendLine("        return UniTask.CompletedTask;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public UniTask OnOpenAsync(" + windowName + " window)");
            sb.AppendLine("    {");
            sb.AppendLine("        return UniTask.CompletedTask;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public void OnEnable(" + windowName + " window)");
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public void OnDisable(" + windowName + " window)");
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public void Release(" + windowName + " window)");
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
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
            public BindingInfo(string mappingName, string fieldName, string componentType)
            {
                MappingName = mappingName;
                FieldName = fieldName;
                ComponentType = componentType;
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
        }
    }
}
