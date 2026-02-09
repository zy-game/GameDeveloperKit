using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using GameDeveloperKit.Procedure;
using UnityEditor.UIElements;

namespace GameDeveloperKit.Editor
{
    [CustomEditor(typeof(Startup))]
    public class StartupEditor : UnityEditor.Editor
    {
        private SerializedProperty _preloadBasePackagesProperty;
        private SerializedProperty _customProcedureTypeNameProperty;
        private SerializedProperty _resourceModeProperty;
        private SerializedProperty _resourceUpdateUrlProperty;
        private SerializedProperty _webServerUrlProperty;
        private List<string> _availablePackages;
        private bool _isValidType = false;
        private List<Type> _availableProcedures;
        private List<string> _procedureChoices;
        private DropdownField _procedureDropdown;

        private void OnEnable()
        {
            _customProcedureTypeNameProperty = serializedObject.FindProperty("customProcedureTypeName");
            _preloadBasePackagesProperty = serializedObject.FindProperty("_preloadBasePackages");
            _resourceModeProperty = serializedObject.FindProperty("_resourceMode");
            _resourceUpdateUrlProperty = serializedObject.FindProperty("_resourceUpdateUrl");
            _webServerUrlProperty = serializedObject.FindProperty("_webServerUrl");

            // 扫描所有可用的 Procedure
            ScanAvailableProcedures();
            // 扫描所有可用的资源包
            ScanAvailablePackages();

            if (_customProcedureTypeNameProperty != null)
            {
                ValidateType(_customProcedureTypeNameProperty.stringValue);
            }
        }

        private void ScanAvailablePackages()
        {
            _availablePackages = new List<string>();
            var settings = GameDeveloperKit.Editor.Resource.ResourcePackagesData.Instance;
            if (settings != null && settings.packages != null)
            {
                _availablePackages.AddRange(settings.packages.Select(p => p.packageName));
            }
        }

        private void ScanAvailableProcedures()
        {
            _availableProcedures = FindAllProcedureTypes();

            // 创建选项列表：第一个是 "None"，然后是所有找到的 Procedure
            _procedureChoices = new List<string> { "None (不使用自定义流程)" };
            _procedureChoices.AddRange(_availableProcedures.Select(t => t.FullName));
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            // 加载通用样式表
            var commonStyleSheet = EditorAssetLoader.LoadStyleSheet("Common/Style/EditorCommonStyle.uss");
            if (commonStyleSheet != null)
            {
                root.styleSheets.Add(commonStyleSheet);
            }
            
            var dropdownStyleSheet = EditorAssetLoader.LoadStyleSheet("Common/Style/CustomDropdownMenu.uss");
            if (dropdownStyleSheet != null)
            {
                root.styleSheets.Add(dropdownStyleSheet);
            }

            // Procedure Settings
            var procedureContainer = CreateHelpBoxContainer();
            var procedureFoldout = new Foldout { text = "Procedure Settings", value = true };
            CreateTypeField(procedureFoldout);
            procedureContainer.Add(procedureFoldout);
            root.Add(procedureContainer);

            // Resource Settings
            var resourceContainer = CreateHelpBoxContainer();
            var resourceFoldout = new Foldout { text = "Resource Settings", value = true };
            
            var resourceModeField = new PropertyField(_resourceModeProperty, "Resource Mode");
            resourceModeField.AddToClassList("custom-dropdown");
            resourceFoldout.Add(resourceModeField);

            var resourceUpdateUrlField = new PropertyField(_resourceUpdateUrlProperty, "Resource Update URL");
            resourceUpdateUrlField.AddToClassList("custom-textfield");
            resourceFoldout.Add(resourceUpdateUrlField);

            CreatePackageField(resourceFoldout);
            resourceContainer.Add(resourceFoldout);
            root.Add(resourceContainer);

            // Web Settings
            var webContainer = CreateHelpBoxContainer();
            var webFoldout = new Foldout { text = "Web Settings", value = true };
            var webServerUrlField = new PropertyField(_webServerUrlProperty, "Web Server URL");
            webServerUrlField.AddToClassList("custom-textfield");
            webFoldout.Add(webServerUrlField);
            webContainer.Add(webFoldout);
            root.Add(webContainer);

            return root;
        }

        private VisualElement CreateHelpBoxContainer()
        {
            var container = new VisualElement();
            container.AddToClassList("custom-help-box");
            container.AddToClassList("help-box--info");
            return container;
        }

