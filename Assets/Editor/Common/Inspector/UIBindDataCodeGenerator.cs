using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using GameDeveloperKit.UI;

namespace GameDeveloperKit.Editor
{
    /// <summary>
    /// UIBindData 代码生成器
    /// </summary>
    public static class UIBindDataCodeGenerator
    {
        public class BindingInfo
        {
            public string GameObjectName;
            public string ComponentType;
            public string ComponentNamespace;
            public string PropertyName;
            public bool IsChildView;
            public string ChildViewClassName;
        }
        
        public class BindDataNode
        {
            public UIBindData BindData;
            public string ClassName;
            public List<BindDataNode> Children = new List<BindDataNode>();
            public List<BindingInfo> Bindings = new List<BindingInfo>();
        }
        
        public static BindDataNode CollectBindDataTree(UIBindData rootBindData)
        {
            var root = new BindDataNode
            {
                BindData = rootBindData,
                ClassName = GetClassNameFromGameObject(rootBindData.gameObject)
            };
            CollectBindDataRecursive(rootBindData.gameObject, rootBindData, root);
            return root;
        }
        
        private static void CollectBindDataRecursive(GameObject current, UIBindData ownerBindData, BindDataNode ownerNode)
        {
            foreach (Transform child in current.transform)
            {
                var go = child.gameObject;
                var childBindData = go.GetComponent<UIBindData>();
                
                if (childBindData != null && childBindData != ownerBindData)
                {
                    var childNode = new BindDataNode
                    {
                        BindData = childBindData,
                        ClassName = GetClassNameFromGameObject(go)
                    };
                    ownerNode.Children.Add(childNode);
                    CollectBindDataRecursive(go, childBindData, childNode);
                }
                else
                {
                    CollectBindDataRecursive(go, ownerBindData, ownerNode);
                }
            }
        }
        
        private static string GetClassNameFromGameObject(GameObject go)
        {
            string baseName = go.name.Replace(" ", "");
            if (baseName.StartsWith("b_") || baseName.StartsWith("d_"))
                baseName = baseName.Substring(2);
            if (baseName.EndsWith("View") || baseName.EndsWith("Form"))
                baseName = baseName.Substring(0, baseName.Length - 4);
            return ToPascalCase(baseName);
        }
        
        public static void CollectBindings(BindDataNode node)
        {
            var bindData = node.BindData;
            var bindings = bindData.GetBindings();
            var result = new List<BindingInfo>();
            
            foreach (var binding in bindings)
            {
                if (binding.target == null || binding.components == null || binding.components.Count == 0)
                    continue;
                
                string goName = binding.target.name;
                var childBindData = binding.target.GetComponent<UIBindData>();
                bool isChildView = childBindData != null && childBindData != bindData;
                
                foreach (var comp in binding.components)
                {
                    if (comp == null) continue;
                    
                    var compType = comp.GetType();
                    result.Add(new BindingInfo
                    {
                        GameObjectName = goName,
                        ComponentType = compType.Name,
                        ComponentNamespace = compType.Namespace,
                        PropertyName = GeneratePropertyName(compType.Name, goName),
                        IsChildView = isChildView,
                        ChildViewClassName = isChildView ? GetClassNameFromGameObject(binding.target) + "View" : null
                    });
                }
            }
            node.Bindings = result;
        }
        
        public static List<string> GenerateAllFiles(string savePath, BindDataNode rootNode, string uiAssetPath, bool isRootForm)
        {
            var generatedFiles = new List<string>();
            GenerateFilesRecursive(savePath, rootNode, uiAssetPath, isRootForm, generatedFiles);
            return generatedFiles;
        }
        
        private static void GenerateFilesRecursive(string savePath, BindDataNode node, string uiAssetPath, bool isRoot, List<string> generatedFiles)
        {
            CollectBindings(node);
            
            string dataClassName = node.ClassName + "Data";
            string viewClassName = node.ClassName + "View";
            string formClassName = node.ClassName + "Form";
            
            // 生成Data文件
            GenerateDataFile(savePath, dataClassName);
            generatedFiles.Add($"{dataClassName}.cs");
            
            // 生成View文件（只有根节点的View需要UIForm属性）
            if (isRoot)
                GenerateViewFile(savePath, viewClassName, node, uiAssetPath, node.BindData.UILayer, node.BindData.UIMode);
            else
                GenerateViewFile(savePath, viewClassName, node, null, EUILayer.Window, EUIMode.Normal);
            generatedFiles.Add($"{viewClassName}.cs");
            
            // 所有节点都生成Form文件
            GenerateFormFile(savePath, formClassName, dataClassName, viewClassName);
            generatedFiles.Add($"{formClassName}.cs");
            
            foreach (var child in node.Children)
            {
                GenerateFilesRecursive(savePath, child, uiAssetPath, false, generatedFiles);
            }
        }
        
