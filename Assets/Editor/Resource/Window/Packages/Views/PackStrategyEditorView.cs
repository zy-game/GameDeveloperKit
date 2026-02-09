using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 打包策略类型枚举
    /// </summary>
    public enum PackStrategyType
    {
        [InspectorName("未配置")]
        None,
        [InspectorName("按目录打包")]
        Directory,
        [InspectorName("按文件打包")]
        File,
        [InspectorName("按标签打包")]
        Label,
        [InspectorName("按类型打包")]
        Type,
        [InspectorName("全部打包")]
        Together,
        [InspectorName("大小限制打包")]
        SizeLimit,
        [InspectorName("共享资源提取")]
        SharedAsset,
        [InspectorName("自定义规则")]
        CustomRule
    }
    
    /// <summary>
    /// 打包策略编辑器视图
    /// </summary>
    public class PackStrategyEditorView
    {
        private VisualElement _container;
        private VisualElement _root;
        private PackageSettings _package;
        private Action _onChanged;
        
        public void Initialize(VisualElement container, VisualElement root, PackageSettings package, Action onChanged)
        {
            _container = container;
            _root = root;
            _package = package;
            _onChanged = onChanged;
            
            Refresh();
        }
        
        public void Refresh()
        {
            _container.Clear();
            
            if (_package == null)
                return;
            
            // 策略类型选择 - 使用 CustomDropdownMenu
            var typeContainer = new VisualElement();
            typeContainer.style.marginBottom = 8;
            
            var currentType = GetStrategyType(_package.packStrategyConfig);
            var typeField = CustomDropdownMenu.CreateEnumDropdown(
                "打包策略",
                currentType,
                newValue =>
                {
                    SetStrategyType(newValue);
                    _onChanged?.Invoke();
                    Refresh();
                },
                _root
            );
            typeContainer.Add(typeField);
            _container.Add(typeContainer);
            
            // 策略配置
            if (_package.packStrategyConfig != null)
            {
                var configContainer = new VisualElement();
                configContainer.style.marginTop = 8;
                configContainer.style.paddingTop = 8;
                configContainer.style.paddingBottom = 8;
                configContainer.style.paddingLeft = 8;
                configContainer.style.paddingRight = 8;
                configContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                configContainer.style.borderTopLeftRadius = 4;
                configContainer.style.borderTopRightRadius = 4;
                configContainer.style.borderBottomLeftRadius = 4;
                configContainer.style.borderBottomRightRadius = 4;
                
                DrawStrategyConfig(configContainer, _package.packStrategyConfig);
                _container.Add(configContainer);
                
                // 策略描述
                var descLabel = new Label(_package.packStrategyConfig.Description);
                descLabel.style.fontSize = 11;
                descLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                descLabel.style.marginTop = 8;
                descLabel.style.whiteSpace = WhiteSpace.Normal;
                _container.Add(descLabel);
            }
            
            // 预览按钮
            var previewRow = new VisualElement();
            previewRow.style.flexDirection = FlexDirection.Row;
            previewRow.style.marginTop = 12;
            
            var previewButton = new Button(() => ShowBundlePreview());
            previewButton.text = "预览打包分组";
            previewButton.AddToClassList("btn");
            previewButton.AddToClassList("btn-primary");
            previewRow.Add(previewButton);
            
            _container.Add(previewRow);
        }
        
        private PackStrategyType GetStrategyType(PackStrategyConfig config)
        {
            if (config == null) return PackStrategyType.None;
            
            return config switch
            {
                DirectoryPackConfig => PackStrategyType.Directory,
                FilePackConfig => PackStrategyType.File,
                LabelPackConfig => PackStrategyType.Label,
                TypePackConfig => PackStrategyType.Type,
                TogetherPackConfig => PackStrategyType.Together,
                SizeLimitPackConfig => PackStrategyType.SizeLimit,
                SharedAssetPackConfig => PackStrategyType.SharedAsset,
                CustomRulePackConfig => PackStrategyType.CustomRule,
                _ => PackStrategyType.None
            };
        }
        
        private void SetStrategyType(PackStrategyType type)
        {
            _package.packStrategyConfig = type switch
            {
                PackStrategyType.Directory => new DirectoryPackConfig(),
                PackStrategyType.File => new FilePackConfig(),
                PackStrategyType.Label => new LabelPackConfig(),
                PackStrategyType.Type => new TypePackConfig(),
                PackStrategyType.Together => new TogetherPackConfig { bundleName = _package.packageName },
                PackStrategyType.SizeLimit => new SizeLimitPackConfig(),
                PackStrategyType.SharedAsset => new SharedAssetPackConfig(),
                PackStrategyType.CustomRule => new CustomRulePackConfig(),
                _ => null
            };
        }
        
        private void DrawStrategyConfig(VisualElement container, PackStrategyConfig config)
        {
            switch (config)
            {
                case DirectoryPackConfig dc:
                    DrawDirectoryPackConfig(container, dc);
                    break;
                case FilePackConfig:
                    // 无额外配置
                    var noConfigLabel = new Label("此策略无额外配置项");
                    noConfigLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                    container.Add(noConfigLabel);
                    break;
                case LabelPackConfig lc:
                    DrawLabelPackConfig(container, lc);
                    break;
                case TypePackConfig:
                    var noConfigLabel2 = new Label("此策略无额外配置项");
                    noConfigLabel2.style.color = new Color(0.6f, 0.6f, 0.6f);
                    container.Add(noConfigLabel2);
                    break;
                case TogetherPackConfig tc:
                    DrawTogetherPackConfig(container, tc);
                    break;
                case SizeLimitPackConfig sc:
                    DrawSizeLimitPackConfig(container, sc);
                    break;
                case SharedAssetPackConfig sac:
                    DrawSharedAssetPackConfig(container, sac);
                    break;
                case CustomRulePackConfig crc:
                    DrawCustomRulePackConfig(container, crc);
                    break;
            }
        }
        
        private void DrawDirectoryPackConfig(VisualElement container, DirectoryPackConfig config)
        {
            var depthField = new IntegerField("目录深度") { value = config.directoryDepth };
            depthField.tooltip = "从 Assets 开始计算的目录层级";
            depthField.RegisterValueChangedCallback(evt =>
            {
                config.directoryDepth = Math.Max(1, evt.newValue);
                _onChanged?.Invoke();
            });
            container.Add(depthField);
            
            var includeRootToggle = new Toggle("包含根目录名") { value = config.includeRootName };
            includeRootToggle.RegisterValueChangedCallback(evt =>
            {
                config.includeRootName = evt.newValue;
                _onChanged?.Invoke();
            });
            container.Add(includeRootToggle);
        }
        
        private void DrawLabelPackConfig(VisualElement container, LabelPackConfig config)
        {
            var handlingField = CustomDropdownMenu.CreateEnumDropdown(
                "无标签资源处理",
                config.unlabeledHandling,
                newValue =>
                {
                    config.unlabeledHandling = newValue;
                    _onChanged?.Invoke();
                },
                _root
            );
            container.Add(handlingField);
            
            var bundleNameField = new TextField("无标签 Bundle 名") { value = config.unlabeledBundleName };
            bundleNameField.RegisterValueChangedCallback(evt =>
            {
                config.unlabeledBundleName = evt.newValue;
                _onChanged?.Invoke();
            });
            container.Add(bundleNameField);
        }
        
        private void DrawTogetherPackConfig(VisualElement container, TogetherPackConfig config)
        {
            var bundleNameField = new TextField("Bundle 名称") { value = config.bundleName };
            bundleNameField.RegisterValueChangedCallback(evt =>
            {
                config.bundleName = evt.newValue;
                _onChanged?.Invoke();
            });
            container.Add(bundleNameField);
        }
        
        private void DrawSizeLimitPackConfig(VisualElement container, SizeLimitPackConfig config)
        {
            var sizeField = new FloatField("最大大小 (MB)") { value = config.maxBundleSizeMB };
            sizeField.RegisterValueChangedCallback(evt =>
            {
                config.maxBundleSizeMB = Math.Max(0.1f, evt.newValue);
                _onChanged?.Invoke();
            });
            container.Add(sizeField);
            
            var prefixField = new TextField("Bundle 名称前缀") { value = config.bundleNamePrefix };
            prefixField.RegisterValueChangedCallback(evt =>
            {
                config.bundleNamePrefix = evt.newValue;
                _onChanged?.Invoke();
            });
            container.Add(prefixField);
        }
        
        private void DrawSharedAssetPackConfig(VisualElement container, SharedAssetPackConfig config)
        {
            var refCountField = new IntegerField("最小引用次数") { value = config.minReferenceCount };
            refCountField.tooltip = "被引用超过此次数的资源会被提取到共享 Bundle";
            refCountField.RegisterValueChangedCallback(evt =>
            {
                config.minReferenceCount = Math.Max(2, evt.newValue);
                _onChanged?.Invoke();
            });
            container.Add(refCountField);
            
            var bundleNameField = new TextField("共享 Bundle 名称") { value = config.sharedBundleName };
            bundleNameField.RegisterValueChangedCallback(evt =>
            {
                config.sharedBundleName = evt.newValue;
                _onChanged?.Invoke();
            });
            container.Add(bundleNameField);
        }
        
        private void DrawCustomRulePackConfig(VisualElement container, CustomRulePackConfig config)
        {
            var headerLabel = new Label("打包规则列表");
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerLabel.style.marginBottom = 8;
            container.Add(headerLabel);
            
            if (config.rules == null)
                config.rules = new List<PackRule>();
            
            for (int i = 0; i < config.rules.Count; i++)
            {
                var rule = config.rules[i];
                var index = i;
                
                var ruleContainer = new VisualElement();
                ruleContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
                ruleContainer.style.paddingTop = 8;
                ruleContainer.style.paddingBottom = 8;
                ruleContainer.style.paddingLeft = 8;
                ruleContainer.style.paddingRight = 8;
                ruleContainer.style.marginBottom = 4;
                ruleContainer.style.borderTopLeftRadius = 4;
                ruleContainer.style.borderTopRightRadius = 4;
                ruleContainer.style.borderBottomLeftRadius = 4;
                ruleContainer.style.borderBottomRightRadius = 4;
                
                var headerRow = new VisualElement();
                headerRow.style.flexDirection = FlexDirection.Row;
                headerRow.style.alignItems = Align.Center;
                
                var ruleNameField = new TextField { value = rule.ruleName ?? $"规则 {i + 1}" };
                ruleNameField.style.flexGrow = 1;
                ruleNameField.RegisterValueChangedCallback(evt =>
                {
                    rule.ruleName = evt.newValue;
                    _onChanged?.Invoke();
                });
                headerRow.Add(ruleNameField);
                
                var removeButton = new Button(() =>
                {
                    config.rules.RemoveAt(index);
                    _onChanged?.Invoke();
                    Refresh();
                });
                removeButton.text = "×";
                removeButton.style.width = 24;
                headerRow.Add(removeButton);
                
                ruleContainer.Add(headerRow);
                
                // 条件类型
                var conditionField = CustomDropdownMenu.CreateEnumDropdown(
                    "条件类型",
                    rule.conditionType,
                    newValue =>
                    {
                        rule.conditionType = newValue;
                        _onChanged?.Invoke();
                    },
                    _root
                );
                ruleContainer.Add(conditionField);
                
                // 匹配模式
                var patternField = new TextField("匹配模式") { value = rule.pattern ?? "" };
                patternField.RegisterValueChangedCallback(evt =>
                {
                    rule.pattern = evt.newValue;
                    _onChanged?.Invoke();
                });
                ruleContainer.Add(patternField);
                
                // Bundle 名称
                var bundleField = new TextField("Bundle 名称") { value = rule.bundleName ?? "" };
                bundleField.RegisterValueChangedCallback(evt =>
                {
                    rule.bundleName = evt.newValue;
                    _onChanged?.Invoke();
                });
                ruleContainer.Add(bundleField);
                
                container.Add(ruleContainer);
            }
            
            // 添加规则按钮
            var addButton = new Button(() =>
            {
                config.rules.Add(new PackRule
                {
                    ruleName = $"规则 {config.rules.Count + 1}",
                    conditionType = ConditionType.PathContains,
                    pattern = "",
                    bundleName = "bundle_name"
                });
                _onChanged?.Invoke();
                Refresh();
            });
            addButton.text = "+ 添加规则";
            addButton.AddToClassList("btn");
            addButton.style.marginTop = 8;
            container.Add(addButton);
            
            // 默认 Bundle 名称
            var defaultBundleField = new TextField("默认 Bundle 名称") { value = config.defaultBundleName };
            defaultBundleField.tooltip = "不匹配任何规则的资源使用此名称";
            defaultBundleField.style.marginTop = 12;
            defaultBundleField.RegisterValueChangedCallback(evt =>
            {
                config.defaultBundleName = evt.newValue;
                _onChanged?.Invoke();
            });
            container.Add(defaultBundleField);
        }
        
        private void ShowBundlePreview()
        {
            if (_package?.collector == null)
            {
                EditorUtility.DisplayDialog("预览", "请先配置收集器", "确定");
                return;
            }
            
            var bundles = _package.GetBundleGroups();
            
            var message = $"打包分组预览\n\n";
            message += $"总计 {bundles.Count} 个 Bundle\n\n";
            
            var sortedBundles = bundles.OrderByDescending(b => b.Value.Count).Take(10);
            
            foreach (var bundle in sortedBundles)
            {
                message += $"[{bundle.Key}] - {bundle.Value.Count} 个资源\n";
            }
            
            if (bundles.Count > 10)
            {
                message += $"\n... 还有 {bundles.Count - 10} 个 Bundle";
            }
            
            EditorUtility.DisplayDialog("打包分组预览", message, "确定");
        }
    }
}
