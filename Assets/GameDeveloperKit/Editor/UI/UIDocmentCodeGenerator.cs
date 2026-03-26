using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GameDeveloperKit.Runtime;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor
{
    /// <summary>
    /// UIDocument 代码生成器，用于根据预制体绑定信息生成窗口相关代码。
    /// </summary>
    public static class UIDocumentCodeGenerator
    {
        private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
            "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
            "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
            "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
            "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
            "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
        };

        private sealed class BindingInfo
        {
            /// <summary>
            /// 绑定键。
            /// </summary>
            public string Key;

            /// <summary>
            /// 生成的属性名。
            /// </summary>
            public string PropertyName;

            /// <summary>
            /// 组件类型名称。
            /// </summary>
            public string ComponentTypeName;

            /// <summary>
            /// 组件命名空间。
            /// </summary>
            public string ComponentNamespace;

            /// <summary>
            /// 是否为子设计节点。
            /// </summary>
            public bool IsChildDesign;

            /// <summary>
            /// 子设计类名。
            /// </summary>
            public string ChildClassName;

            /// <summary>
            /// 子文档对象。
            /// </summary>
            public UIDocument ChildDocument;
        }

        private sealed class DesignNode
        {
            /// <summary>
            /// 设计类名。
            /// </summary>
            public string ClassName;

            /// <summary>
            /// 关联的 UI 文档。
            /// </summary>
            public UIDocument Document;

            /// <summary>
            /// 绑定信息列表。
            /// </summary>
            public List<BindingInfo> Bindings = new();
        }

        /// <summary>
        /// 为指定的 UI 文档生成全部代码文件。
        /// </summary>
        /// <param name="document">目标 UI 文档。</param>
        /// <param name="assetPath">预制体资源路径。</param>
        /// <returns>本次生成的文件路径列表。</returns>
        public static List<string> GenerateAllFiles(UIDocument document, string assetPath)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var validationError = ValidateDocument(document, assetPath);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                throw new InvalidOperationException(validationError);
            }

            var generatedFiles = new List<string>();
            var outputDirectoryPath = GetOutputDirectoryPath(document);
            var outputDirectory = AssetPathToAbsolutePath(outputDirectoryPath);
            var windowClassName = GetWindowClassName(document);
            var windowNamespace = GetWindowNamespace(document);
            var designNode = BuildNode("Design", document);

            generatedFiles.Add(WriteGeneratedFile(outputDirectory, outputDirectoryPath, $"{windowClassName}.g.cs", GenerateWindowShell(windowClassName, windowNamespace, assetPath)));
            generatedFiles.Add(WriteGeneratedFile(outputDirectory, outputDirectoryPath, $"{windowClassName}.Design.g.cs", GenerateDesignFile(windowClassName, windowNamespace, designNode, assetPath)));
            generatedFiles.Add(WriteGeneratedFile(outputDirectory, outputDirectoryPath, $"{windowClassName}.Data.g.cs", GenerateDataFile(windowClassName, windowNamespace, assetPath)));

            WriteIfMissing(Path.Combine(outputDirectory, $"{windowClassName}.cs"), GenerateWindowPartial(windowClassName, windowNamespace));
            WriteIfMissing(Path.Combine(outputDirectory, $"{windowClassName}.Design.cs"), GenerateDesignPartial(windowClassName, windowNamespace));
            WriteIfMissing(Path.Combine(outputDirectory, $"{windowClassName}.Data.cs"), GenerateDataPartial(windowClassName, windowNamespace));

            return generatedFiles;
        }

        /// <summary>
        /// 校验指定 UI 文档是否满足代码生成要求。
        /// </summary>
        /// <param name="document">目标 UI 文档。</param>
        /// <param name="assetPath">预制体资源路径。</param>
        /// <returns>校验错误信息；如果校验通过则返回空字符串。</returns>
        public static string ValidateDocument(UIDocument document, string assetPath)
        {
            if (document == null)
            {
                return "UIDocument target is missing.";
            }

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return $"UIDocument '{document.name}' prefab asset path is empty.";
            }

            var outputDirectoryPath = GetOutputDirectoryPath(document);
            if (string.IsNullOrWhiteSpace(outputDirectoryPath) || !outputDirectoryPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return $"UIDocument '{document.name}' output directory must stay under Assets.";
            }

            var windowClassName = GetWindowClassName(document);
            if (string.IsNullOrWhiteSpace(windowClassName))
            {
                return $"UIDocument '{document.name}' could not resolve a valid window class name.";
            }

            return ValidateNode(BuildNode("Design", document), document.name);
        }

        /// <summary>
        /// 将绝对路径转换为 Unity 资源路径。
        /// </summary>
        /// <param name="absolutePath">绝对路径。</param>
        /// <returns>对应的 Unity 资源路径；如果路径不在 Assets 下则返回空字符串。</returns>
        public static string AbsolutePathToAssetPath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return string.Empty;
            }

            var normalizedAssetsPath = NormalizePath(Application.dataPath);
            var normalizedAbsolutePath = NormalizePath(absolutePath);
            if (normalizedAbsolutePath.Equals(normalizedAssetsPath, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets";
            }

            if (!normalizedAbsolutePath.StartsWith(normalizedAssetsPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return "Assets" + normalizedAbsolutePath.Substring(normalizedAssetsPath.Length);
        }

        private static DesignNode BuildNode(string className, UIDocument document)
        {
            var node = new DesignNode
            {
                ClassName = className,
                Document = document
            };

            var bindings = document.Bindings
                .Where(static binding => binding != null && !string.IsNullOrWhiteSpace(binding.Key) && binding.Target != null)
                .OrderBy(static binding => binding.Key, StringComparer.Ordinal)
                .ThenBy(static binding => GetHierarchyPath(binding.Target.transform), StringComparer.Ordinal)
                .ToArray();

            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                var childDocument = binding.Target.GetComponent<UIDocument>();
                if (childDocument != null && childDocument != document)
                {
                    node.Bindings.Add(new BindingInfo
                    {
                        Key = binding.Key,
                        PropertyName = GetPropertyName(binding.Key, "Design"),
                        IsChildDesign = true,
                        ChildClassName = GetDesignClassName(binding.Target.name),
                        ChildDocument = childDocument
                    });
                    continue;
                }

                var components = binding.Components
                    .Where(static component => component != null)
                    .OrderBy(static component => component.GetType().FullName, StringComparer.Ordinal)
                    .ToArray();

                for (var j = 0; j < components.Length; j++)
                {
                    var component = components[j];

                    node.Bindings.Add(new BindingInfo
                    {
                        Key = binding.Key,
                        PropertyName = GetPropertyName(binding.Key, component.GetType().Name),
                        ComponentTypeName = component.GetType().Name,
                        ComponentNamespace = component.GetType().Namespace
                    });
                }
            }

            return node;
        }

        private static string GenerateWindowShell(string windowClassName, string windowNamespace, string assetPath)
        {
            var sb = new StringBuilder();
            AppendGeneratedHeader(sb, assetPath);
            sb.AppendLine("using GameDeveloperKit.Runtime;");
            sb.AppendLine();
            AppendNamespaceOpen(sb, windowNamespace);
            sb.AppendLine($"public partial class {windowClassName} : UIWindow");
            sb.AppendLine("{");
            sb.AppendLine($"    protected override string ResolveAssetPath() => \"{EscapeString(assetPath)}\";");
            sb.AppendLine("}");
            AppendNamespaceClose(sb, windowNamespace);
            return sb.ToString();
        }

        private static string GenerateDesignFile(string windowClassName, string windowNamespace, DesignNode rootNode, string assetPath)
        {
            var namespaces = new HashSet<string>
            {
                "System",
                "GameDeveloperKit.Runtime",
                "UnityEngine"
            };

            CollectNamespaces(rootNode, namespaces);

            var sb = new StringBuilder();
            AppendGeneratedHeader(sb, assetPath);
            foreach (var ns in namespaces.OrderBy(static value => value))
            {
                sb.AppendLine($"using {ns};");
            }

            sb.AppendLine();
            AppendNamespaceOpen(sb, windowNamespace);
            sb.AppendLine($"public partial class {windowClassName}");
            sb.AppendLine("{");
            GenerateDesignClass(sb, rootNode, 1);
            sb.AppendLine("}");
            AppendNamespaceClose(sb, windowNamespace);
            return sb.ToString();
        }

        private static string GenerateDataFile(string windowClassName, string windowNamespace, string assetPath)
        {
            var sb = new StringBuilder();
            AppendGeneratedHeader(sb, assetPath);
            sb.AppendLine("using GameDeveloperKit.Runtime;");
            sb.AppendLine();
            AppendNamespaceOpen(sb, windowNamespace);
            sb.AppendLine($"public partial class {windowClassName}");
            sb.AppendLine("{");
            sb.AppendLine("    public partial class Data : UIDataBase");
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            AppendNamespaceClose(sb, windowNamespace);
            return sb.ToString();
        }

        private static string GenerateWindowPartial(string windowClassName, string windowNamespace)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using GameDeveloperKit.Runtime;");
            sb.AppendLine();
            AppendNamespaceOpen(sb, windowNamespace);
            sb.AppendLine($"public partial class {windowClassName}");
            sb.AppendLine("{");
            sb.AppendLine("}");
            AppendNamespaceClose(sb, windowNamespace);
            return sb.ToString();
        }

        private static string GenerateDesignPartial(string windowClassName, string windowNamespace)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            AppendNamespaceOpen(sb, windowNamespace);
            sb.AppendLine($"public partial class {windowClassName}");
            sb.AppendLine("{");
            sb.AppendLine("    public partial class Design");
            sb.AppendLine("    {");
            sb.AppendLine("        partial void Initialize(GameObject root)");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            AppendNamespaceClose(sb, windowNamespace);
            return sb.ToString();
        }

        private static string GenerateDataPartial(string windowClassName, string windowNamespace)
        {
            var sb = new StringBuilder();
            AppendNamespaceOpen(sb, windowNamespace);
            sb.AppendLine($"public partial class {windowClassName}");
            sb.AppendLine("{");
            sb.AppendLine("    public partial class Data");
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            AppendNamespaceClose(sb, windowNamespace);
            return sb.ToString();
        }

        private static void GenerateDesignClass(StringBuilder sb, DesignNode node, int indentLevel)
        {
            AppendLine(sb, indentLevel, $"public partial class {node.ClassName} : UIDesignBase");
            AppendLine(sb, indentLevel, "{");
            AppendLine(sb, indentLevel + 1, "private UIDocument _document;");

            foreach (var binding in node.Bindings.Where(static item => item.IsChildDesign))
            {
                AppendLine(sb, indentLevel + 1, $"private {binding.ChildClassName} _{ToCamelCase(binding.PropertyName)};");
            }

            AppendLine(sb, indentLevel + 1, "public UIDocument.Node this[string key] => _document[key];");

            foreach (var binding in node.Bindings)
            {
                if (binding.IsChildDesign)
                {
                    AppendLine(sb, indentLevel + 1, $"public {binding.ChildClassName} {binding.PropertyName} => _{ToCamelCase(binding.PropertyName)} ??= GetDesign<{binding.ChildClassName}>(\"{EscapeString(binding.Key)}\");");
                }
                else
                {
                    AppendLine(sb, indentLevel + 1, $"public {binding.ComponentTypeName} {binding.PropertyName} => this[\"{EscapeString(binding.Key)}\"].Get<{binding.ComponentTypeName}>();");
                }
            }

            AppendLine(sb, indentLevel + 1, "public override void Load(GameObject root)");
            AppendLine(sb, indentLevel + 1, "{");
            AppendLine(sb, indentLevel + 2, "base.Load(root);");
            AppendLine(sb, indentLevel + 2, "_document = Document;");
            AppendLine(sb, indentLevel + 2, "Initialize(root);");
            AppendLine(sb, indentLevel + 1, "}");
            AppendLine(sb, indentLevel + 1, "partial void Initialize(GameObject root);");
            AppendLine(sb, indentLevel + 1, "public override void Clear()");
            AppendLine(sb, indentLevel + 1, "{");

            foreach (var binding in node.Bindings.Where(static item => item.IsChildDesign))
            {
                AppendLine(sb, indentLevel + 2, $"_{ToCamelCase(binding.PropertyName)}?.Dispose();");
                AppendLine(sb, indentLevel + 2, $"_{ToCamelCase(binding.PropertyName)} = null;");
            }

            AppendLine(sb, indentLevel + 2, "_document = null;");
            AppendLine(sb, indentLevel + 2, "base.Clear();");
            AppendLine(sb, indentLevel + 1, "}");

            foreach (var binding in node.Bindings.Where(static item => item.IsChildDesign))
            {
                AppendLine(sb, indentLevel + 1, string.Empty);
                GenerateDesignClass(sb, BuildNode(binding.ChildClassName, binding.ChildDocument), indentLevel + 1);
            }

            AppendLine(sb, indentLevel, "}");
        }

        private static void CollectNamespaces(DesignNode node, HashSet<string> namespaces)
        {
            for (var i = 0; i < node.Bindings.Count; i++)
            {
                var binding = node.Bindings[i];
                if (!binding.IsChildDesign && !string.IsNullOrWhiteSpace(binding.ComponentNamespace))
                {
                    namespaces.Add(binding.ComponentNamespace);
                }

                if (binding.IsChildDesign && binding.ChildDocument != null)
                {
                    CollectNamespaces(BuildNode(binding.ChildClassName, binding.ChildDocument), namespaces);
                }
            }
        }

        private static string GetClassName(string name)
        {
            return EscapeIdentifier(ToPascalCase(TrimPrefix(name)));
        }

        private static string GetDesignClassName(string name)
        {
            return $"{GetClassName(name)}Design";
        }

        private static string GetPropertyName(string key, string componentType)
        {
            return EscapeIdentifier($"{GetPrefix(componentType)}_{ToPascalCase(TrimPrefix(key))}");
        }

        private static string GetPrefix(string componentType)
        {
            return componentType switch
            {
                "Button" => "Btn",
                "Image" => "Img",
                "RawImage" => "RawImg",
                "Text" => "Txt",
                "TextMeshProUGUI" => "Txt",
                "TMP_Text" => "Txt",
                "InputField" => "Input",
                "TMP_InputField" => "Input",
                "Toggle" => "Tgl",
                "Slider" => "Slider",
                "Dropdown" => "Dropdown",
                "TMP_Dropdown" => "Dropdown",
                "ScrollRect" => "Scroll",
                "CanvasGroup" => "Group",
                "RectTransform" => "Rect",
                "Design" => "Design",
                _ => ToPascalCase(componentType)
            };
        }

        private static string TrimPrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.StartsWith("b_", StringComparison.OrdinalIgnoreCase) || value.StartsWith("d_", StringComparison.OrdinalIgnoreCase)
                ? value.Substring(2)
                : value;
        }

        private static string ToPascalCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Generated";
            }

            var builder = new StringBuilder();
            var shouldUpper = true;
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (!char.IsLetterOrDigit(character))
                {
                    shouldUpper = true;
                    continue;
                }

                if (builder.Length == 0 && char.IsDigit(character))
                {
                    builder.Append('_');
                }

                builder.Append(shouldUpper ? char.ToUpperInvariant(character) : character);
                shouldUpper = false;
            }

            return builder.Length == 0 ? "Generated" : builder.ToString();
        }

        private static string ToCamelCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return char.ToLowerInvariant(value[0]) + value.Substring(1);
        }

        private static string EscapeString(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string EscapeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Generated";
            }

            if (!(char.IsLetter(value[0]) || value[0] == '_'))
            {
                value = "_" + value;
            }

            return CSharpKeywords.Contains(value) ? "_" + value : value;
        }

        private static string GetWindowClassName(UIDocument document)
        {
            return string.IsNullOrWhiteSpace(document.Generation.WindowClassName)
                ? GetClassName(document.gameObject.name)
                : EscapeIdentifier(document.Generation.WindowClassName);
        }

        private static string GetWindowNamespace(UIDocument document)
        {
            return document.Generation.WindowNamespace?.Trim() ?? string.Empty;
        }

        private static string GetOutputDirectoryPath(UIDocument document)
        {
            var path = document.Generation.OutputDirectoryPath?.Trim();
            return string.IsNullOrWhiteSpace(path) ? "Assets" : path.Replace('\\', '/');
        }

        private static string AssetPathToAbsolutePath(string assetPath)
        {
            var normalizedAssetPath = assetPath.Replace('\\', '/');
            if (normalizedAssetPath.Equals("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return Application.dataPath;
            }

            if (!normalizedAssetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Asset path '{assetPath}' must stay under Assets.");
            }

            return Path.Combine(Application.dataPath, normalizedAssetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
        }

        private static string WriteGeneratedFile(string outputDirectory, string outputDirectoryPath, string fileName, string content)
        {
            WriteFile(Path.Combine(outputDirectory, fileName), content);
            return $"{outputDirectoryPath.TrimEnd('/')}/{fileName}";
        }

        private static string ValidateNode(DesignNode node, string documentName)
        {
            var propertyNames = new HashSet<string>(StringComparer.Ordinal);
            var childClassNames = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < node.Bindings.Count; i++)
            {
                var binding = node.Bindings[i];
                if (!propertyNames.Add(binding.PropertyName))
                {
                    return $"UIDocument '{documentName}' generates duplicate property '{binding.PropertyName}' for binding '{binding.Key}'.";
                }

                if (binding.IsChildDesign)
                {
                    if (!childClassNames.Add(binding.ChildClassName))
                    {
                        return $"UIDocument '{documentName}' generates duplicate child design class '{binding.ChildClassName}'.";
                    }

                    var childValidationError = ValidateNode(BuildNode(binding.ChildClassName, binding.ChildDocument), binding.ChildDocument.name);
                    if (!string.IsNullOrWhiteSpace(childValidationError))
                    {
                        return childValidationError;
                    }
                }
            }

            return string.Empty;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var names = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        private static void AppendGeneratedHeader(StringBuilder sb, string assetPath)
        {
            sb.AppendLine("// Auto-generated by UIDocumentCodeGenerator.");
            sb.AppendLine($"// Source prefab: {assetPath}");
        }

        private static void AppendNamespaceOpen(StringBuilder sb, string windowNamespace)
        {
            if (string.IsNullOrWhiteSpace(windowNamespace))
            {
                return;
            }

            sb.AppendLine($"namespace {windowNamespace}");
            sb.AppendLine("{");
        }

        private static void AppendNamespaceClose(StringBuilder sb, string windowNamespace)
        {
            if (string.IsNullOrWhiteSpace(windowNamespace))
            {
                return;
            }

            sb.AppendLine("}");
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }

        private static void AppendLine(StringBuilder sb, int indentLevel, string text)
        {
            if (text.Length == 0)
            {
                sb.AppendLine();
                return;
            }

            sb.Append(' ', indentLevel * 4);
            sb.AppendLine(text);
        }

        private static void WriteFile(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            File.WriteAllText(path, content, Encoding.UTF8);
        }

        private static void WriteIfMissing(string path, string content)
        {
            if (!File.Exists(path))
            {
                WriteFile(path, content);
            }
        }
    }
}