        private void CreatePackageField(VisualElement root)
        {
            var container = new VisualElement();
            container.style.marginTop = 10;
            container.style.marginBottom = 10;
            
            // 准备数据
            var currentMask = 0;
            // 过滤掉不存在的包，避免索引错误
            var validPackages = new List<string>();
            
            for (int i = 0; i < _preloadBasePackagesProperty.arraySize; i++)
            {
                var pkgName = _preloadBasePackagesProperty.GetArrayElementAtIndex(i).stringValue;
                var idx = _availablePackages.IndexOf(pkgName);
                if (idx >= 0)
                {
                    currentMask |= (1 << idx);
                    validPackages.Add(pkgName);
                }
            }

            if (_availablePackages.Count == 0)
            {
                var helpBox = new HelpBox("没有找到可用的资源包 (Resource Packages)。请先在 Resource Packages Window 中创建包。", HelpBoxMessageType.Info);
                helpBox.AddToClassList("custom-help-box");
                helpBox.AddToClassList("help-box--info");
                container.Add(helpBox);
                root.Add(container);
                return;
            }

            // 使用原生 MaskField
            // 注意：MaskField 最多支持 32 个选项 (int限制)
            var maskField = new MaskField("预加载首包", _availablePackages, currentMask);
            maskField.AddToClassList("custom-dropdown");
            maskField.RegisterValueChangedCallback(evt => 
            {
                var newMask = evt.newValue;
                _preloadBasePackagesProperty.ClearArray();
                
                var settings = GameDeveloperKit.Editor.Resource.ResourcePackagesData.Instance;
                
                for (int i = 0; i < _availablePackages.Count; i++)
                {
                    var packageName = _availablePackages[i];
                    var isSelected = (newMask & (1 << i)) != 0;
                    
                    if (isSelected)
                    {
                        var index = _preloadBasePackagesProperty.arraySize;
                        _preloadBasePackagesProperty.InsertArrayElementAtIndex(index);
                        _preloadBasePackagesProperty.GetArrayElementAtIndex(index).stringValue = packageName;
                    }
                    
                    // 同步更新 PackageType
                    if (settings != null)
                    {
                        var package = settings.FindPackage(packageName);
                        if (package != null)
                        {
                            var newType = isSelected ? GameDeveloperKit.Editor.Resource.PackageType.BasePackage : GameDeveloperKit.Editor.Resource.PackageType.HotfixPackage;
                            if (package.packageType != newType)
                            {
                                package.packageType = newType;
                                settings.Save();
                            }
                        }
                    }
                }
                serializedObject.ApplyModifiedProperties();
            });
            
            container.Add(maskField);
            root.Add(container);
        }

        private void CreateTypeField(VisualElement root)
        {
            // 创建一个横向容器，用于放置下拉框和验证图标
            var fieldRow = new VisualElement();
            fieldRow.style.flexDirection = FlexDirection.Row;
            fieldRow.style.alignItems = Align.Center;

            // 获取当前选中的值
            string currentValue = _customProcedureTypeNameProperty.stringValue;
            string currentChoice = string.IsNullOrEmpty(currentValue) ? "None (不使用自定义流程)" : currentValue;

            // 如果当前值不在列表中，添加它（可能是手动输入的）
            if (!string.IsNullOrEmpty(currentValue) && !_procedureChoices.Contains(currentValue))
            {
                _procedureChoices.Add(currentValue);
            }

            // 创建下拉框
            _procedureDropdown = new DropdownField("自定义流程", _procedureChoices, currentChoice);
            _procedureDropdown.AddToClassList("custom-dropdown");
            _procedureDropdown.style.flexGrow = 1;
            _procedureDropdown.style.marginRight = 5;

            // 注册值变化回调
            _procedureDropdown.RegisterValueChangedCallback(evt =>
            {
                string newValue = evt.newValue;

                // 如果选择 "None"，清空值
                if (newValue == "None (不使用自定义流程)")
                {
                    _customProcedureTypeNameProperty.stringValue = "";
                }
                else
                {
                    _customProcedureTypeNameProperty.stringValue = newValue;
                }

                serializedObject.ApplyModifiedProperties();
                ValidateType(_customProcedureTypeNameProperty.stringValue);

                // 更新验证图标
                var icon = fieldRow.Q<Label>("status-icon");
                if (icon != null)
                {
                    string currentValue = _customProcedureTypeNameProperty.stringValue;
                    if (string.IsNullOrEmpty(currentValue))
                    {
                        icon.text = "";
                    }
                    else
                    {
                        icon.text = _isValidType ? "✓" : "✗";
                        icon.style.color = _isValidType ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
                    }
                }
            });

            fieldRow.Add(_procedureDropdown);

            // 创建验证状态图标
            var statusIcon = new Label(_isValidType ? "✓" : (string.IsNullOrEmpty(currentValue) ? "" : "✗"));
            statusIcon.name = "status-icon";
            statusIcon.style.color = _isValidType ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
            statusIcon.style.fontSize = 16;
            statusIcon.style.unityFontStyleAndWeight = FontStyle.Bold;
            statusIcon.style.minWidth = 20;
            statusIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
            fieldRow.Add(statusIcon);

            root.Add(fieldRow);
        }

        private void ValidateType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                _isValidType = false;
                return;
            }

            try
            {
                // Search all assemblies
                Type procedureType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    procedureType = assembly.GetType(typeName, false);
                    if (procedureType != null) break;
                }

                if (procedureType == null)
                {
                    _isValidType = false;
                    return;
                }

                // Check if derives from ProcedureBase
                if (!typeof(StateBase).IsAssignableFrom(procedureType))
                {
                    _isValidType = false;
                    return;
                }

                // Check if abstract
                if (procedureType.IsAbstract)
                {
                    _isValidType = false;
                    return;
                }

                // Check if has parameterless constructor
                if (procedureType.GetConstructor(Type.EmptyTypes) == null)
                {
                    _isValidType = false;
                    return;
                }

                _isValidType = true;
            }
            catch
            {
                _isValidType = false;
            }
        }

        private List<Type> FindAllProcedureTypes()
        {
            var procedures = new List<Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    // 跳过 Unity 和系统程序集以提高性能
                    var assemblyName = assembly.GetName().Name;
                    if (assemblyName.StartsWith("Unity") ||
                        assemblyName.StartsWith("System") ||
                        assemblyName.StartsWith("Mono") ||
                        assemblyName.StartsWith("mscorlib") ||
                        assemblyName.StartsWith("netstandard"))
                    {
                        continue;
                    }

                    foreach (var type in assembly.GetTypes())
                    {
                        if (typeof(StateBase).IsAssignableFrom(type) &&
                            !type.IsAbstract &&
                            type != typeof(StateBase) &&
                            type.GetConstructor(Type.EmptyTypes) != null)
                        {
                            procedures.Add(type);
                        }
                    }
                }
                catch
                {
                    // Skip problematic assemblies
                }
            }

            return procedures.OrderBy(t => t.FullName).ToList();
        }
    }
}
