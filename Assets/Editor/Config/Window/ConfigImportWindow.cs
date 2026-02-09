using System;
using System.Collections.Generic;
using System.IO;
using GameDeveloperKit.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor.Config
{
    /// <summary>
    /// 配置表导入工具窗口
    /// </summary>
    public class ConfigImportWindow : EditorWindow
    {
        private ConfigImportData _data;
        private ConfigFileInfo _selectedFile;
        private ConfigSheetInfo _selectedSheet;
        
        private VisualElement _root;
        private VisualElement _toolbar;
        private VisualElement _leftPanel;
        private VisualElement _rightPanel;
        private VisualElement _fileListContainer;
        private VisualElement _detailContainer;
        private VisualElement _settingsContainer;
        private VisualElement _emptyState;
        
        private bool _showSettings = false;
        
        private const float LeftPanelWidth = 240f;
        private const float DefaultColumnWidth = 120f;
        private const float MinColumnWidth = 60f;
        private const float MaxColumnWidth = 400f;
        private const int PageSize = 20;
        
        private List<float> _columnWidths = new List<float>();
        private List<VisualElement> _allColumnCells = new List<VisualElement>();
        private int _currentPage = 0;
        
        [MenuItem("GameDeveloperKit/配置表工具")]
        public static void ShowWindow()
        {
            var window = GetWindow<ConfigImportWindow>("配置表导入");
            window.minSize = new Vector2(900, 600);
            window.Show();
        }
        
        private void CreateGUI()
        {
            _data = ConfigImportData.Instance;
            
            _root = new VisualElement();
            _root.style.flexGrow = 1;
            _root.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(_root);
            
            var styleSheet = EditorAssetLoader.LoadStyleSheet("Common/Style/EditorCommonStyle.uss");
            if (styleSheet != null)
            {
                _root.styleSheets.Add(styleSheet);
            }
            
            CreateToolbar();
            CreateMainContent();
            RefreshFileList();
        }
        
        private void CreateToolbar()
        {
            _toolbar = new VisualElement();
            _toolbar.AddToClassList("toolbar");
            
            var title = new Label("配置表导入工具");
            title.AddToClassList("toolbar-title");
            _toolbar.Add(title);
            
            var spacer = new VisualElement();
            spacer.AddToClassList("toolbar-spacer");
            _toolbar.Add(spacer);
            
            // 刷新按钮（源目录不为空时显示）
            if (!string.IsNullOrEmpty(_data.sourceDirectory) && Directory.Exists(_data.sourceDirectory))
            {
                var refreshBtn = new Button(() => ScanSourceDirectory());
                refreshBtn.text = "刷新";
                refreshBtn.AddToClassList("btn");
                refreshBtn.AddToClassList("btn-secondary");
                refreshBtn.style.marginRight = 8;
                _toolbar.Add(refreshBtn);
            }
            
            var importBtn = new Button(() => ImportFile());
            importBtn.text = "导入文件";
            importBtn.AddToClassList("btn");
            importBtn.AddToClassList("btn-primary");
            importBtn.style.marginRight = 8;
            _toolbar.Add(importBtn);
            
            var generateAllBtn = new Button(() => GenerateAll());
            generateAllBtn.text = "全部生成";
            generateAllBtn.AddToClassList("btn");
            generateAllBtn.AddToClassList("btn-success");
            generateAllBtn.style.marginRight = 8;
            _toolbar.Add(generateAllBtn);
            
            var settingsBtn = new Button(() => ToggleSettings());
            settingsBtn.text = "⚙";
            settingsBtn.style.fontSize = 14;
            settingsBtn.style.width = 28;
            settingsBtn.style.height = 28;
            settingsBtn.AddToClassList("btn");
            _toolbar.Add(settingsBtn);
            
            _root.Add(_toolbar);
        }
        
        private void RecreateToolbar()
        {
            if (_toolbar != null)
            {
                var index = _root.IndexOf(_toolbar);
                _toolbar.RemoveFromHierarchy();
                
                _toolbar = new VisualElement();
                _toolbar.AddToClassList("toolbar");
                
                var title = new Label("配置表导入工具");
                title.AddToClassList("toolbar-title");
                _toolbar.Add(title);
                
                var spacer = new VisualElement();
                spacer.AddToClassList("toolbar-spacer");
                _toolbar.Add(spacer);
                
                if (!string.IsNullOrEmpty(_data.sourceDirectory) && Directory.Exists(_data.sourceDirectory))
                {
                    var refreshBtn = new Button(() => ScanSourceDirectory());
                    refreshBtn.text = "刷新";
                    refreshBtn.AddToClassList("btn");
                    refreshBtn.AddToClassList("btn-secondary");
                    refreshBtn.style.marginRight = 8;
                    _toolbar.Add(refreshBtn);
                }
                
                var importBtn = new Button(() => ImportFile());
                importBtn.text = "导入文件";
                importBtn.AddToClassList("btn");
                importBtn.AddToClassList("btn-primary");
                importBtn.style.marginRight = 8;
                _toolbar.Add(importBtn);
                
                var generateAllBtn = new Button(() => GenerateAll());
                generateAllBtn.text = "全部生成";
                generateAllBtn.AddToClassList("btn");
                generateAllBtn.AddToClassList("btn-success");
                generateAllBtn.style.marginRight = 8;
                _toolbar.Add(generateAllBtn);
                
                var settingsBtn = new Button(() => ToggleSettings());
                settingsBtn.text = "⚙";
                settingsBtn.style.fontSize = 14;
                settingsBtn.style.width = 28;
                settingsBtn.style.height = 28;
                settingsBtn.AddToClassList("btn");
                _toolbar.Add(settingsBtn);
                
                _root.Insert(index >= 0 ? index : 0, _toolbar);
            }
        }
        
        private void ScanSourceDirectory()
        {
            if (string.IsNullOrEmpty(_data.sourceDirectory) || !Directory.Exists(_data.sourceDirectory))
            {
                EditorUtility.DisplayDialog("错误", "源目录不存在或未配置", "确定");
                return;
            }
            
            var extensions = new[] { "*.xlsx", "*.xls", "*.csv" };
            var files = new List<string>();
            
            foreach (var ext in extensions)
            {
                files.AddRange(Directory.GetFiles(_data.sourceDirectory, ext, SearchOption.AllDirectories));
            }
            
            if (files.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "源目录下没有找到配置文件（xlsx/xls/csv）", "确定");
                return;
            }
            
            int imported = 0;
            int updated = 0;
            
            foreach (var filePath in files)
            {
                var existing = _data.files.Find(f => f.filePath == filePath);
                var file = ExcelImporter.ImportFromFile(filePath);
                
                if (file != null && file.sheets.Count > 0)
                {
                    if (existing != null)
                    {
                        file.id = existing.id;
                        updated++;
                    }
                    else
                    {
                        imported++;
                    }
                    _data.AddFile(file);
                }
            }
            
            RefreshFileList();
            
            if (_data.files.Count > 0 && _selectedFile == null)
            {
                SelectFile(_data.files[0]);
            }
            
            Debug.Log($"扫描完成: 新增 {imported} 个, 更新 {updated} 个配置文件");
        }
        
        private void ToggleSettings()
        {
            _showSettings = !_showSettings;
            UpdateRightPanelVisibility();
        }
        
        private void UpdateRightPanelVisibility()
        {
            if (_showSettings)
            {
                _settingsContainer.style.display = DisplayStyle.Flex;
                _detailContainer.style.display = DisplayStyle.None;
                _emptyState.style.display = DisplayStyle.None;
            }
            else
            {
                _settingsContainer.style.display = DisplayStyle.None;
                if (_selectedFile != null)
                {
                    _detailContainer.style.display = DisplayStyle.Flex;
                    _emptyState.style.display = DisplayStyle.None;
                }
                else
                {
                    _detailContainer.style.display = DisplayStyle.None;
                    _emptyState.style.display = DisplayStyle.Flex;
                }
            }
        }
        
        private void CreateMainContent()
        {
            var content = new VisualElement();
            content.AddToClassList("content-area");
            
            CreateLeftPanel(content);
            CreateRightPanel(content);
            
            _root.Add(content);
        }
        
        private void CreateLeftPanel(VisualElement parent)
        {
            _leftPanel = new VisualElement();
            _leftPanel.AddToClassList("left-panel");
            _leftPanel.style.width = LeftPanelWidth;
            
            var scroll = new ScrollView();
            scroll.AddToClassList("package-scroll");
            scroll.style.flexGrow = 1;
            
            _fileListContainer = new VisualElement();
            _fileListContainer.AddToClassList("package-list-container");
            scroll.Add(_fileListContainer);
            
            _leftPanel.Add(scroll);
            parent.Add(_leftPanel);
        }
        
        private void CreateRightPanel(VisualElement parent)
        {
            _rightPanel = new VisualElement();
            _rightPanel.AddToClassList("right-panel");
            
            // 空状态
            _emptyState = new VisualElement();
            _emptyState.AddToClassList("empty-state");
            var emptyText = new Label("请选择或导入配置文件\n\n右键点击左侧条目可进行更多操作");
            emptyText.AddToClassList("empty-state-text");
            emptyText.style.whiteSpace = WhiteSpace.Normal;
            emptyText.style.unityTextAlign = TextAnchor.MiddleCenter;
            _emptyState.Add(emptyText);
            _rightPanel.Add(_emptyState);
            
            // 详情容器
            _detailContainer = new VisualElement();
            _detailContainer.style.flexGrow = 1;
            _detailContainer.style.display = DisplayStyle.None;
            _rightPanel.Add(_detailContainer);
            
            // 设置容器
            _settingsContainer = new VisualElement();
            _settingsContainer.style.flexGrow = 1;
            _settingsContainer.style.display = DisplayStyle.None;
            CreateSettingsContent(_settingsContainer);
            _rightPanel.Add(_settingsContainer);
            
            parent.Add(_rightPanel);
        }
        
        private void CreateSettingsContent(VisualElement parent)
        {
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.style.paddingLeft = 16;
            scroll.style.paddingRight = 16;
            scroll.style.paddingTop = 16;
            
            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;
            card.style.paddingLeft = 16;
            card.style.paddingRight = 16;
            card.style.paddingTop = 16;
            card.style.paddingBottom = 16;
            
            var title = new Label("全局设置");
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.9f, 0.9f, 0.9f);
            title.style.marginBottom = 16;
            card.Add(title);
            
            // 源目录
            var sourceRow = CreateSettingsPathRow("源目录", _data.sourceDirectory,
                value => { 
                    _data.sourceDirectory = value; 
                    _data.Save();
                    // 源目录改变后自动扫描
                    if (!string.IsNullOrEmpty(value) && Directory.Exists(value))
                    {
                        ScanSourceDirectory();
                        RecreateToolbar();
                    }
                },
                () => EditorUtility.OpenFolderPanel("选择配置源目录", _data.sourceDirectory, ""));
            card.Add(sourceRow);
            
            // 代码输出路径
            var codeRow = CreateSettingsPathRow("代码输出路径", _data.codeOutputPath,
                value => { _data.codeOutputPath = value; _data.Save(); },
                () => EditorUtility.OpenFolderPanel("选择代码输出目录", _data.codeOutputPath, ""));
            card.Add(codeRow);
            
            // 数据输出路径
            var jsonRow = CreateSettingsPathRow("数据输出路径", _data.jsonOutputPath,
                value => { _data.jsonOutputPath = value; _data.Save(); },
                () => EditorUtility.OpenFolderPanel("选择数据输出目录", _data.jsonOutputPath, ""));
            card.Add(jsonRow);
            
            scroll.Add(card);
            parent.Add(scroll);
        }
        
        private VisualElement CreateSettingsPathRow(string label, string value, System.Action<string> onChanged, System.Func<string> openDialog)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 10;
            
            var field = new TextField(label);
            field.value = value ?? "";
            field.AddToClassList("custom-textfield");
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            row.Add(field);
            
            var browseBtn = new Button(() => {
                var path = openDialog();
                if (!string.IsNullOrEmpty(path))
                {
                    field.value = path;
                    onChanged(path);
                }
            });
            browseBtn.text = "...";
            browseBtn.style.width = 30;
            browseBtn.style.marginLeft = 4;
            row.Add(browseBtn);
            
            return row;
        }
        
        private void RefreshFileList()
        {
            _fileListContainer.Clear();
            
            foreach (var file in _data.files)
            {
                var item = CreateFileListItem(file);
                _fileListContainer.Add(item);
            }
        }
        
        private VisualElement CreateFileListItem(ConfigFileInfo file)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingLeft = 10;
            item.style.paddingRight = 10;
            item.style.paddingTop = 10;
            item.style.paddingBottom = 10;
            item.style.marginBottom = 4;
            item.style.borderTopLeftRadius = 6;
            item.style.borderTopRightRadius = 6;
            item.style.borderBottomLeftRadius = 6;
            item.style.borderBottomRightRadius = 6;
            item.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            
            item.RegisterCallback<ClickEvent>(evt => SelectFile(file));
            item.RegisterCallback<MouseEnterEvent>(evt => {
                if (_selectedFile != file)
                    item.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            });
            item.RegisterCallback<MouseLeaveEvent>(evt => {
                if (_selectedFile != file)
                    item.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            });
            
            // 右键菜单
            item.RegisterCallback<ContextClickEvent>(evt => {
                ShowFileContextMenu(file);
                evt.StopPropagation();
            });
            
            var icon = new Label(file.isCsv ? "📄" : "📊");
            icon.style.marginRight = 8;
            icon.style.fontSize = 16;
            item.Add(icon);
            
            var textContainer = new VisualElement();
            textContainer.style.flexGrow = 1;
            textContainer.style.flexDirection = FlexDirection.Column;
            
            var name = new Label(file.fileName);
            name.style.fontSize = 12;
            name.style.color = new Color(0.86f, 0.86f, 0.86f);
            name.style.overflow = Overflow.Hidden;
            name.style.textOverflow = TextOverflow.Ellipsis;
            textContainer.Add(name);
            
            var sheetInfo = new Label($"{file.sheets.Count} 个表");
            sheetInfo.style.fontSize = 9;
            sheetInfo.style.color = new Color(0.5f, 0.5f, 0.5f);
            sheetInfo.style.marginTop = 2;
            textContainer.Add(sheetInfo);
            
            item.Add(textContainer);
            item.userData = file;
            
            return item;
        }
        
        private void SelectFile(ConfigFileInfo file)
        {
            if (_selectedFile != file)
            {
                _columnWidths.Clear();
                _currentPage = 0;
            }
            
            _selectedFile = file;
            _selectedSheet = file.GetFirstSheet();
            
            // 设置parentFile引用并自动加载数据
            foreach (var sheet in file.sheets)
            {
                sheet.parentFile = file;
                if (sheet.previewData == null)
                {
                    ExcelImporter.ReloadSheetData(file, sheet);
                }
            }
            
            // 更新列表高亮
            foreach (var child in _fileListContainer.Children())
            {
                var f = child.userData as ConfigFileInfo;
                child.style.backgroundColor = f == file 
                    ? new Color(0.23f, 0.36f, 0.53f) 
                    : new Color(0.22f, 0.22f, 0.22f);
            }
            
            // 关闭设置视图，显示详情
            _showSettings = false;
            _settingsContainer.style.display = DisplayStyle.None;
            _emptyState.style.display = DisplayStyle.None;
            _detailContainer.style.display = DisplayStyle.Flex;
            
            RefreshDetailView();
        }
        
        private void ShowFileContextMenu(ConfigFileInfo file)
        {
            var menu = new GenericMenu();
            
            menu.AddItem(new GUIContent("生成代码"), false, () => {
                foreach (var sheet in file.sheets)
                {
                    sheet.parentFile = file;
                    ConfigCodeGenerator.GenerateConfigClass(file, sheet);
                }
            });
            
            menu.AddItem(new GUIContent("生成数据"), false, () => {
                foreach (var sheet in file.sheets)
                {
                    sheet.parentFile = file;
                    ConfigCodeGenerator.GenerateJsonData(file, sheet);
                }
            });
            
            menu.AddItem(new GUIContent("全部生成"), false, () => {
                ConfigCodeGenerator.GenerateAllSheetsInFile(file);
            });
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("重新导入"), false, () => {
                ReimportFile(file);
            });
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("删除"), false, () => {
                DeleteFile(file);
            });
            
            menu.ShowAsContext();
        }
        
        private void ReimportFile(ConfigFileInfo file)
        {
            if (!File.Exists(file.filePath))
            {
                EditorUtility.DisplayDialog("错误", $"文件不存在: {file.filePath}", "确定");
                return;
            }
            
            var newFile = ExcelImporter.ImportFromFile(file.filePath);
            if (newFile != null && newFile.sheets.Count > 0)
            {
                // 保留原有ID
                newFile.id = file.id;
                _data.AddFile(newFile);
                RefreshFileList();
                
                if (_selectedFile?.id == file.id)
                {
                    SelectFile(newFile);
                }
                
                Debug.Log($"Reimported: {file.fileName}");
            }
        }
        
        private void DeleteFile(ConfigFileInfo file)
        {
            if (EditorUtility.DisplayDialog("确认删除", $"确定要删除 '{file.fileName}' 吗？", "删除", "取消"))
            {
                _data.RemoveFile(file.id);
                
                if (_selectedFile?.id == file.id)
                {
                    _selectedFile = null;
                    _selectedSheet = null;
                    _emptyState.style.display = DisplayStyle.Flex;
                    _detailContainer.style.display = DisplayStyle.None;
                }
                
                RefreshFileList();
                
                if (_data.files.Count > 0 && _selectedFile == null)
                {
                    SelectFile(_data.files[0]);
                }
            }
        }
        
        private void RefreshDetailView()
        {
            _detailContainer.Clear();
            
            if (_selectedFile == null) return;
            
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.style.paddingLeft = 16;
            scroll.style.paddingRight = 16;
            scroll.style.paddingTop = 16;
            
            CreateSheetTabs(scroll);
            CreateBasicInfoCard(scroll);
            CreateExcelTableCard(scroll);
            
            _detailContainer.Add(scroll);
        }
        
        private void CreateSheetTabs(VisualElement parent)
        {
            if (_selectedFile.sheets.Count <= 1) return;
            
            // 外层容器，用于居中
            var wrapper = new VisualElement();
            wrapper.style.flexDirection = FlexDirection.Row;
            wrapper.style.justifyContent = Justify.Center;
            wrapper.style.marginBottom = 16;
            
            // Tab 容器，带圆角背景
            var tabContainer = new VisualElement();
            tabContainer.style.flexDirection = FlexDirection.Row;
            tabContainer.style.backgroundColor = new Color(0.13f, 0.13f, 0.13f);
            tabContainer.style.borderTopLeftRadius = 8;
            tabContainer.style.borderTopRightRadius = 8;
            tabContainer.style.borderBottomLeftRadius = 8;
            tabContainer.style.borderBottomRightRadius = 8;
            tabContainer.style.paddingLeft = 4;
            tabContainer.style.paddingRight = 4;
            tabContainer.style.paddingTop = 4;
            tabContainer.style.paddingBottom = 4;
            
            for (int i = 0; i < _selectedFile.sheets.Count; i++)
            {
                var sheet = _selectedFile.sheets[i];
                var isSelected = sheet == _selectedSheet;
                var isFirst = i == 0;
                var isLast = i == _selectedFile.sheets.Count - 1;
                
                var tab = new Button(() => {
                    _selectedSheet = sheet;
                    _columnWidths.Clear();
                    _currentPage = 0;
                    // 切换Sheet时自动加载数据（如果未加载）
                    if (sheet.previewData == null)
                    {
                        ExcelImporter.ReloadSheetData(_selectedFile, sheet);
                    }
                    RefreshDetailView();
                });
                tab.text = sheet.sheetName;
                tab.style.marginLeft = 0;
                tab.style.marginRight = 0;
                tab.style.paddingLeft = 16;
                tab.style.paddingRight = 16;
                tab.style.paddingTop = 8;
                tab.style.paddingBottom = 8;
                tab.style.borderTopWidth = 0;
                tab.style.borderBottomWidth = 0;
                tab.style.borderLeftWidth = 0;
                tab.style.borderRightWidth = 0;
                tab.style.fontSize = 12;
                
                // 圆角处理
                tab.style.borderTopLeftRadius = isFirst ? 6 : 0;
                tab.style.borderBottomLeftRadius = isFirst ? 6 : 0;
                tab.style.borderTopRightRadius = isLast ? 6 : 0;
                tab.style.borderBottomRightRadius = isLast ? 6 : 0;
                
                if (isSelected)
                {
                    tab.style.backgroundColor = new Color(0.24f, 0.47f, 0.73f);
                    tab.style.color = Color.white;
                }
                else
                {
                    tab.style.backgroundColor = new Color(0, 0, 0, 0);
                    tab.style.color = new Color(0.6f, 0.6f, 0.6f);
                }
                
                tabContainer.Add(tab);
            }
            
            wrapper.Add(tabContainer);
            parent.Add(wrapper);
        }
        
        private static readonly List<string> TypeChoices = new List<string> 
        { 
            "int", "float", "string", "bool", "long", "double", "int[]", "float[]", "string[]",
            "Vector2", "Vector3", "Vector4", "Color", "Rect"
        };
        
        /// <summary>
        /// 将类型别名规范化为标准类型名
        /// </summary>
        private static string NormalizeTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return "string";
            
            var lower = typeName.ToLower().Trim();
            
            // 整数类型
            if (lower == "int32" || lower == "short" || lower == "int16" || lower == "byte" || lower == "sbyte" 
                || lower == "uint" || lower == "uint32" || lower == "uint16")
                return "int";
            
            if (lower == "int64" || lower == "uint64")
                return "long";
            
            // 浮点类型
            if (lower == "single" || lower == "decimal")
                return "float";
            
            // 布尔类型
            if (lower == "boolean")
                return "bool";
            
            // 字符串类型
            if (lower == "text")
                return "string";
            
            // 数组类型
            if (lower.EndsWith("[]"))
            {
                var elementType = lower.Substring(0, lower.Length - 2);
                var normalizedElement = NormalizeTypeName(elementType);
                return normalizedElement + "[]";
            }
            
            return typeName;
        }
        
        private static readonly List<string> ScopeChoices = new List<string>
        {
            "common", "client", "server"
        };
        
        private void CreateBasicInfoCard(VisualElement parent)
        {
            var card = CreateCard(_selectedFile.isCsv ? "基本信息 (CSV)" : $"基本信息 - {_selectedSheet?.sheetName}");
            
            var fieldsContainer = new VisualElement();
            fieldsContainer.style.marginBottom = 12;
            
            // 类名和源文件
            var row1 = new VisualElement();
            row1.style.flexDirection = FlexDirection.Row;
            
            var classNameField = new TextField("类名");
            classNameField.value = _selectedSheet?.className ?? "";
            classNameField.AddToClassList("custom-textfield");
            classNameField.style.flexGrow = 1;
            classNameField.style.marginRight = 8;
            classNameField.RegisterValueChangedCallback(evt => {
                if (_selectedSheet != null)
                {
                    _selectedSheet.className = evt.newValue;
                    _data.Save();
                }
            });
            row1.Add(classNameField);
            
            var filePathField = new TextField("源文件");
            filePathField.value = _selectedFile.filePath ?? "";
            filePathField.AddToClassList("custom-textfield");
            filePathField.style.flexGrow = 2;
            filePathField.SetEnabled(false);
            row1.Add(filePathField);
            
            fieldsContainer.Add(row1);
            
            // 主键选择
            var row2 = new VisualElement();
            row2.style.flexDirection = FlexDirection.Row;
            row2.style.marginTop = 6;
            
            if (_selectedSheet != null && _selectedSheet.fields.Count > 0)
            {
                var fieldNames = _selectedSheet.fields.ConvertAll(f => f.fieldName);
                var currentKeyField = _selectedSheet.fields.Find(f => f.isKey);
                var currentKeyIndex = currentKeyField != null ? fieldNames.IndexOf(currentKeyField.fieldName) : 0;
                
                // 如果没有设置主键，默认第一列为主键
                if (currentKeyField == null && _selectedSheet.fields.Count > 0)
                {
                    _selectedSheet.fields[0].isKey = true;
                    _data.Save();
                    currentKeyIndex = 0;
                }
                
                var keyDropdown = new DropdownField("主键", fieldNames, currentKeyIndex);
                keyDropdown.AddToClassList("custom-textfield");
                keyDropdown.style.flexGrow = 1;
                keyDropdown.RegisterValueChangedCallback(evt => {
                    // 清除所有字段的isKey标记
                    foreach (var f in _selectedSheet.fields)
                    {
                        f.isKey = false;
                    }
                    // 设置新的主键
                    var keyField = _selectedSheet.fields.Find(f => f.fieldName == evt.newValue);
                    if (keyField != null)
                    {
                        keyField.isKey = true;
                    }
                    _data.Save();
                });
                row2.Add(keyDropdown);
            }
            
            fieldsContainer.Add(row2);
            
            // 操作按钮行（右对齐）
            var actionsRow = new VisualElement();
            actionsRow.style.flexDirection = FlexDirection.Row;
            actionsRow.style.justifyContent = Justify.FlexEnd;
            actionsRow.style.marginTop = 8;
            
            var generateCodeBtn = new Button(() => {
                if (_selectedSheet != null)
                    ConfigCodeGenerator.GenerateConfigClass(_selectedFile, _selectedSheet);
            });
            generateCodeBtn.text = "生成代码";
            generateCodeBtn.AddToClassList("btn");
            generateCodeBtn.AddToClassList("btn-primary");
            generateCodeBtn.AddToClassList("btn-sm");
            generateCodeBtn.style.marginRight = 8;
            actionsRow.Add(generateCodeBtn);
            
            var generateDataBtn = new Button(() => {
                if (_selectedSheet != null)
                    ConfigCodeGenerator.GenerateJsonData(_selectedFile, _selectedSheet);
            });
            generateDataBtn.text = "生成数据";
            generateDataBtn.AddToClassList("btn");
            generateDataBtn.AddToClassList("btn-primary");
            generateDataBtn.AddToClassList("btn-sm");
            generateDataBtn.style.marginRight = 8;
            actionsRow.Add(generateDataBtn);
            
            var generateAllBtn = new Button(() => {
                if (_selectedSheet != null)
                    ConfigCodeGenerator.GenerateAll(_selectedFile, _selectedSheet);
            });
            generateAllBtn.text = "全部生成";
            generateAllBtn.AddToClassList("btn");
            generateAllBtn.AddToClassList("btn-success");
            generateAllBtn.AddToClassList("btn-sm");
            actionsRow.Add(generateAllBtn);
            
            fieldsContainer.Add(actionsRow);
            card.Add(fieldsContainer);
            parent.Add(card);
        }
        
        private void CreateExcelTableCard(VisualElement parent)
        {
            if (_selectedSheet == null) return;
            
            int totalRows = _selectedSheet.previewData?.Count ?? 0;
            int totalPages = totalRows > 0 ? (int)Math.Ceiling((double)totalRows / PageSize) : 1;
            _currentPage = Math.Clamp(_currentPage, 0, Math.Max(0, totalPages - 1));
            
            var card = new VisualElement();
            card.AddToClassList("info-card");
            card.style.marginBottom = 16;
            
            // 标题行（包含重新加载按钮）
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.justifyContent = Justify.SpaceBetween;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 8;
            
            var titleLabel = new Label($"配置数据 ({_selectedSheet.fields.Count} 列, {totalRows} 行)");
            titleLabel.AddToClassList("card-title");
            titleRow.Add(titleLabel);
            
            var reloadBtn = new Button(() => ReloadPreview());
            reloadBtn.text = "🔄 重新加载";
            reloadBtn.AddToClassList("btn");
            reloadBtn.AddToClassList("btn-secondary");
            reloadBtn.AddToClassList("btn-sm");
            reloadBtn.style.fontSize = 11;
            titleRow.Add(reloadBtn);
            
            card.Add(titleRow);
            
            // 初始化列宽
            InitColumnWidths();
            _allColumnCells.Clear();
            
            // 水平滚动容器
            var scrollView = new ScrollView(ScrollViewMode.Horizontal);
            scrollView.style.flexGrow = 1;
            scrollView.style.maxHeight = 500;
            
            // 表格容器
            var tableContainer = new VisualElement();
            tableContainer.style.borderTopWidth = 1;
            tableContainer.style.borderBottomWidth = 1;
            tableContainer.style.borderLeftWidth = 1;
            tableContainer.style.borderRightWidth = 1;
            tableContainer.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
            tableContainer.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            tableContainer.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);
            tableContainer.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
            tableContainer.style.borderTopLeftRadius = 4;
            tableContainer.style.borderTopRightRadius = 4;
            tableContainer.style.borderBottomLeftRadius = 4;
            tableContainer.style.borderBottomRightRadius = 4;
            tableContainer.style.overflow = Overflow.Hidden;
            
            // 第一行：字段名
            var nameRow = CreateTableRow(0, true);
            for (int i = 0; i < _selectedSheet.fields.Count; i++)
            {
                var field = _selectedSheet.fields[i];
                int colIdx = i;
                var cellContainer = CreateCellWithResizer(colIdx, () => CreateEditableCell(field.fieldName, 0, colIdx, (newValue) => {
                    field.fieldName = newValue;
                    field.isKey = newValue.ToLower() == "id" || newValue.ToLower() == "key";
                    _data.Save();
                }));
                nameRow.Add(cellContainer);
            }
            tableContainer.Add(nameRow);
            
            // 第二行：作用域
            var scopeRow = CreateTableRow(1, true);
            for (int i = 0; i < _selectedSheet.fields.Count; i++)
            {
                var field = _selectedSheet.fields[i];
                int colIdx = i;
                var cell = CreateScopeDropdownCell(field, colIdx);
                ApplyColumnWidth(cell, colIdx);
                scopeRow.Add(cell);
            }
            tableContainer.Add(scopeRow);
            
            // 第三行：字段类型
            var typeRow = CreateTableRow(2, true);
            for (int i = 0; i < _selectedSheet.fields.Count; i++)
            {
                var field = _selectedSheet.fields[i];
                int colIdx = i;
                var cell = CreateTypeDropdownCell(field, colIdx);
                ApplyColumnWidth(cell, colIdx);
                typeRow.Add(cell);
            }
            tableContainer.Add(typeRow);
            
            // 第四行：注释
            var commentRow = CreateTableRow(3, true);
            for (int i = 0; i < _selectedSheet.fields.Count; i++)
            {
                var field = _selectedSheet.fields[i];
                int colIdx = i;
                var cell = CreateEditableCell(field.comment, 3, colIdx, (newValue) => {
                    field.comment = newValue;
                    _data.Save();
                });
                ApplyColumnWidth(cell, colIdx);
                commentRow.Add(cell);
            }
            tableContainer.Add(commentRow);
            
            // 数据行（分页显示）
            if (_selectedSheet.previewData != null && _selectedSheet.previewData.Count > 0)
            {
                int startIdx = _currentPage * PageSize;
                int endIdx = Math.Min(startIdx + PageSize, _selectedSheet.previewData.Count);
                
                for (int rowIdx = startIdx; rowIdx < endIdx; rowIdx++)
                {
                    var dataRow = _selectedSheet.previewData[rowIdx];
                    var row = CreateTableRow(4 + (rowIdx - startIdx), false);
                    
                    for (int colIdx = 0; colIdx < _selectedSheet.fields.Count; colIdx++)
                    {
                        var value = colIdx < dataRow.Count ? dataRow[colIdx] : "";
                        var cell = CreateDataCell(value, rowIdx, colIdx);
                        ApplyColumnWidth(cell, colIdx);
                        row.Add(cell);
                    }
                    tableContainer.Add(row);
                }
            }
            else
            {
                var emptyRow = new VisualElement();
                emptyRow.style.height = 40;
                emptyRow.style.alignItems = Align.Center;
                emptyRow.style.justifyContent = Justify.Center;
                emptyRow.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
                
                var emptyLabel = new Label("暂无预览数据，点击\"重新加载\"加载数据");
                emptyLabel.style.fontSize = 11;
                emptyLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                emptyRow.Add(emptyLabel);
                
                tableContainer.Add(emptyRow);
            }
            
            scrollView.Add(tableContainer);
            card.Add(scrollView);
            
            // 分页控件（只有超过一页时显示）
            if (totalPages > 1)
            {
                CreatePagination(card, totalPages);
            }
            
            parent.Add(card);
        }
        
        private void CreatePagination(VisualElement parent, int totalPages)
        {
            var paginationWrapper = new VisualElement();
            paginationWrapper.style.flexDirection = FlexDirection.Row;
            paginationWrapper.style.justifyContent = Justify.Center;
            paginationWrapper.style.alignItems = Align.Center;
            paginationWrapper.style.marginTop = 12;
            
            // 分页容器
            var pagination = new VisualElement();
            pagination.style.flexDirection = FlexDirection.Row;
            pagination.style.alignItems = Align.Center;
            pagination.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            pagination.style.borderTopLeftRadius = 6;
            pagination.style.borderTopRightRadius = 6;
            pagination.style.borderBottomLeftRadius = 6;
            pagination.style.borderBottomRightRadius = 6;
            pagination.style.paddingLeft = 4;
            pagination.style.paddingRight = 4;
            pagination.style.paddingTop = 4;
            pagination.style.paddingBottom = 4;
            
            // 上一页按钮
            var prevBtn = new Button(() => {
                if (_currentPage > 0)
                {
                    _currentPage--;
                    RefreshDetailView();
                }
            });
            prevBtn.text = "◀";
            prevBtn.style.width = 32;
            prevBtn.style.height = 28;
            prevBtn.style.fontSize = 12;
            prevBtn.style.borderTopLeftRadius = 4;
            prevBtn.style.borderBottomLeftRadius = 4;
            prevBtn.style.borderTopRightRadius = 0;
            prevBtn.style.borderBottomRightRadius = 0;
            prevBtn.style.borderTopWidth = 0;
            prevBtn.style.borderBottomWidth = 0;
            prevBtn.style.borderLeftWidth = 0;
            prevBtn.style.borderRightWidth = 0;
            prevBtn.style.backgroundColor = _currentPage > 0 
                ? new Color(0.25f, 0.25f, 0.25f) 
                : new Color(0.15f, 0.15f, 0.15f);
            prevBtn.style.color = _currentPage > 0 
                ? new Color(0.8f, 0.8f, 0.8f) 
                : new Color(0.4f, 0.4f, 0.4f);
            pagination.Add(prevBtn);
            
            // 页码显示区域
            var pageContainer = new VisualElement();
            pageContainer.style.minWidth = 100;
            pageContainer.style.height = 28;
            pageContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            pageContainer.style.alignItems = Align.Center;
            pageContainer.style.justifyContent = Justify.Center;
            
            var pageLabel = new Label($"{_currentPage + 1} / {totalPages}");
            pageLabel.style.fontSize = 12;
            pageLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            pageLabel.pickingMode = PickingMode.Ignore;
            pageContainer.Add(pageLabel);
            
            // 单击显示下拉框，双击显示输入框
            double lastClickTime = 0;
            pageContainer.RegisterCallback<PointerDownEvent>(evt => {
                double currentTime = EditorApplication.timeSinceStartup;
                if (currentTime - lastClickTime < 0.3)
                {
                    // 双击：显示输入框
                    ShowPageInput(pageContainer, pageLabel, totalPages);
                    lastClickTime = 0;
                }
                else
                {
                    // 单击：显示下拉框
                    lastClickTime = currentTime;
                    pageContainer.schedule.Execute(() => {
                        if (Math.Abs(lastClickTime - currentTime) < 0.01)
                        {
                            ShowPageDropdown(pageContainer, pageLabel, totalPages);
                        }
                    }).ExecuteLater(310);
                }
            });
            
            pagination.Add(pageContainer);
            
            // 下一页按钮
            var nextBtn = new Button(() => {
                if (_currentPage < totalPages - 1)
                {
                    _currentPage++;
                    RefreshDetailView();
                }
            });
            nextBtn.text = "▶";
            nextBtn.style.width = 32;
            nextBtn.style.height = 28;
            nextBtn.style.fontSize = 12;
            nextBtn.style.borderTopLeftRadius = 0;
            nextBtn.style.borderBottomLeftRadius = 0;
            nextBtn.style.borderTopRightRadius = 4;
            nextBtn.style.borderBottomRightRadius = 4;
            nextBtn.style.borderTopWidth = 0;
            nextBtn.style.borderBottomWidth = 0;
            nextBtn.style.borderLeftWidth = 0;
            nextBtn.style.borderRightWidth = 0;
            nextBtn.style.backgroundColor = _currentPage < totalPages - 1 
                ? new Color(0.25f, 0.25f, 0.25f) 
                : new Color(0.15f, 0.15f, 0.15f);
            nextBtn.style.color = _currentPage < totalPages - 1 
                ? new Color(0.8f, 0.8f, 0.8f) 
                : new Color(0.4f, 0.4f, 0.4f);
            pagination.Add(nextBtn);
            
            paginationWrapper.Add(pagination);
            parent.Add(paginationWrapper);
        }
        
        private void ShowPageDropdown(VisualElement container, Label pageLabel, int totalPages)
        {
            pageLabel.style.display = DisplayStyle.None;
            
            var choices = new List<string>();
            for (int i = 1; i <= totalPages; i++)
            {
                choices.Add($"第 {i} 页");
            }
            
            var dropdown = new DropdownField(choices, _currentPage);
            dropdown.style.width = 90;
            dropdown.style.marginTop = -4;
            dropdown.style.marginBottom = -4;
            container.Add(dropdown);
            
            dropdown.schedule.Execute(() => dropdown.Focus());
            
            bool committed = false;
            dropdown.RegisterValueChangedCallback(e => {
                if (committed) return;
                committed = true;
                _currentPage = choices.IndexOf(e.newValue);
                pageLabel.style.display = DisplayStyle.Flex;
                if (dropdown.parent != null)
                    dropdown.RemoveFromHierarchy();
                RefreshDetailView();
            });
            
            dropdown.RegisterCallback<FocusOutEvent>(e => {
                if (committed) return;
                committed = true;
                pageLabel.style.display = DisplayStyle.Flex;
                if (dropdown.parent != null)
                    dropdown.RemoveFromHierarchy();
            });
        }
        
        private void ShowPageInput(VisualElement container, Label pageLabel, int totalPages)
        {
            pageLabel.style.display = DisplayStyle.None;
            
            var textField = new TextField();
            textField.value = (_currentPage + 1).ToString();
            textField.style.width = 50;
            textField.style.marginTop = -4;
            textField.style.marginBottom = -4;
            container.Add(textField);
            
            textField.schedule.Execute(() => {
                textField.Focus();
                textField.SelectAll();
            });
            
            bool committed = false;
            Action commitEdit = () => {
                if (committed) return;
                committed = true;
                
                if (int.TryParse(textField.value, out int page))
                {
                    page = Math.Clamp(page, 1, totalPages);
                    _currentPage = page - 1;
                }
                
                pageLabel.style.display = DisplayStyle.Flex;
                if (textField.parent != null)
                    textField.RemoveFromHierarchy();
                RefreshDetailView();
            };
            
            textField.RegisterCallback<FocusOutEvent>(e => commitEdit());
            textField.RegisterCallback<KeyDownEvent>(e => {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    commitEdit();
                    e.StopPropagation();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    committed = true;
                    pageLabel.style.display = DisplayStyle.Flex;
                    if (textField.parent != null)
                        textField.RemoveFromHierarchy();
                    e.StopPropagation();
                }
            });
        }
        
        private void InitColumnWidths()
        {
            if (_selectedSheet == null) return;
            
            int fieldCount = _selectedSheet.fields.Count;
            
            while (_columnWidths.Count < fieldCount)
            {
                _columnWidths.Add(DefaultColumnWidth);
            }
            
            while (_columnWidths.Count > fieldCount)
            {
                _columnWidths.RemoveAt(_columnWidths.Count - 1);
            }
        }
        
        private void ApplyColumnWidth(VisualElement cell, int colIndex)
        {
            if (colIndex >= 0 && colIndex < _columnWidths.Count)
            {
                cell.style.width = _columnWidths[colIndex];
                cell.style.minWidth = _columnWidths[colIndex];
                cell.style.maxWidth = _columnWidths[colIndex];
                cell.style.flexGrow = 0;
                cell.style.flexShrink = 0;
                _allColumnCells.Add(cell);
            }
        }
        
        private VisualElement CreateCellWithResizer(int colIndex, Func<VisualElement> createCell)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexGrow = 0;
            container.style.flexShrink = 0;
            
            var cell = createCell();
            ApplyColumnWidth(cell, colIndex);
            cell.userData = colIndex;
            container.Add(cell);
            
            // 拖拽调整列宽的手柄
            var resizer = new VisualElement();
            resizer.style.width = 6;
            resizer.style.backgroundColor = new Color(0, 0, 0, 0);
            resizer.style.position = Position.Absolute;
            resizer.style.right = 0;
            resizer.style.top = 0;
            resizer.style.bottom = 0;
            resizer.AddToClassList("column-resizer");
            
            float startX = 0;
            float startWidth = 0;
            bool isDragging = false;
            
            resizer.RegisterCallback<PointerDownEvent>(evt => {
                if (evt.button == 0)
                {
                    isDragging = true;
                    startX = evt.position.x;
                    startWidth = _columnWidths[colIndex];
                    resizer.CapturePointer(evt.pointerId);
                    resizer.style.backgroundColor = new Color(0.4f, 0.6f, 0.8f, 0.8f);
                    evt.StopPropagation();
                }
            });
            
            resizer.RegisterCallback<PointerMoveEvent>(evt => {
                if (isDragging)
                {
                    float delta = evt.position.x - startX;
                    float newWidth = Mathf.Clamp(startWidth + delta, MinColumnWidth, MaxColumnWidth);
                    _columnWidths[colIndex] = newWidth;
                    
                    cell.style.width = newWidth;
                    cell.style.minWidth = newWidth;
                    cell.style.maxWidth = newWidth;
                }
            });
            
            resizer.RegisterCallback<PointerUpEvent>(evt => {
                if (isDragging)
                {
                    isDragging = false;
                    resizer.ReleasePointer(evt.pointerId);
                    resizer.style.backgroundColor = new Color(0, 0, 0, 0);
                    RefreshDetailView();
                }
            });
            
            resizer.RegisterCallback<PointerLeaveEvent>(evt => {
                if (!isDragging)
                    resizer.style.backgroundColor = new Color(0, 0, 0, 0);
            });
            
            cell.Add(resizer);
            
            return container;
        }
        
        private VisualElement CreateTableRow(int rowIndex, bool isHeader)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.minHeight = 28;
            
            if (isHeader)
            {
                row.style.backgroundColor = rowIndex switch
                {
                    0 => new Color(0.2f, 0.3f, 0.4f),
                    1 => new Color(0.25f, 0.35f, 0.25f),
                    2 => new Color(0.3f, 0.25f, 0.2f),
                    _ => new Color(0.22f, 0.22f, 0.22f)
                };
            }
            else
            {
                row.style.backgroundColor = rowIndex % 2 == 0 
                    ? new Color(0.2f, 0.2f, 0.2f) 
                    : new Color(0.18f, 0.18f, 0.18f);
            }
            
            if (rowIndex > 0)
            {
                row.style.borderTopWidth = 1;
                row.style.borderTopColor = new Color(0.15f, 0.15f, 0.15f);
            }
            
            return row;
        }
        
        private VisualElement CreateEditableCell(string value, int rowIndex, int colIndex, Action<string> onValueChanged)
        {
            var cell = new VisualElement();
            cell.style.paddingLeft = 6;
            cell.style.paddingRight = 6;
            cell.style.paddingTop = 4;
            cell.style.paddingBottom = 4;
            cell.style.justifyContent = Justify.Center;
            cell.style.borderRightWidth = 1;
            cell.style.borderRightColor = new Color(0.15f, 0.15f, 0.15f);
            
            var label = new Label(value);
            label.style.fontSize = 11;
            label.style.color = new Color(0.9f, 0.9f, 0.9f);
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.pickingMode = PickingMode.Ignore;
            cell.Add(label);
            
            double lastClickTime = 0;
            cell.RegisterCallback<PointerDownEvent>(evt => {
                double currentTime = EditorApplication.timeSinceStartup;
                if (currentTime - lastClickTime < 0.3)
                {
                    label.style.display = DisplayStyle.None;
                    
                    var textField = new TextField();
                    textField.value = label.text;
                    textField.style.flexGrow = 1;
                    textField.style.marginTop = -2;
                    textField.style.marginBottom = -2;
                    cell.Add(textField);
                    
                    textField.schedule.Execute(() => {
                        textField.Focus();
                        textField.SelectAll();
                    });
                    
                    bool committed = false;
                    Action commitEdit = () => {
                        if (committed) return;
                        committed = true;
                        var newValue = textField.value;
                        label.text = newValue;
                        label.style.display = DisplayStyle.Flex;
                        if (textField.parent != null)
                            textField.RemoveFromHierarchy();
                        onValueChanged?.Invoke(newValue);
                    };
                    
                    textField.RegisterCallback<FocusOutEvent>(e => commitEdit());
                    textField.RegisterCallback<KeyDownEvent>(e => {
                        if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                        {
                            commitEdit();
                            e.StopPropagation();
                        }
                        else if (e.keyCode == KeyCode.Escape)
                        {
                            committed = true;
                            label.style.display = DisplayStyle.Flex;
                            if (textField.parent != null)
                                textField.RemoveFromHierarchy();
                            e.StopPropagation();
                        }
                    });
                    
                    lastClickTime = 0;
                }
                else
                {
                    lastClickTime = currentTime;
                }
            });
            
            return cell;
        }
        
        private VisualElement CreateTypeDropdownCell(ConfigFieldInfo field, int colIndex)
        {
            var cell = new VisualElement();
            cell.userData = colIndex;
            cell.style.paddingLeft = 0;
            cell.style.paddingRight = 0;
            cell.style.paddingTop = 0;
            cell.style.paddingBottom = 0;
            cell.style.justifyContent = Justify.Center;
            cell.style.borderRightWidth = 1;
            cell.style.borderRightColor = new Color(0.15f, 0.15f, 0.15f);
            
            // 规范化类型名（将int32等别名转换为标准类型）
            var normalizedType = NormalizeTypeName(field.fieldType);
            var currentIndex = TypeChoices.IndexOf(normalizedType);
            if (currentIndex < 0)
            {
                // 如果规范化后仍然不在列表中，尝试直接匹配原始类型
                currentIndex = TypeChoices.IndexOf(field.fieldType);
            }
            if (currentIndex < 0) currentIndex = 2; // 默认string
            
            // 如果类型被规范化了，更新字段类型
            if (normalizedType != field.fieldType && TypeChoices.Contains(normalizedType))
            {
                field.fieldType = normalizedType;
                _data.Save();
            }
            
            var dropdown = new DropdownField(TypeChoices, currentIndex);
            dropdown.style.flexGrow = 1;
            dropdown.style.marginTop = 0;
            dropdown.style.marginBottom = 0;
            dropdown.style.marginLeft = 0;
            dropdown.style.marginRight = 0;
            
            // 背景透明，移除边框
            var inputElement = dropdown.Q(className: "unity-base-field__input");
            inputElement.style.backgroundColor = new Color(0, 0, 0, 0);
            inputElement.style.borderTopWidth = 0;
            inputElement.style.borderBottomWidth = 0;
            inputElement.style.borderLeftWidth = 0;
            inputElement.style.borderRightWidth = 0;
            
            dropdown.RegisterValueChangedCallback(e => {
                field.fieldType = e.newValue;
                _data.Save();
            });
            
            cell.Add(dropdown);
            _allColumnCells.Add(cell);
            return cell;
        }
        
        private VisualElement CreateScopeDropdownCell(ConfigFieldInfo field, int colIndex)
        {
            var cell = new VisualElement();
            cell.userData = colIndex;
            cell.style.paddingLeft = 0;
            cell.style.paddingRight = 0;
            cell.style.paddingTop = 0;
            cell.style.paddingBottom = 0;
            cell.style.justifyContent = Justify.Center;
            cell.style.borderRightWidth = 1;
            cell.style.borderRightColor = new Color(0.15f, 0.15f, 0.15f);
            
            var scopeStr = field.scope.ToString().ToLower();
            var currentIndex = ScopeChoices.IndexOf(scopeStr);
            if (currentIndex < 0) currentIndex = 0;
            
            var dropdown = new DropdownField(ScopeChoices, currentIndex);
            dropdown.style.flexGrow = 1;
            dropdown.style.marginTop = 0;
            dropdown.style.marginBottom = 0;
            dropdown.style.marginLeft = 0;
            dropdown.style.marginRight = 0;
            
            // 背景透明，移除边框
            var inputElement = dropdown.Q(className: "unity-base-field__input");
            inputElement.style.backgroundColor = new Color(0, 0, 0, 0);
            inputElement.style.borderTopWidth = 0;
            inputElement.style.borderBottomWidth = 0;
            inputElement.style.borderLeftWidth = 0;
            inputElement.style.borderRightWidth = 0;
            
            dropdown.RegisterValueChangedCallback(e => {
                field.scope = e.newValue switch
                {
                    "client" => FieldScope.Client,
                    "server" => FieldScope.Server,
                    _ => FieldScope.Common
                };
                _data.Save();
            });
            
            cell.Add(dropdown);
            _allColumnCells.Add(cell);
            return cell;
        }
        
        private VisualElement CreateDataCell(string value, int rowIndex, int colIndex)
        {
            var cell = new VisualElement();
            cell.userData = colIndex;
            cell.style.paddingLeft = 6;
            cell.style.paddingRight = 6;
            cell.style.paddingTop = 4;
            cell.style.paddingBottom = 4;
            cell.style.justifyContent = Justify.Center;
            cell.style.borderRightWidth = 1;
            cell.style.borderRightColor = new Color(0.15f, 0.15f, 0.15f);
            
            var label = new Label(value);
            label.style.fontSize = 11;
            label.style.color = new Color(0.75f, 0.75f, 0.75f);
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            cell.Add(label);
            
            return cell;
        }
        

        
        private VisualElement CreateCard(string title)
        {
            var card = new VisualElement();
            card.AddToClassList("info-card");
            card.style.marginBottom = 16;
            
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("card-title");
            card.Add(titleLabel);
            
            return card;
        }
        
        private void ImportFile()
        {
            var path = EditorUtility.OpenFilePanel("选择配置文件", "", "csv,xlsx,xls");
            if (string.IsNullOrEmpty(path)) return;
            
            try
            {
                var file = ExcelImporter.ImportFromFile(path);
                if (file != null && file.sheets.Count > 0)
                {
                    _data.AddFile(file);
                    RefreshFileList();
                    SelectFile(file);
                    
                    Debug.Log($"Imported: {file.fileName} with {file.sheets.Count} sheet(s)");
                }
                else if (file != null && file.sheets.Count == 0)
                {
                    EditorUtility.DisplayDialog("导入提示", 
                        "未找到有效的配置表。\n\nExcel中Sheet名称需要以 c_, d_, db_ 开头。\n例如: c_item, d_skill, db_player", 
                        "确定");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("导入失败", ex.Message, "确定");
            }
        }
        

        
        private void ReloadPreview()
        {
            if (_selectedFile == null || _selectedSheet == null) return;
            ExcelImporter.ReloadSheetData(_selectedFile, _selectedSheet);
            RefreshDetailView();
        }
        
        private void GenerateAll()
        {
            if (_data.files.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有配置文件需要生成", "确定");
                return;
            }
            
            ConfigCodeGenerator.GenerateAllFiles();
            EditorUtility.DisplayDialog("完成", $"已生成所有配置表", "确定");
        }
    }
}