        public static void GenerateDataFile(string path, string className)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// 此代码由 UIBindData 自动生成");
            sb.AppendLine("// 生成时间: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("using GameFramework.UI;");
            sb.AppendLine();
            sb.AppendLine($"public class {className} : UIDataBase");
            sb.AppendLine("{");
            sb.AppendLine("    public override void OnClearup()");
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            File.WriteAllText(Path.Combine(path, className + ".cs"), sb.ToString());
        }
        
        public static void GenerateViewFile(string path, string className, BindDataNode node, string uiAssetPath, EUILayer layer, EUIMode mode)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// 此代码由 UIBindData 自动生成");
            sb.AppendLine("// 生成时间: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            
            var namespaces = new HashSet<string> { "UnityEngine", "UnityEngine.UI", "GameFramework.UI" };
            foreach (var binding in node.Bindings)
                if (!string.IsNullOrEmpty(binding.ComponentNamespace))
                    namespaces.Add(binding.ComponentNamespace);
            
            foreach (var ns in namespaces.OrderBy(n => n))
                sb.AppendLine($"using {ns};");
            
            sb.AppendLine();
            
            // 如果有uiAssetPath，添加UIForm属性
            if (!string.IsNullOrEmpty(uiAssetPath))
                sb.AppendLine($"[UIForm(\"{uiAssetPath}\", EUILayer.{layer}, EUIMode.{mode}, true)]");
            
            sb.AppendLine($"public class {className} : UIViewBase");
            sb.AppendLine("{");
            
            // 子View属性
            foreach (var child in node.Children)
                sb.AppendLine($"    public {child.ClassName}View {child.ClassName} {{ get; private set; }}");
            
            // 组件属性
            foreach (var binding in node.Bindings)
            {
                if (binding.IsChildView) continue;
                sb.AppendLine($"    public {binding.ComponentType} {binding.PropertyName} {{ get; private set; }}");
            }
            
            sb.AppendLine();
            sb.AppendLine("    public override void OnStartup()");
            sb.AppendLine("    {");
            
            // 初始化子View
            foreach (var child in node.Children)
            {
                sb.AppendLine($"        {child.ClassName} = new {child.ClassName}View();");
                sb.AppendLine($"        {child.ClassName}.Startup(Get<RectTransform>(\"{child.BindData.gameObject.name}\").gameObject);");
            }
            
            // 初始化组件
            foreach (var binding in node.Bindings)
            {
                if (binding.IsChildView) continue;
                sb.AppendLine($"        {binding.PropertyName} = Get<{binding.ComponentType}>(\"{binding.GameObjectName}\");");
            }
            
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public override void OnClearup()");
            sb.AppendLine("    {");
            
            foreach (var child in node.Children)
                sb.AppendLine($"        {child.ClassName}?.OnClearup();");
            
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            File.WriteAllText(Path.Combine(path, className + ".cs"), sb.ToString());
        }
        
        public static void GenerateFormFile(string path, string className, string dataClassName, string viewClassName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// 此代码由 UIBindData 自动生成");
            sb.AppendLine("// 生成时间: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("using GameFramework.UI;");
            sb.AppendLine();
            sb.AppendLine($"public class {className} : UIFormBase<{dataClassName}, {viewClassName}>");
            sb.AppendLine("{");
            sb.AppendLine("    protected override void OnStartup(params object[] args)");
            sb.AppendLine("    {");
            sb.AppendLine("        base.OnStartup(args);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    protected override void OnClearup()");
            sb.AppendLine("    {");
            sb.AppendLine("        base.OnClearup();");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            File.WriteAllText(Path.Combine(path, className + ".cs"), sb.ToString());
        }
        
        public static string GeneratePropertyName(string componentType, string goName)
        {
            string baseName = goName.StartsWith("b_") || goName.StartsWith("d_") ? goName.Substring(2) : goName;
            return $"{GetPropertyPrefix(componentType)}_{ToPascalCase(baseName)}";
        }
        
        public static string GetPropertyPrefix(string componentType) => componentType switch
        {
            "Button" => "Btn", "Text" => "Txt", "Image" => "Img", "RawImage" => "Raw",
            "InputField" or "TMP_InputField" => "Ipt", "TMP_Text" or "TextMeshProUGUI" => "Txt",
            "Slider" => "Sld", "Toggle" => "Tgl", "Dropdown" or "TMP_Dropdown" => "Drp",
            "ScrollRect" => "Scr", "RectTransform" => "Rect", "Transform" => "Trans", "CanvasGroup" => "Cg",
            _ => componentType
        };
        
        public static string ToPascalCase(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            var sb = new StringBuilder();
            foreach (var part in str.Split('_'))
                if (!string.IsNullOrEmpty(part))
                    sb.Append(char.ToUpper(part[0])).Append(part.Length > 1 ? part.Substring(1) : "");
            return sb.ToString();
        }
    }
}
