using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using GameDeveloperKit.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameDeveloperKit.Editor
{
    [CustomEditor(typeof(UIBindData))]
    public class UIBindDataEditor : UnityEditor.Editor
    {
        private SerializedProperty _bindingsProperty;
        private SerializedProperty _fullScreenBackgroundProperty;
        private SerializedProperty _uiLayerProperty;
        private SerializedProperty _uiModeProperty;
        private VisualElement _root;
        private VisualElement _bindingsList;
        private Label _titleLabel;
        
        private void OnEnable()
        {
            _bindingsProperty = serializedObject.FindProperty("bindings");
            _fullScreenBackgroundProperty = serializedObject.FindProperty("fullScreenBackground");
            _uiLayerProperty = serializedObject.FindProperty("uiLayer");
            _uiModeProperty = serializedObject.FindProperty("uiMode");
        }
        
        public override VisualElement CreateInspectorGUI()
        {
            _root = new VisualElement();
            
            var styleSheet = EditorAssetLoader.LoadStyleSheet("Common/Style/EditorCommonStyle.uss");
            if (styleSheet != null)
                _root.styleSheets.Add(styleSheet);
            
            CreateBindingsSection();
            return _root;
        }
        
        private void CreateBindingsSection()
        {
            var section = new VisualElement();
            section.AddToClassList("info-card");
            
            // 标题行
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 12;
            
            _titleLabel = new Label($"组件绑定 ({_bindingsProperty.arraySize})");
            _titleLabel.AddToClassList("card-title");
            _titleLabel.style.marginBottom = 0;
            _titleLabel.style.flexGrow = 1;
            titleRow.Add(_titleLabel);
            
            var refreshBtn = new Button(() => AutoScanBindings());
            refreshBtn.text = "刷新";
            refreshBtn.AddToClassList("btn");
            refreshBtn.AddToClassList("btn-secondary");
            refreshBtn.AddToClassList("btn-sm");
            refreshBtn.style.marginRight = 4;
            titleRow.Add(refreshBtn);
            
            var generateCodeBtn = new Button(() => GenerateCode());
            generateCodeBtn.text = "生成代码";
            generateCodeBtn.AddToClassList("btn");
            generateCodeBtn.AddToClassList("btn-primary");
            generateCodeBtn.AddToClassList("btn-sm");
            titleRow.Add(generateCodeBtn);
            
            section.Add(titleRow);
            
            // UI Form 设置
            var formSettingsContainer = new VisualElement();
            formSettingsContainer.style.marginBottom = 8;
            
            // Layer设置
            var layerContainer = new VisualElement();
            layerContainer.style.flexDirection = FlexDirection.Row;
            layerContainer.style.alignItems = Align.Center;
            layerContainer.style.marginBottom = 4;
            
            var layerLabel = new Label("UI Layer");
            layerLabel.style.minWidth = 60;
            layerLabel.style.marginRight = 8;
            layerContainer.Add(layerLabel);
            
            var layerField = new PropertyField(_uiLayerProperty);
            layerField.label = "";
            layerField.style.flexGrow = 1;
            layerField.Bind(serializedObject);
            layerContainer.Add(layerField);
            
            formSettingsContainer.Add(layerContainer);
            
            // Mode设置
            var modeContainer = new VisualElement();
            modeContainer.style.flexDirection = FlexDirection.Row;
            modeContainer.style.alignItems = Align.Center;
            modeContainer.style.marginBottom = 4;
            
            var modeLabel = new Label("UI Mode");
            modeLabel.style.minWidth = 60;
            modeLabel.style.marginRight = 8;
            modeContainer.Add(modeLabel);
            
            var modeField = new PropertyField(_uiModeProperty);
            modeField.label = "";
            modeField.style.flexGrow = 1;
            modeField.Bind(serializedObject);
            modeContainer.Add(modeField);
            
            formSettingsContainer.Add(modeContainer);
            
            section.Add(formSettingsContainer);
            
            // 全屏背景设置
            var bgContainer = new VisualElement();
            bgContainer.style.flexDirection = FlexDirection.Row;
            bgContainer.style.alignItems = Align.Center;
            bgContainer.style.marginBottom = 8;
            
            var bgLabel = new Label("全屏背景");
            bgLabel.style.minWidth = 60;
            bgLabel.style.marginRight = 8;
            bgContainer.Add(bgLabel);
            
            var bgField = new PropertyField(_fullScreenBackgroundProperty);
            bgField.label = "";
            bgField.style.flexGrow = 1;
            bgField.Bind(serializedObject);
            bgContainer.Add(bgField);
            
            section.Add(bgContainer);
            
            _bindingsList = new VisualElement();
            _bindingsList.AddToClassList("card-content");
            section.Add(_bindingsList);
            
            RefreshBindingsList();
            _root.Add(section);
        }
        
        private void RefreshBindingsList()
        {
            _bindingsList.Clear();
            serializedObject.Update();
            
            if (_bindingsProperty.arraySize == 0)
            {
                var empty = new Label("暂无绑定，点击「刷新」扫描组件");
                empty.style.color = new Color(0.5f, 0.5f, 0.5f);
                empty.style.fontSize = 12;
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                empty.style.paddingTop = 20;
                empty.style.paddingBottom = 20;
                _bindingsList.Add(empty);
                return;
            }
            
            for (int i = 0; i < _bindingsProperty.arraySize; i++)
            {
                var element = CreateBindingElement(i);
                _bindingsList.Add(element);
            }
            
            _titleLabel.text = $"组件绑定 ({_bindingsProperty.arraySize})";
        }
        
        private VisualElement CreateBindingElement(int index)
        {
            var bindingProp = _bindingsProperty.GetArrayElementAtIndex(index);
            var targetProp = bindingProp.FindPropertyRelative("target");
            var componentsProp = bindingProp.FindPropertyRelative("components");
            
            var targetGo = targetProp.objectReferenceValue as GameObject;
            string goName = targetGo != null ? targetGo.name : "(无目标)";
            bool isChildView = targetGo != null && targetGo.GetComponent<UIBindData>() != null 
                && targetGo.GetComponent<UIBindData>() != target;
            
            var container = new VisualElement();
            container.style.marginBottom = 4;
            container.style.paddingLeft = 8;
            container.style.paddingRight = 8;
            container.style.paddingTop = 4;
            container.style.paddingBottom = 4;
            container.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            
            // 图标
            var icon = new Label(isChildView ? "◆" : "●");
            icon.style.fontSize = 10;
            icon.style.marginRight = 6;
            icon.style.color = isChildView ? new Color(0.4f, 0.8f, 1f) : new Color(0.6f, 0.8f, 0.6f);
            row.Add(icon);
            
            // 名称
            var nameLabel = new Label(goName);
            nameLabel.style.flexGrow = 1;
            nameLabel.style.fontSize = 12;
            if (isChildView)
            {
                nameLabel.style.color = new Color(0.4f, 0.8f, 1f);
                nameLabel.text = $"{goName} (子View)";
            }
            row.Add(nameLabel);
            
            // 组件数量
            var countLabel = new Label($"{componentsProp.arraySize} 个组件");
            countLabel.style.fontSize = 10;
            countLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            countLabel.style.marginRight = 8;
            row.Add(countLabel);
            
            // 删除按钮
            var deleteBtn = new Button(() => {
                _bindingsProperty.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                RefreshBindingsList();
            });
            deleteBtn.text = "×";
            deleteBtn.style.width = 20;
            deleteBtn.style.height = 20;
            deleteBtn.style.fontSize = 14;
            deleteBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            deleteBtn.style.backgroundColor = Color.clear;
            deleteBtn.style.borderTopWidth = 0;
            deleteBtn.style.borderBottomWidth = 0;
            deleteBtn.style.borderLeftWidth = 0;
            deleteBtn.style.borderRightWidth = 0;
            row.Add(deleteBtn);
            
            container.Add(row);
            
            // 组件列表
            if (componentsProp.arraySize > 0)
            {
                var compList = new VisualElement();
                compList.style.marginTop = 4;
                compList.style.paddingLeft = 16;
                
                for (int i = 0; i < componentsProp.arraySize; i++)
                {
                    var comp = componentsProp.GetArrayElementAtIndex(i).objectReferenceValue as Component;
                    if (comp == null) continue;
                    
                    var compLabel = new Label($"• {comp.GetType().Name}");
                    compLabel.style.fontSize = 10;
                    compLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                    compList.Add(compLabel);
                }
                container.Add(compList);
            }
            
            return container;
        }
        
        private void AutoScanBindings()
        {
            var bindData = target as UIBindData;
            if (bindData == null) return;
            
            serializedObject.Update();
            _bindingsProperty.ClearArray();
            
            var root = bindData.gameObject;
            var childUIBindDatas = new HashSet<UIBindData>();
            CollectChildUIBindDatas(root, bindData, childUIBindDatas);
            
            ScanChildrenRecursive(root, bindData, childUIBindDatas);
            
            serializedObject.ApplyModifiedProperties();
            RefreshBindingsList();
        }
        
        private void CollectChildUIBindDatas(GameObject current, UIBindData owner, HashSet<UIBindData> result)
        {
            foreach (Transform child in current.transform)
            {
                var childBindData = child.GetComponent<UIBindData>();
                if (childBindData != null && childBindData != owner)
                {
                    result.Add(childBindData);
                }
                else
                {
                    CollectChildUIBindDatas(child.gameObject, owner, result);
                }
            }
        }
        
        private void ScanChildrenRecursive(GameObject current, UIBindData owner, HashSet<UIBindData> childUIBindDatas)
        {
            foreach (Transform child in current.transform)
            {
                var go = child.gameObject;
                var childBindData = go.GetComponent<UIBindData>();
                
                if (childBindData != null && childBindData != owner)
                {
                    // 子UIBindData节点，添加为子View引用
                    AddBinding(go, new List<Component> { go.GetComponent<RectTransform>() });
                    continue;
                }
                
                // 检查是否需要绑定
                if (go.name.StartsWith("b_") || go.name.StartsWith("d_"))
                {
                    var components = GetBindableComponents(go);
                    if (components.Count > 0)
                        AddBinding(go, components);
                }
                
                ScanChildrenRecursive(go, owner, childUIBindDatas);
            }
        }
        
        private void AddBinding(GameObject go, List<Component> components)
        {
            int newIndex = _bindingsProperty.arraySize;
            _bindingsProperty.InsertArrayElementAtIndex(newIndex);
            var newBinding = _bindingsProperty.GetArrayElementAtIndex(newIndex);
            newBinding.FindPropertyRelative("target").objectReferenceValue = go;
            
            var componentsProp = newBinding.FindPropertyRelative("components");
            componentsProp.ClearArray();
            for (int i = 0; i < components.Count; i++)
            {
                componentsProp.InsertArrayElementAtIndex(i);
                componentsProp.GetArrayElementAtIndex(i).objectReferenceValue = components[i];
            }
        }
        
        private List<Component> GetBindableComponents(GameObject go)
        {
            var result = new List<Component>();
            
            // 标准UI组件类型
            var standardTypes = new System.Type[]
            {
                typeof(UnityEngine.UI.Button),
                typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.RawImage),
                typeof(UnityEngine.UI.Text),
                typeof(UnityEngine.UI.InputField),
                typeof(UnityEngine.UI.Slider),
                typeof(UnityEngine.UI.Toggle),
                typeof(UnityEngine.UI.Dropdown),
                typeof(UnityEngine.UI.ScrollRect),
                typeof(RectTransform),
                typeof(CanvasGroup)
            };
            
            foreach (var type in standardTypes)
            {
                var comp = go.GetComponent(type);
                if (comp != null)
                    result.Add(comp);
            }
            
            // TMPro组件（通过字符串查找，避免直接引用）
            var tmpTypeNames = new[] { "TMP_Text", "TextMeshProUGUI", "TMP_InputField", "TMP_Dropdown" };
            foreach (var typeName in tmpTypeNames)
            {
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == typeName)
                        result.Add(comp);
                }
            }
            
            // 如果没有找到特定组件，至少添加RectTransform
            if (result.Count == 0)
            {
                var rect = go.GetComponent<RectTransform>();
                if (rect != null)
                    result.Add(rect);
            }
            
            return result;
        }
        
        private void GenerateCode()
        {
            var bindData = target as UIBindData;
            if (bindData == null) return;
            
            serializedObject.Update();
            
            if (_bindingsProperty.arraySize == 0)
            {
                EditorUtility.DisplayDialog("提示", "当前没有任何绑定，无法生成代码", "确定");
                return;
            }
            
            string savePath = EditorUtility.SaveFolderPanel("选择代码保存路径", "Assets", "");
            if (string.IsNullOrEmpty(savePath)) return;
            
            if (!savePath.StartsWith(Application.dataPath))
            {
                EditorUtility.DisplayDialog("错误", "保存路径必须在Assets文件夹下", "确定");
                return;
            }
            
            var rootNode = UIBindDataCodeGenerator.CollectBindDataTree(bindData);
            string uiAssetPath = GetUIAssetPath(bindData.gameObject);
            var generatedFiles = UIBindDataCodeGenerator.GenerateAllFiles(savePath, rootNode, uiAssetPath, true);
            
            AssetDatabase.Refresh();
            
            string relativePath = "Assets" + savePath.Substring(Application.dataPath.Length);
            
            if (generatedFiles.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "代码生成完成",
                    $"已成功生成以下文件到 {relativePath}：\n\n{string.Join("\n", generatedFiles.Select(f => "- " + f))}\n\n共生成 {generatedFiles.Count} 个文件。",
                    "确定");
            }
        }
        
        private string GetUIAssetPath(GameObject go)
        {
            string prefabPath = AssetDatabase.GetAssetPath(go);
            
            if (string.IsNullOrEmpty(prefabPath))
            {
                var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage != null && prefabStage.prefabContentsRoot == go)
                    prefabPath = prefabStage.assetPath;
            }
            
            if (string.IsNullOrEmpty(prefabPath))
            {
                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (prefabAsset != null)
                    prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
            }
            
            if (string.IsNullOrEmpty(prefabPath))
                return go.name;
            
            const string resourcesFolder = "/Resources/";
            int resourcesIndex = prefabPath.IndexOf(resourcesFolder, System.StringComparison.OrdinalIgnoreCase);
            if (resourcesIndex >= 0)
            {
                string relativePath = prefabPath.Substring(resourcesIndex + 1);
                if (relativePath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                    relativePath = relativePath.Substring(0, relativePath.Length - 7);
                return relativePath;
            }
            
            return prefabPath;
        }
    }
}
