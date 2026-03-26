using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameDeveloperKit.Editor
{
    /// <summary>
    /// UIDocument 自定义编辑器，用于扫描绑定、配置生成参数并触发代码生成。
    /// </summary>
    [CustomEditor(typeof(UIDocument), true)]
    public sealed class UIDocumentEditor : UnityEditor.Editor
    {
        private SerializedProperty _fullScreenBackgroundProperty;
        private SerializedProperty _bindingsProperty;
        private SerializedProperty _generationProperty;
        private string _lastValidationMessage;

        private void OnEnable()
        {
            _fullScreenBackgroundProperty = serializedObject.FindProperty("fullScreenBackground");
            _bindingsProperty = serializedObject.FindProperty("bindings");
            _generationProperty = serializedObject.FindProperty("generation");
        }

        /// <summary>
        /// 绘制 UIDocument 的自定义检视面板。
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawMetadataOwnershipNotice();
            EditorGUILayout.PropertyField(_fullScreenBackgroundProperty);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_bindingsProperty, true);
            EditorGUILayout.Space();
            DrawGenerationSettings();
            DrawValidationHelpBox();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scan Bindings"))
                {
                    ScanBindings();
                }

                if (GUILayout.Button("Generate Code"))
                {
                    GenerateCode();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Current From Prefab"))
                {
                    GenerateCurrentPrefabSelection();
                }

                if (GUILayout.Button("Generate All Prefabs"))
                {
                    UIDocumentCodeGenerationMenu.GenerateAllPrefabs();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void ScanBindings()
        {
            serializedObject.Update();
            _bindingsProperty.ClearArray();

            var document = (UIDocument)target;
            var root = document.gameObject;
            var results = new List<(string key, GameObject target, List<Component> components)>();
            ScanRecursive(root.transform, document, results);

            for (var i = 0; i < results.Count; i++)
            {
                _bindingsProperty.InsertArrayElementAtIndex(i);
                var element = _bindingsProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("key").stringValue = results[i].key;
                element.FindPropertyRelative("target").objectReferenceValue = results[i].target;

                var componentsProperty = element.FindPropertyRelative("components");
                componentsProperty.ClearArray();
                for (var j = 0; j < results[i].components.Count; j++)
                {
                    componentsProperty.InsertArrayElementAtIndex(j);
                    componentsProperty.GetArrayElementAtIndex(j).objectReferenceValue = results[i].components[j];
                }
            }

            serializedObject.ApplyModifiedProperties();
            document.Rebuild();
            EditorUtility.SetDirty(document);
            AssetDatabase.SaveAssets();
            _lastValidationMessage = ValidateDocument(document);
        }

        private void GenerateCode()
        {
            var document = (UIDocument)target;
            _lastValidationMessage = ValidateDocument(document);
            if (!string.IsNullOrWhiteSpace(_lastValidationMessage))
            {
                EditorUtility.DisplayDialog("Generate UI Code", _lastValidationMessage, "OK");
                return;
            }

            var assetPath = ResolveAssetPath(document.gameObject);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                EditorUtility.DisplayDialog("Generate UI Code", "Unable to resolve prefab asset path.", "OK");
                return;
            }

            var generatedFiles = UIDocumentCodeGenerator.GenerateAllFiles(document, assetPath);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Generate UI Code", $"UI code generated successfully.\n{string.Join("\n", generatedFiles.Select(static path => $"- {path}"))}", "OK");
        }

        private static void ScanRecursive(Transform current, UIDocument owner, List<(string key, GameObject target, List<Component> components)> results)
        {
            for (var i = 0; i < current.childCount; i++)
            {
                var child = current.GetChild(i);
                var childObject = child.gameObject;
                var childDocument = childObject.GetComponent<UIDocument>();

                if (childDocument != null && childDocument != owner)
                {
                    var childKey = NormalizeKey(childObject.name, "d_");
                    results.Add((childKey, childObject, CollectComponents(childObject, true)));
                    continue;
                }

                if (IsBindableName(childObject.name))
                {
                    results.Add((childObject.name, childObject, CollectComponents(childObject, false)));
                }

                ScanRecursive(child, owner, results);
            }
        }

        private static List<Component> CollectComponents(GameObject target, bool childDocument)
        {
            var results = new List<Component>();
            var rectTransform = target.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                results.Add(rectTransform);
            }

            if (childDocument)
            {
                return results;
            }

            TryAdd(results, target, typeof(CanvasGroup));
            TryAdd(results, target, typeof(UnityEngine.UI.Button));
            TryAdd(results, target, typeof(UnityEngine.UI.Image));
            TryAdd(results, target, typeof(UnityEngine.UI.RawImage));
            TryAdd(results, target, typeof(UnityEngine.UI.Text));
            TryAdd(results, target, typeof(UnityEngine.UI.InputField));
            TryAdd(results, target, typeof(UnityEngine.UI.Toggle));
            TryAdd(results, target, typeof(UnityEngine.UI.Slider));
            TryAdd(results, target, typeof(UnityEngine.UI.Dropdown));
            TryAdd(results, target, typeof(UnityEngine.UI.ScrollRect));

            var allComponents = target.GetComponents<Component>();
            for (var i = 0; i < allComponents.Length; i++)
            {
                var component = allComponents[i];
                if (component == null)
                {
                    continue;
                }

                var typeName = component.GetType().Name;
                if (typeName is "TMP_Text" or "TextMeshProUGUI" or "TMP_InputField" or "TMP_Dropdown")
                {
                    if (!results.Contains(component))
                    {
                        results.Add(component);
                    }
                }
            }

            return results;
        }

        private static void TryAdd(ICollection<Component> components, GameObject target, Type type)
        {
            var component = target.GetComponent(type);
            if (component != null)
            {
                components.Add(component);
            }
        }

        private static bool IsBindableName(string name)
        {
            return !string.IsNullOrWhiteSpace(name)
                && (name.StartsWith("b_", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("d_", StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeKey(string name, string prefix)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return prefix + "unnamed";
            }

            return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? name : prefix + name;
        }

        /// <summary>
        /// 解析指定对象对应的预制体资源路径。
        /// </summary>
        /// <param name="root">预制体根对象。</param>
        /// <returns>预制体资源路径；如果无法解析则返回空字符串。</returns>
        internal static string ResolveAssetPath(GameObject root)
        {
            var assetPath = AssetDatabase.GetAssetPath(root);
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                return assetPath;
            }

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.prefabContentsRoot == root)
            {
                return prefabStage.assetPath;
            }

            var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(root);
            return prefabSource == null ? string.Empty : AssetDatabase.GetAssetPath(prefabSource);
        }

        private void DrawMetadataOwnershipNotice()
        {
            EditorGUILayout.HelpBox($"代码元数据由 UIWindowAttribute 持有：{UIMetadataOwnership.GetCodeOwnedSummary()}。\nPrefab 元数据由 UIDocument 持有：{UIMetadataOwnership.GetPrefabOwnedSummary()}。", MessageType.Info);
        }

        private void DrawGenerationSettings()
        {
            EditorGUILayout.LabelField("Code Generation", EditorStyles.boldLabel);

            var outputDirectoryPathProperty = _generationProperty.FindPropertyRelative("outputDirectoryPath");
            var windowClassNameProperty = _generationProperty.FindPropertyRelative("windowClassName");
            var windowNamespaceProperty = _generationProperty.FindPropertyRelative("windowNamespace");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(outputDirectoryPathProperty, new GUIContent("Output Directory"));
                if (GUILayout.Button("Select", GUILayout.Width(72f)))
                {
                    SelectOutputDirectory(outputDirectoryPathProperty);
                }
            }

            EditorGUILayout.PropertyField(windowClassNameProperty, new GUIContent("Window Class Name"));
            EditorGUILayout.PropertyField(windowNamespaceProperty, new GUIContent("Namespace"));
        }

        private void DrawValidationHelpBox()
        {
            var document = (UIDocument)target;
            var validationMessage = ValidateDocument(document);
            _lastValidationMessage = validationMessage;
            if (string.IsNullOrWhiteSpace(validationMessage))
            {
                EditorGUILayout.HelpBox("UIDocument scan and generation settings are valid.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(validationMessage, MessageType.Warning);
        }

        private static void SelectOutputDirectory(SerializedProperty outputDirectoryPathProperty)
        {
            var selectedPath = EditorUtility.OpenFolderPanel("Select UI code output directory", Application.dataPath, string.Empty);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            var assetRelativePath = UIDocumentCodeGenerator.AbsolutePathToAssetPath(selectedPath);
            if (string.IsNullOrWhiteSpace(assetRelativePath))
            {
                EditorUtility.DisplayDialog("Generate UI Code", "Output directory must be inside Assets.", "OK");
                return;
            }

            outputDirectoryPathProperty.stringValue = assetRelativePath;
        }

        /// <summary>
        /// 校验指定 UI 文档是否满足扫描与代码生成要求。
        /// </summary>
        /// <param name="document">目标 UI 文档。</param>
        /// <returns>校验错误信息；如果校验通过则返回空字符串。</returns>
        internal static string ValidateDocument(UIDocument document)
        {
            if (document == null)
            {
                return "UIDocument target is missing.";
            }

            var bindingValidationError = document.GetBindingValidationError();
            if (!string.IsNullOrWhiteSpace(bindingValidationError))
            {
                return bindingValidationError;
            }

            var generationValidationError = document.GetGenerationValidationError();
            if (!string.IsNullOrWhiteSpace(generationValidationError))
            {
                return generationValidationError;
            }

            var assetPath = ResolveAssetPath(document.gameObject);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return $"UIDocument '{document.name}' must belong to a prefab asset before code generation.";
            }

            return UIDocumentCodeGenerator.ValidateDocument(document, assetPath);
        }

        private void GenerateCurrentPrefabSelection()
        {
            if (target is not UIDocument document)
            {
                return;
            }

            UIDocumentCodeGenerationMenu.GenerateDocument(document);
        }
    }

    /// <summary>
    /// UIDocument 代码生成菜单命令集合。
    /// </summary>
    internal static class UIDocumentCodeGenerationMenu
    {
        /// <summary>
        /// 为当前选中的 UIDocument 生成代码。
        /// </summary>
        [MenuItem("Tools/GameDeveloperKit/UI/Generate Current UIDocument", priority = 2000)]
        public static void GenerateCurrent()
        {
            var document = ResolveSelectedDocument();
            if (document == null)
            {
                EditorUtility.DisplayDialog("Generate UI Code", "Select a prefab root or GameObject with UIDocument first.", "OK");
                return;
            }

            GenerateDocument(document);
        }

        /// <summary>
        /// 为项目内全部 UIDocument 预制体生成代码。
        /// </summary>
        [MenuItem("Tools/GameDeveloperKit/UI/Generate All UIDocuments", priority = 2001)]
        public static void GenerateAllPrefabs()
        {
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            var generatedFiles = new List<string>();

            for (var i = 0; i < prefabGuids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                {
                    continue;
                }

                var document = prefab.GetComponent<UIDocument>();
                if (document == null)
                {
                    continue;
                }

                var validationMessage = UIDocumentEditor.ValidateDocument(document);
                if (!string.IsNullOrWhiteSpace(validationMessage))
                {
                    throw new InvalidOperationException(validationMessage);
                }

                generatedFiles.AddRange(UIDocumentCodeGenerator.GenerateAllFiles(document, assetPath));
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Generate UI Code", generatedFiles.Count == 0 ? "No UIDocument prefabs found." : $"Generated {generatedFiles.Count} files.", "OK");
        }

        /// <summary>
        /// 为指定的 UIDocument 生成代码。
        /// </summary>
        /// <param name="document">目标 UI 文档。</param>
        public static void GenerateDocument(UIDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var validationMessage = UIDocumentEditor.ValidateDocument(document);
            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                EditorUtility.DisplayDialog("Generate UI Code", validationMessage, "OK");
                return;
            }

            var assetPath = UIDocumentEditor.ResolveAssetPath(document.gameObject);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                EditorUtility.DisplayDialog("Generate UI Code", "Unable to resolve prefab asset path.", "OK");
                return;
            }

            var generatedFiles = UIDocumentCodeGenerator.GenerateAllFiles(document, assetPath);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Generate UI Code", $"Generated {generatedFiles.Count} files.\n{string.Join("\n", generatedFiles)}", "OK");
        }

        [MenuItem("Tools/GameDeveloperKit/UI/Generate Current UIDocument", true)]
        private static bool ValidateGenerateCurrent()
        {
            return ResolveSelectedDocument() != null;
        }

        private static UIDocument ResolveSelectedDocument()
        {
            if (Selection.activeGameObject == null)
            {
                return null;
            }

            return Selection.activeGameObject.GetComponent<UIDocument>();
        }
    }
}
