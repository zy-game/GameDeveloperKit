using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 收集器类型枚举
    /// </summary>
    public enum CollectorType
    {
        [InspectorName("未配置")]
        None,
        [InspectorName("目录收集器")]
        Directory,
        [InspectorName("标签收集器")]
        Label,
        [InspectorName("类型收集器")]
        Type,
        [InspectorName("GUID列表")]
        GuidList,
        [InspectorName("依赖收集器")]
        Dependency,
        [InspectorName("查询收集器")]
        Query,
        [InspectorName("组合收集器")]
        Composite,
        [InspectorName("过滤收集器")]
        Filtered
    }
    
    /// <summary>
    /// 收集器编辑器视图
    /// </summary>
    public class CollectorEditorView
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
            
            // 收集器类型选择 - 使用 CustomDropdownMenu
            var typeContainer = new VisualElement();
            typeContainer.style.marginBottom = 8;
            
            var currentType = GetCollectorType(_package.collector);
            var typeField = CustomDropdownMenu.CreateEnumDropdown(
                "收集器类型",
                currentType,
                newValue =>
                {
                    SetCollectorType(newValue);
                    _onChanged?.Invoke();
                    Refresh();
                },
                _root
            );
            typeContainer.Add(typeField);
            _container.Add(typeContainer);
            
            // 收集器配置
            if (_package.collector != null)
            {
                var configContainer = new VisualElement();
                configContainer.AddToClassList("collector-config");
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
                
                DrawCollectorConfig(configContainer, _package.collector);
                _container.Add(configContainer);
            }
            
            // 预览按钮
            var previewRow = new VisualElement();
            previewRow.style.flexDirection = FlexDirection.Row;
            previewRow.style.marginTop = 12;
            
            var previewButton = new Button(() => ShowCollectionPreview());
            previewButton.text = "预览收集结果";
            previewButton.AddToClassList("btn");
            previewButton.AddToClassList("btn-primary");
            previewRow.Add(previewButton);
            
            var validateButton = new Button(() => ValidateCollector());
            validateButton.text = "验证配置";
            validateButton.AddToClassList("btn");
            validateButton.style.marginLeft = 8;
            previewRow.Add(validateButton);
            
            _container.Add(previewRow);
        }
        
        private CollectorType GetCollectorType(IAssetCollector collector)
        {
            if (collector == null) return CollectorType.None;
            
            return collector switch
            {
                DirectoryCollector => CollectorType.Directory,
                LabelCollector => CollectorType.Label,
                TypeCollector => CollectorType.Type,
                GuidListCollector => CollectorType.GuidList,
                DependencyCollector => CollectorType.Dependency,
                QueryCollector => CollectorType.Query,
                CompositeCollector => CollectorType.Composite,
                FilteredCollector => CollectorType.Filtered,
                _ => CollectorType.None
            };
        }
        
        private void SetCollectorType(CollectorType type)
        {
            _package.collector = type switch
            {
                CollectorType.Directory => new DirectoryCollector(),
                CollectorType.Label => new LabelCollector(),
                CollectorType.Type => new TypeCollector(),
                CollectorType.GuidList => new GuidListCollector(),
                CollectorType.Dependency => new DependencyCollector(),
                CollectorType.Query => new QueryCollector(),
                CollectorType.Composite => new CompositeCollector(),
                CollectorType.Filtered => new FilteredCollector(),
                _ => null
            };
        }
        
        private string GetCollectorDisplayName(IAssetCollector collector)
        {
            if (collector == null) return "未配置";
            
            return collector switch
            {
                DirectoryCollector => "目录收集器",
                LabelCollector => "标签收集器",
                TypeCollector => "类型收集器",
                GuidListCollector => "GUID列表",
                DependencyCollector => "依赖收集器",
                QueryCollector => "查询收集器",
                CompositeCollector => "组合收集器",
                FilteredCollector => "过滤收集器",
                _ => collector.Name
            };
        }
        
        private void DrawCollectorConfig(VisualElement container, IAssetCollector collector)
        {
            switch (collector)
            {
                case DirectoryCollector dc:
                    DrawDirectoryCollectorConfig(container, dc);
                    break;
                case LabelCollector lc:
                    DrawLabelCollectorConfig(container, lc);
                    break;
                case TypeCollector tc:
                    DrawTypeCollectorConfig(container, tc);
                    break;
                case GuidListCollector gc:
                    DrawGuidListCollectorConfig(container, gc);
                    break;
                case DependencyCollector dep:
                    DrawDependencyCollectorConfig(container, dep);
                    break;
                case QueryCollector qc:
                    DrawQueryCollectorConfig(container, qc);
                    break;
                case CompositeCollector cc:
                    DrawCompositeCollectorConfig(container, cc);
                    break;
                case FilteredCollector fc:
                    DrawFilteredCollectorConfig(container, fc);
                    break;
            }
        }
        
        private void DrawDirectoryCollectorConfig(VisualElement container, DirectoryCollector collector)
        {
            // 目录路径
            var pathRow = new VisualElement();
            pathRow.style.flexDirection = FlexDirection.Row;
            pathRow.style.marginBottom = 4;
            
            var pathField = new TextField("目录路径") { value = collector.directoryPath ?? "" };
            pathField.style.flexGrow = 1;
            pathField.RegisterValueChangedCallback(evt =>
            {
                collector.directoryPath = evt.newValue;
                _onChanged?.Invoke();
            });
            pathRow.Add(pathField);
            
            var browseButton = new Button(() =>
            {
                var path = EditorUtility.OpenFolderPanel("选择目录", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                    {
                        path = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                    collector.directoryPath = path;
                    pathField.value = path;
                    _onChanged?.Invoke();
                }
            });
            browseButton.text = "...";
            browseButton.style.width = 30;
            pathRow.Add(browseButton);
            
            container.Add(pathRow);
            
            // 搜索模式
            var patternsField = new TextField("搜索模式") 
            { 
                value = collector.searchPatterns != null ? string.Join(", ", collector.searchPatterns) : "*.*" 
            };
            patternsField.tooltip = "多个模式用逗号分隔，如: *.prefab, *.mat";
            patternsField.RegisterValueChangedCallback(evt =>
            {
                collector.searchPatterns = evt.newValue.Split(',').Select(s => s.Trim()).ToArray();
                _onChanged?.Invoke();
            });
            container.Add(patternsField);
            
            // 递归
            var recursiveToggle = new Toggle("递归搜索") { value = collector.recursive };
            recursiveToggle.RegisterValueChangedCallback(evt =>
            {
                collector.recursive = evt.newValue;
                _onChanged?.Invoke();
            });
            container.Add(recursiveToggle);
            
            // 排除模式
            var excludeField = new TextField("排除模式") 
            { 
                value = collector.excludePatterns != null ? string.Join(", ", collector.excludePatterns) : "*.meta, *.cs" 
            };
            excludeField.tooltip = "多个模式用逗号分隔";
            excludeField.RegisterValueChangedCallback(evt =>
            {
                collector.excludePatterns = evt.newValue.Split(',').Select(s => s.Trim()).ToArray();
                _onChanged?.Invoke();
            });
            container.Add(excludeField);
        }
        
        private void DrawLabelCollectorConfig(VisualElement container, LabelCollector collector)
        {
            var labelsField = new TextField("标签列表") 
            { 
                value = collector.labels != null ? string.Join(", ", collector.labels) : "" 
            };
            labelsField.tooltip = "多个标签用逗号分隔";
            labelsField.RegisterValueChangedCallback(evt =>
            {
                collector.labels = evt.newValue.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                _onChanged?.Invoke();
            });
            container.Add(labelsField);
            
            var searchPathField = new TextField("搜索路径") { value = collector.searchPath ?? "Assets" };
            searchPathField.RegisterValueChangedCallback(evt =>
            {
                collector.searchPath = evt.newValue;
                _onChanged?.Invoke();
            });
            container.Add(searchPathField);
        }
        
        private void DrawTypeCollectorConfig(VisualElement container, TypeCollector collector)
        {
            var typeField = new TextField("类型名称") { value = collector.typeName ?? "GameObject" };
            typeField.tooltip = "如: GameObject, Texture2D, AudioClip, Material";
            typeField.RegisterValueChangedCallback(evt =>
            {
                collector.typeName = evt.newValue;
                _onChanged?.Invoke();
            });
            container.Add(typeField);
            
            var searchPathField = new TextField("搜索路径") { value = collector.searchPath ?? "Assets" };
            searchPathField.RegisterValueChangedCallback(evt =>
            {
                collector.searchPath = evt.newValue;
                _onChanged?.Invoke();
            });
            container.Add(searchPathField);
        }
        
        private void DrawGuidListCollectorConfig(VisualElement container, GuidListCollector collector)
        {
            var countLabel = new Label($"已添加 {collector.guids?.Count ?? 0} 个资源");
            countLabel.style.marginBottom = 8;
            container.Add(countLabel);
            
            var addButton = new Button(() =>
            {
                var path = EditorUtility.OpenFilePanel("选择资源", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                    {
                        path = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                    collector.AddFromPath(path);
                    _onChanged?.Invoke();
                    Refresh();
                }
            });
            addButton.text = "添加资源";
            addButton.AddToClassList("btn");
            container.Add(addButton);
            
            // 显示已添加的资源列表
            if (collector.guids != null && collector.guids.Count > 0)
            {
                var listContainer = new VisualElement();
                listContainer.style.marginTop = 8;
                listContainer.style.maxHeight = 150;
                
                var scrollView = new ScrollView();
                
                for (int i = 0; i < Math.Min(collector.guids.Count, 10); i++)
                {
                    var guid = collector.guids[i];
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.marginBottom = 2;
                    
                    var pathLabel = new Label(path);
                    pathLabel.style.flexGrow = 1;
                    pathLabel.style.fontSize = 11;
                    row.Add(pathLabel);
                    
                    var index = i;
                    var removeButton = new Button(() =>
                    {
                        collector.guids.RemoveAt(index);
                        _onChanged?.Invoke();
                        Refresh();
                    });
                    removeButton.text = "×";
                    removeButton.style.width = 20;
                    row.Add(removeButton);
                    
                    scrollView.Add(row);
                }
                
                if (collector.guids.Count > 10)
                {
                    var moreLabel = new Label($"... 还有 {collector.guids.Count - 10} 个资源");
                    moreLabel.style.fontSize = 11;
                    moreLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                    scrollView.Add(moreLabel);
                }
                
                listContainer.Add(scrollView);
                container.Add(listContainer);
            }
        }
        
        private void DrawDependencyCollectorConfig(VisualElement container, DependencyCollector collector)
        {
            var pathRow = new VisualElement();
            pathRow.style.flexDirection = FlexDirection.Row;
            
            var pathField = new TextField("根资源路径") { value = collector.rootAssetPath ?? "" };
            pathField.style.flexGrow = 1;
            pathField.RegisterValueChangedCallback(evt =>
            {
                collector.rootAssetPath = evt.newValue;
                _onChanged?.Invoke();
            });
            pathRow.Add(pathField);
            
            var browseButton = new Button(() =>
            {
                var path = EditorUtility.OpenFilePanel("选择根资源", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                    {
                        path = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                    collector.rootAssetPath = path;
                    pathField.value = path;
                    _onChanged?.Invoke();
                }
            });
            browseButton.text = "...";
            browseButton.style.width = 30;
            pathRow.Add(browseButton);
            
            container.Add(pathRow);
            
            var recursiveToggle = new Toggle("递归收集依赖") { value = collector.recursive };
            recursiveToggle.RegisterValueChangedCallback(evt =>
            {
                collector.recursive = evt.newValue;
                _onChanged?.Invoke();
            });
            container.Add(recursiveToggle);
        }
        
        private void DrawQueryCollectorConfig(VisualElement container, QueryCollector collector)
        {
            var queryField = new TextField("查询字符串") { value = collector.query ?? "" };
            queryField.tooltip = "支持 t:Type, l:Label 等语法";
            queryField.RegisterValueChangedCallback(evt =>
            {
                collector.query = evt.newValue;
                _onChanged?.Invoke();
            });
            container.Add(queryField);
            
            var pathsField = new TextField("搜索路径") 
            { 
                value = collector.searchPaths != null ? string.Join(", ", collector.searchPaths) : "Assets" 
            };
            pathsField.tooltip = "多个路径用逗号分隔";
            pathsField.RegisterValueChangedCallback(evt =>
            {
                collector.searchPaths = evt.newValue.Split(',').Select(s => s.Trim()).ToArray();
                _onChanged?.Invoke();
            });
            container.Add(pathsField);
        }
        
        private void DrawCompositeCollectorConfig(VisualElement container, CompositeCollector collector)
        {
            var headerLabel = new Label("子收集器列表");
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerLabel.style.marginBottom = 8;
            container.Add(headerLabel);
            
            if (collector.collectors == null)
                collector.collectors = new List<IAssetCollector>();
            
            for (int i = 0; i < collector.collectors.Count; i++)
            {
                var subCollector = collector.collectors[i];
                var index = i;
                
                var itemContainer = new VisualElement();
                itemContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
                itemContainer.style.paddingTop = 8;
                itemContainer.style.paddingBottom = 8;
                itemContainer.style.paddingLeft = 8;
                itemContainer.style.paddingRight = 8;
                itemContainer.style.marginBottom = 4;
                itemContainer.style.borderTopLeftRadius = 4;
                itemContainer.style.borderTopRightRadius = 4;
                itemContainer.style.borderBottomLeftRadius = 4;
                itemContainer.style.borderBottomRightRadius = 4;
                
                var headerRow = new VisualElement();
                headerRow.style.flexDirection = FlexDirection.Row;
                headerRow.style.alignItems = Align.Center;
                
                var typeLabel = new Label($"[{i + 1}] {GetCollectorDisplayName(subCollector)}");
                typeLabel.style.flexGrow = 1;
                headerRow.Add(typeLabel);
                
                var removeButton = new Button(() =>
                {
                    collector.collectors.RemoveAt(index);
                    _onChanged?.Invoke();
                    Refresh();
                });
                removeButton.text = "×";
                removeButton.style.width = 24;
                headerRow.Add(removeButton);
                
                itemContainer.Add(headerRow);
                
                // 子收集器配置
                var subConfigContainer = new VisualElement();
                subConfigContainer.style.marginTop = 8;
                DrawCollectorConfig(subConfigContainer, subCollector);
                itemContainer.Add(subConfigContainer);
                
                container.Add(itemContainer);
            }
            
            // 添加子收集器按钮
            var addButton = new Button(() => ShowAddSubCollectorMenu(collector));
            addButton.text = "+ 添加子收集器";
            addButton.AddToClassList("btn");
            addButton.style.marginTop = 8;
            container.Add(addButton);
        }
        
        private void ShowAddSubCollectorMenu(CompositeCollector composite)
        {
            var menu = new CustomDropdownMenu();
            
            menu.AddItem("目录收集器", false, () => { composite.Add(new DirectoryCollector()); _onChanged?.Invoke(); Refresh(); });
            menu.AddItem("标签收集器", false, () => { composite.Add(new LabelCollector()); _onChanged?.Invoke(); Refresh(); });
            menu.AddItem("类型收集器", false, () => { composite.Add(new TypeCollector()); _onChanged?.Invoke(); Refresh(); });
            menu.AddItem("GUID列表", false, () => { composite.Add(new GuidListCollector()); _onChanged?.Invoke(); Refresh(); });
            menu.AddItem("依赖收集器", false, () => { composite.Add(new DependencyCollector()); _onChanged?.Invoke(); Refresh(); });
            menu.AddItem("查询收集器", false, () => { composite.Add(new QueryCollector()); _onChanged?.Invoke(); Refresh(); });
            
            menu.ShowAsContext(_root);
        }
        
        private void DrawFilteredCollectorConfig(VisualElement container, FilteredCollector collector)
        {
            var sourceLabel = new Label("源收集器");
            sourceLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            container.Add(sourceLabel);
            
            if (collector.source != null)
            {
                var sourceContainer = new VisualElement();
                sourceContainer.style.marginTop = 4;
                sourceContainer.style.marginBottom = 8;
                DrawCollectorConfig(sourceContainer, collector.source);
                container.Add(sourceContainer);
            }
            else
            {
                var setSourceButton = new Button(() => ShowSetSourceMenu(collector));
                setSourceButton.text = "设置源收集器";
                setSourceButton.AddToClassList("btn");
                container.Add(setSourceButton);
            }
            
            var filtersLabel = new Label("过滤器列表");
            filtersLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            filtersLabel.style.marginTop = 12;
            container.Add(filtersLabel);
            
            // TODO: 添加过滤器配置 UI
            var addFilterButton = new Button(() => { });
            addFilterButton.text = "+ 添加过滤器";
            addFilterButton.AddToClassList("btn");
            addFilterButton.style.marginTop = 8;
            container.Add(addFilterButton);
        }
        
        private void ShowSetSourceMenu(FilteredCollector filtered)
        {
            var menu = new CustomDropdownMenu();
            
            menu.AddItem("目录收集器", false, () => { filtered.source = new DirectoryCollector(); _onChanged?.Invoke(); Refresh(); });
            menu.AddItem("标签收集器", false, () => { filtered.source = new LabelCollector(); _onChanged?.Invoke(); Refresh(); });
            menu.AddItem("类型收集器", false, () => { filtered.source = new TypeCollector(); _onChanged?.Invoke(); Refresh(); });
            menu.AddItem("查询收集器", false, () => { filtered.source = new QueryCollector(); _onChanged?.Invoke(); Refresh(); });
            
            menu.ShowAsContext(_root);
        }
        
        private void ShowCollectionPreview()
        {
            if (_package?.collector == null)
            {
                EditorUtility.DisplayDialog("预览", "请先配置收集器", "确定");
                return;
            }
            
            var context = new CollectorContext
            {
                PackageName = _package.packageName,
                AddressMode = _package.addressMode,
                BaseDirectory = "Assets"
            };
            
            var assets = _package.collector.Collect(context).ToList();
            
            var message = $"收集到 {assets.Count} 个资源\n\n";
            
            if (assets.Count > 0)
            {
                message += "前 10 个资源:\n";
                for (int i = 0; i < Math.Min(10, assets.Count); i++)
                {
                    message += $"  {assets[i].assetPath}\n";
                }
                
                if (assets.Count > 10)
                {
                    message += $"  ... 还有 {assets.Count - 10} 个资源";
                }
            }
            
            EditorUtility.DisplayDialog("收集预览", message, "确定");
        }
        
        private void ValidateCollector()
        {
            if (_package?.collector == null)
            {
                EditorUtility.DisplayDialog("验证", "请先配置收集器", "确定");
                return;
            }
            
            if (_package.collector.Validate(out var error))
            {
                EditorUtility.DisplayDialog("验证通过", "收集器配置有效", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("验证失败", error, "确定");
            }
        }
    }
}
