using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 构建报告窗口 (UIToolkit版本)
    /// </summary>
    public class BuildReportWindow : EditorWindow
    {
        private enum TabType
        {
            Summary,
            BuildHistory
        }

        private AssetBundleBuilder.BuildReport _report;
        private Vector2 _scrollPosition;
        private bool _showBundleSizes = true;
        private string _searchFilter = "";

        // 历史记录相关
        private TabType _currentTab = TabType.Summary;
        private List<BuildHistoryRecord> _historyRecords;
        private int _selectedCompareIndex1 = 0;
        private int _selectedCompareIndex2 = 1;
        private BuildCompareResult _compareResult;
        private bool[] _foldoutStates = new bool[5]; // Added, Removed, Modified, Moved, Bundle变动
        private string _currentReportContent = "";  // 当前显示的报告内容

        // UIToolkit元素引用
        private VisualElement _root;
        private VisualElement _summaryTab;
        private VisualElement _historyTab;
        private Button _tabSummaryBtn;
        private Button _tabHistoryBtn;

        // Summary Tab元素
        private VisualElement _summaryEmptyState;
        private VisualElement _buildInfoCard;
        private VisualElement _buildInfoContent;
        private VisualElement _comparisonCard;
        private VisualElement _comparisonContent;
        private VisualElement _bundleListCard;
        private VisualElement _bundleListContent;
        private TextField _bundleSearchField;

        // History Tab元素
        private VisualElement _historyEmptyState;
        private VisualElement _compareSelectorCard;
        private VisualElement _compareSelectorContent;
        private VisualElement _compareResultCard;
        private VisualElement _compareResultContent;
        private VisualElement _historyListCard;
        private VisualElement _historyListContent;

        public static void ShowReport(AssetBundleBuilder.BuildReport report)
        {
            var window = GetWindow<BuildReportWindow>("Build Report");
            window.minSize = new Vector2(800, 600);
            window._report = report;

            // 自动加载历史记录并对比
            window.LoadHistoryRecords();
            window.AutoCompareWithPrevious();

            window.Show();
        }

        /// <summary>
        /// 直接打开窗口（不需要 report，可以查看历史记录）
        /// </summary>
        public static void ShowWindow(string reportPath = null)
        {
            var window = GetWindow<BuildReportWindow>("Build Report");
            window.minSize = new Vector2(800, 600);

            // 如果提供了报告路径，尝试加载
            if (!string.IsNullOrEmpty(reportPath) && File.Exists(reportPath))
            {
                // 从报告文件创建 BuildReport 对象
                var report = LoadReportFromFile(reportPath);
                if (report != null)
                {
                    window._report = report;
                    window.LoadHistoryRecords();
                    window.AutoCompareWithPrevious();
                }
            }
            else
            {
                // 没有报告，切换到 Build History 标签页
                window._currentTab = TabType.BuildHistory;
                window.LoadHistoryRecords();
            }

            window.Show();
        }

        /// <summary>
        /// 从报告文件加载 BuildReport（简化版）
        /// </summary>
        private static AssetBundleBuilder.BuildReport LoadReportFromFile(string reportPath)
        {
            try
            {
                var content = File.ReadAllText(reportPath);
                var outputPath = Path.GetDirectoryName(reportPath);

                // 创建一个基本的 BuildReport
                var report = new AssetBundleBuilder.BuildReport
                {
                    success = content.Contains("✓ Success"),
                    message = "",
                    outputPath = outputPath
                };

                return report;
            }
            catch (Exception ex)
            {
                Debug.LogError($"加载报告文件失败: {ex.Message}");
                return null;
            }
        }

        private void CreateGUI()
        {
            // 加载UXML
            var visualTree = EditorAssetLoader.LoadVisualTree("Resource/Window/Report/BuildReportWindow.uxml");

            if (visualTree == null)
            {
                Debug.LogError("Failed to load BuildReportWindow.uxml");
                return;
            }

            _root = visualTree.CloneTree();

            // 确保rootVisualElement填充整个窗口
            rootVisualElement.style.flexGrow = 1;
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            rootVisualElement.Add(_root);

            // 关键修复：强制设置_root的flexGrow
            _root.style.flexGrow = 1;
            _root.style.flexDirection = FlexDirection.Column;

            // 加载通用编辑器样式
            var commonStyleSheet = EditorAssetLoader.LoadStyleSheet("Common/Style/EditorCommonStyle.uss");

            if (commonStyleSheet != null)
            {
                _root.styleSheets.Add(commonStyleSheet);
            }

            // 获取UI元素引用
            InitializeUIReferences();

            // 强制设置布局样式
            SetupLayout();

            // 绑定事件
            BindEvents();

            // 刷新UI
            RefreshUI();
        }

        private void InitializeUIReferences()
        {
            // Tab按钮
            _tabSummaryBtn = _root.Q<Button>("tab-summary");
            _tabHistoryBtn = _root.Q<Button>("tab-history");

            // Summary Tab
            _summaryTab = _root.Q("summary-tab");
            _summaryEmptyState = _root.Q("summary-empty-state");
            _buildInfoCard = _root.Q("build-info-card");
            _buildInfoContent = _root.Q("build-info-content");
            _comparisonCard = _root.Q("comparison-card");
            _comparisonContent = _root.Q("comparison-content");
            _bundleListCard = _root.Q("bundle-list-card");
            _bundleListContent = _root.Q("bundle-list-content");
            _bundleSearchField = _root.Q<TextField>("bundle-search");

            // History Tab
            _historyTab = _root.Q("history-tab");
            _historyEmptyState = _root.Q("history-empty-state");
            _compareSelectorCard = _root.Q("compare-selector-card");
            _compareSelectorContent = _root.Q("compare-selector-content");
            _compareResultCard = _root.Q("compare-result-card");
            _compareResultContent = _root.Q("compare-result-content");
            _historyListCard = _root.Q("history-list-card");
            _historyListContent = _root.Q("history-list-content");
        }

        private void SetupLayout()
        {
            // 强制设置布局样式（确保填充整个窗口）
            var contentArea = _root.Q("content-area");
            contentArea.style.flexGrow = 1;

            _summaryTab.style.flexGrow = 1;
            _historyTab.style.flexGrow = 1;
        }

        private void BindEvents()
        {
            // Tab切换
            _tabSummaryBtn.clicked += () => SwitchTab(TabType.Summary);
            _tabHistoryBtn.clicked += () => SwitchTab(TabType.BuildHistory);

            // 搜索
            if (_bundleSearchField != null)
            {
                _bundleSearchField.RegisterValueChangedCallback(evt =>
                {
                    _searchFilter = evt.newValue;
                    RefreshBundleList();
                });
            }
        }

        private void SwitchTab(TabType tab)
        {
            _currentTab = tab;

            // 更新Tab按钮样式
            _tabSummaryBtn.RemoveFromClassList("btn-primary");
            _tabHistoryBtn.RemoveFromClassList("btn-primary");

            if (tab == TabType.Summary)
            {
                _tabSummaryBtn.AddToClassList("btn-primary");
                _summaryTab.style.display = DisplayStyle.Flex;
                _historyTab.style.display = DisplayStyle.None;
            }
            else
            {
                _tabHistoryBtn.AddToClassList("btn-primary");
                _summaryTab.style.display = DisplayStyle.None;
                _historyTab.style.display = DisplayStyle.Flex;
            }

            RefreshUI();
        }

        private void RefreshUI()
        {
            if (_currentTab == TabType.Summary)
            {
                RefreshSummaryTab();
            }
            else
            {
                RefreshHistoryTab();
            }
        }

        private void RefreshSummaryTab()
        {
            if (_report == null)
            {
                // 显示空状态
                _summaryEmptyState.style.display = DisplayStyle.Flex;
                _buildInfoCard.style.display = DisplayStyle.None;
                _comparisonCard.style.display = DisplayStyle.None;
                _bundleListCard.style.display = DisplayStyle.None;
            }
            else
            {
                // 显示内容
                _summaryEmptyState.style.display = DisplayStyle.None;
                _buildInfoCard.style.display = DisplayStyle.Flex;
                _bundleListCard.style.display = DisplayStyle.Flex;

                RefreshBuildInfo();
                RefreshBundleList();

                // 如果有对比结果，显示对比卡片
                if (_compareResult != null)
                {
                    _comparisonCard.style.display = DisplayStyle.Flex;
                    RefreshComparisonInfo();
                }
                else
                {
                    _comparisonCard.style.display = DisplayStyle.None;
                }
            }
        }

        private void RefreshHistoryTab()
        {
            if (_historyRecords == null || _historyRecords.Count == 0)
            {
                _historyEmptyState.style.display = DisplayStyle.Flex;
                _compareSelectorCard.style.display = DisplayStyle.None;
                _compareResultCard.style.display = DisplayStyle.None;
                _historyListCard.style.display = DisplayStyle.None;
            }
            else
            {
                _historyEmptyState.style.display = DisplayStyle.None;
                _compareSelectorCard.style.display = DisplayStyle.Flex;
                _historyListCard.style.display = DisplayStyle.Flex;

                RefreshHistoryList();

                if (_compareResult != null)
                {
                    _compareResultCard.style.display = DisplayStyle.Flex;
                    RefreshCompareResult();
                }
                else
                {
                    _compareResultCard.style.display = DisplayStyle.None;
                }
            }
        }

        #region UIToolkit刷新方法

        private void RefreshBuildInfo()
        {
            _buildInfoContent.Clear();

            if (_report == null) return;

            // 构建状态
            var statusLabel = new Label($"构建状态: {(_report.success ? "✓ 成功" : "✗ 失败")}");
            statusLabel.style.fontSize = 14;
            statusLabel.style.color = _report.success ? new Color(0.13f, 0.77f, 0.37f) : new Color(0.94f, 0.27f, 0.27f);
            statusLabel.style.marginBottom = 8;
            _buildInfoContent.Add(statusLabel);

            // 构建耗时
            if (_report.buildTime > 0)
            {
                var timeLabel = new Label($"构建耗时: {_report.buildTime:F2}秒");
                timeLabel.AddToClassList("field");
                _buildInfoContent.Add(timeLabel);
            }

            // 输出路径
            if (!string.IsNullOrEmpty(_report.outputPath))
            {
                var pathLabel = new Label($"输出路径: {_report.outputPath}");
                pathLabel.AddToClassList("field");
                _buildInfoContent.Add(pathLabel);
            }

            // 构建消息
            if (!string.IsNullOrEmpty(_report.message))
            {
                var msgLabel = new Label($"消息: {_report.message}");
                msgLabel.AddToClassList("field");
                msgLabel.style.whiteSpace = WhiteSpace.Normal;
                _buildInfoContent.Add(msgLabel);
            }
        }

        private void RefreshBundleList()
        {
            _bundleListContent.Clear();

            if (_report == null || _report.bundleSizes == null) return;

            // 过滤和排序
            var filteredBundles = _report.bundleSizes
                .Where(b => string.IsNullOrEmpty(_searchFilter) ||
                           b.Key.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(b => b.Value)
                .ToList();

            // 显示Bundle总数
            var countLabel = new Label($"Bundle总数: {filteredBundles.Count} / {_report.bundleSizes.Count}");
            countLabel.style.marginBottom = 8;
            countLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            _bundleListContent.Add(countLabel);

            // 创建Bundle列表
            foreach (var bundle in filteredBundles)
            {
                var bundleItem = CreateBundleItem(bundle.Key, bundle.Value);
                _bundleListContent.Add(bundleItem);
            }
        }

        private VisualElement CreateBundleItem(string bundleName, long size)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.paddingTop = 6;
            item.style.paddingBottom = 6;
            item.style.paddingLeft = 8;
            item.style.paddingRight = 8;
            item.style.backgroundColor = new Color(1f, 1f, 1f, 0.03f);
            item.style.borderTopLeftRadius = 4;
            item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = 4;
            item.style.borderBottomRightRadius = 4;
            item.style.marginBottom = 2;

            var nameLabel = new Label(bundleName);
            nameLabel.style.flexGrow = 1;
            nameLabel.style.color = new Color(0.86f, 0.86f, 0.86f);
            item.Add(nameLabel);

            var sizeLabel = new Label(FormatBytes(size));
            sizeLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            item.Add(sizeLabel);

            return item;
        }

        private void RefreshComparisonInfo()
        {
            _comparisonContent.Clear();

            if (_compareResult == null) return;

            // TODO: 实现对比信息显示
            var infoLabel = new Label("对比功能正在开发中...");
            infoLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            _comparisonContent.Add(infoLabel);
        }

        private void RefreshHistoryList()
        {
            _historyListContent.Clear();

            if (_historyRecords == null || _historyRecords.Count == 0) return;

            // TODO: 实现历史记录列表
            var infoLabel = new Label("历史记录功能正在开发中...");
            infoLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            _historyListContent.Add(infoLabel);
        }

        private void RefreshCompareResult()
        {
            _compareResultContent.Clear();

            if (_compareResult == null) return;

            // TODO: 实现对比结果显示
            var infoLabel = new Label("对比结果功能正在开发中...");
            infoLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            _compareResultContent.Add(infoLabel);
        }

        #endregion

        private void OnEnable()
        {
            // 初始化折叠状态
            for (int i = 0; i < _foldoutStates.Length; i++)
            {
                _foldoutStates[i] = true;
            }
        }

        #region IMGUI方法（保留作为参考，可以逐步迁移）

        private void OnGUI()
        {
            // IMGUI已被UIToolkit替代，保留此方法作为参考
            // 如果需要回退到IMGUI，可以取消注释
            /*
            if (_report == null)
            {
                EditorGUILayout.HelpBox("No build report available", MessageType.Info);
                return;
            }
            
            // Tab选择
            DrawTabs();
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            switch (_currentTab)
            {
                case TabType.Summary:
                    DrawSummary();
                    break;
                case TabType.BuildHistory:
                    DrawBuildHistory();
                    break;
            }
            
            EditorGUILayout.EndScrollView();
            */
        }

        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Toggle(_currentTab == TabType.Summary, "Build Summary", EditorStyles.toolbarButton))
                _currentTab = TabType.Summary;

            if (GUILayout.Toggle(_currentTab == TabType.BuildHistory, "Build History", EditorStyles.toolbarButton))
            {
                _currentTab = TabType.BuildHistory;
                LoadHistoryRecords();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }

        private void DrawSummary()
        {
            // 基本信息
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("构建信息", EditorStyles.boldLabel);

            // 使用自定义 GUIStyle 来设置文本颜色
            var statusStyle = new GUIStyle(EditorStyles.boldLabel);
            var statusColor = _report.success ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.8f, 0.3f, 0.3f);
            statusStyle.normal.textColor = statusColor;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Status:", GUILayout.Width(80));
            EditorGUILayout.LabelField(_report.success ? "✓ Success" : "✗ Failed", statusStyle);
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_report.message))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Message:", GUILayout.Width(80));
                EditorGUILayout.LabelField(_report.message);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField($"构建耗时: {_report.buildTime:F2} 秒");
            EditorGUILayout.LabelField($"Bundle 数量: {_report.totalBundles}");
            EditorGUILayout.LabelField($"资源总数: {_report.totalAssets}");
            EditorGUILayout.LabelField($"总大小: {FormatBytes(_report.totalSize)}");

            if (!string.IsNullOrEmpty(_report.outputPath))
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("输出路径:", EditorStyles.miniLabel);
                EditorGUILayout.SelectableLabel(_report.outputPath, EditorStyles.textField, GUILayout.Height(16));
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);

            // Bundle 详细列表
            DrawBundleList();

            EditorGUILayout.Space(10);

            // 显示与上次构建的对比（如果有）
            if (_compareResult != null)
            {
                DrawComparisonSummary();
            }
        }

        private void DrawComparisonSummary()
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("📊 与上次构建对比", EditorStyles.boldLabel);
            if (GUILayout.Button("刷新", GUILayout.Width(60)))
            {
                RefreshHistoryRecords();
                AutoCompareWithPrevious();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 检查对比结果是否存在
            if (_compareResult == null)
            {
                EditorGUILayout.LabelField("暂无历史记录可对比", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            var hasChanges = _compareResult.assetsAdded > 0 ||
                           _compareResult.assetsRemoved > 0 ||
                           _compareResult.assetsModified > 0 ||
                           _compareResult.assetsMoved > 0;

            if (!hasChanges)
            {
                EditorGUILayout.LabelField("✓ 与上次构建相比无变化", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            // 统计摘要
            EditorGUILayout.BeginHorizontal();
            if (_compareResult.assetsAdded > 0)
                EditorGUILayout.LabelField($"✅ {_compareResult.assetsAdded}", GUILayout.Width(60));
            if (_compareResult.assetsRemoved > 0)
                EditorGUILayout.LabelField($"❌ {_compareResult.assetsRemoved}", GUILayout.Width(60));
            if (_compareResult.assetsModified > 0)
                EditorGUILayout.LabelField($"⚠️ {_compareResult.assetsModified}", GUILayout.Width(60));
            if (_compareResult.assetsMoved > 0)
                EditorGUILayout.LabelField($"🔄 {_compareResult.assetsMoved}", GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"大小: {FormatSizeDiff(_compareResult.totalSizeDiff)}", GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 显示详细变动列表
            DrawDetailedAssetChanges();

            EditorGUILayout.EndVertical();
        }

        private void DrawDetailedAssetChanges()
        {
            if (_compareResult == null || _compareResult.assetChanges.Count == 0)
                return;

            EditorGUILayout.LabelField("变动详情:", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginVertical("box");

            // 按类型分组显示
            var modifiedAssets = _compareResult.assetChanges.Where(c => c.type == AssetChangeType.ContentModified).ToList();
            var addedAssets = _compareResult.assetChanges.Where(c => c.type == AssetChangeType.Added).ToList();
            var removedAssets = _compareResult.assetChanges.Where(c => c.type == AssetChangeType.Removed).ToList();
            var movedAssets = _compareResult.assetChanges.Where(c => c.type == AssetChangeType.BundleMoved).ToList();

            // 修改的资源（最重要，优先显示）
            if (modifiedAssets.Count > 0)
            {
                _foldoutStates[2] = EditorGUILayout.Foldout(_foldoutStates[2], $"⚠️ 修改资源 ({modifiedAssets.Count})", true, EditorStyles.foldoutHeader);
                if (_foldoutStates[2])
                {
                    foreach (var change in modifiedAssets)
                    {
                        DrawAssetChangeItem(change);
                    }
                }
                EditorGUILayout.Space(3);
            }

            // 新增的资源
            if (addedAssets.Count > 0)
            {
                _foldoutStates[0] = EditorGUILayout.Foldout(_foldoutStates[0], $"✅ 新增资源 ({addedAssets.Count})", true, EditorStyles.foldoutHeader);
                if (_foldoutStates[0])
                {
                    foreach (var change in addedAssets.Take(10)) // 只显示前10个
                    {
                        DrawAssetChangeItem(change);
                    }
                    if (addedAssets.Count > 10)
                        EditorGUILayout.LabelField($"... 还有 {addedAssets.Count - 10} 个", EditorStyles.centeredGreyMiniLabel);
                }
                EditorGUILayout.Space(3);
            }

            // 删除的资源
            if (removedAssets.Count > 0)
            {
                _foldoutStates[1] = EditorGUILayout.Foldout(_foldoutStates[1], $"❌ 删除资源 ({removedAssets.Count})", true, EditorStyles.foldoutHeader);
                if (_foldoutStates[1])
                {
                    foreach (var change in removedAssets.Take(10))
                    {
                        DrawAssetChangeItem(change);
                    }
                    if (removedAssets.Count > 10)
                        EditorGUILayout.LabelField($"... 还有 {removedAssets.Count - 10} 个", EditorStyles.centeredGreyMiniLabel);
                }
                EditorGUILayout.Space(3);
            }

            // 移动的资源
            if (movedAssets.Count > 0)
            {
                _foldoutStates[3] = EditorGUILayout.Foldout(_foldoutStates[3], $"🔄 移动资源 ({movedAssets.Count})", true, EditorStyles.foldoutHeader);
                if (_foldoutStates[3])
                {
                    foreach (var change in movedAssets.Take(10))
                    {
                        DrawAssetChangeItem(change);
                    }
                    if (movedAssets.Count > 10)
                        EditorGUILayout.LabelField($"... 还有 {movedAssets.Count - 10} 个", EditorStyles.centeredGreyMiniLabel);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAssetChangeItem(AssetChangeDetail change)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();

            // 资源路径（可点击选中）
            if (GUILayout.Button(Path.GetFileName(change.assetPath), EditorStyles.label, GUILayout.Width(200)))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(change.assetPath);
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            }

            // 资源类型
            EditorGUILayout.LabelField(change.assetType, EditorStyles.miniLabel, GUILayout.Width(100));

            // 变化描述
            if (change.type == AssetChangeType.ContentModified)
            {
                if (change.sizeDiff != 0)
                    EditorGUILayout.LabelField($"{FormatSizeDiff(change.sizeDiff)}", EditorStyles.miniLabel, GUILayout.Width(80));

                EditorGUILayout.LabelField($"内容已修改", EditorStyles.miniLabel);
            }
            else if (change.type == AssetChangeType.Added)
            {
                EditorGUILayout.LabelField($"→ {change.toBundle}", EditorStyles.miniLabel);
            }
            else if (change.type == AssetChangeType.Removed)
            {
                EditorGUILayout.LabelField($"← {change.fromBundle}", EditorStyles.miniLabel);
            }
            else if (change.type == AssetChangeType.BundleMoved)
            {
                EditorGUILayout.LabelField($"{change.fromBundle} → {change.toBundle}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();

            // 显示修改详情
            if (change.type == AssetChangeType.ContentModified)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"  {GetContentChangeDescription(change.assetType)}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawFileDiffLine(FileDiffLine diff)
        {
            EditorGUILayout.BeginHorizontal();

            var lineLabel = $"L{diff.lineNumber}";

            // 行号
            EditorGUILayout.LabelField(lineLabel, EditorStyles.miniLabel, GUILayout.Width(40));

            // 创建带颜色的样式
            var coloredStyle = new GUIStyle(EditorStyles.miniLabel);

            if (diff.type == "added")
            {
                // 新增行 - 绿色
                coloredStyle.normal.textColor = new Color(0.3f, 0.8f, 0.3f);
                EditorGUILayout.LabelField($"+ {diff.line}", coloredStyle);
            }
            else if (diff.type == "removed")
            {
                // 删除行 - 红色
                coloredStyle.normal.textColor = new Color(0.8f, 0.3f, 0.3f);
                EditorGUILayout.LabelField($"- {diff.line}", coloredStyle);
            }
            else if (diff.type == "modified")
            {
                // 修改行 - 黄色
                coloredStyle.normal.textColor = new Color(0.9f, 0.7f, 0.2f);
                EditorGUILayout.LabelField($"~ {diff.line}", coloredStyle);
            }
            else
            {
                // 未知类型
                EditorGUILayout.LabelField(diff.line, EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();
        }

        private string GetContentChangeDescription(string assetType)
        {
            switch (assetType)
            {
                case "SceneAsset":
                    return "场景内容已修改（GameObject、组件或设置变化）";
                case "GameObject": // Prefab
                    return "Prefab 结构已修改";
                case "Material":
                    return "材质已修改（Shader、属性或纹理引用）";
                case "AnimationClip":
                    return "动画数据已修改";
                case "ScriptableObject":
                    return "ScriptableObject 数据已修改";
                default:
                    return $"{assetType} 内容已修改";
            }
        }

        private void DrawBundleList()
        {
            if (_report.bundleSizes == null || _report.bundleSizes.Count == 0)
                return;

            EditorGUILayout.BeginVertical("box");

            _showBundleSizes = EditorGUILayout.Foldout(_showBundleSizes, $"📦 Bundle 列表 ({_report.bundleSizes.Count})", true, EditorStyles.foldoutHeader);

            if (_showBundleSizes)
            {
                EditorGUILayout.Space(3);

                // 搜索过滤
                _searchFilter = EditorGUILayout.TextField("搜索", _searchFilter);

                EditorGUILayout.Space(3);

                // 排序和过滤
                var sortedBundles = _report.bundleSizes
                    .Where(kvp => string.IsNullOrEmpty(_searchFilter) ||
                                  kvp.Key.Contains(_searchFilter))
                    .OrderByDescending(kvp => kvp.Value)
                    .ToList();

                // 列表
                foreach (var kvp in sortedBundles)
                {
                    DrawBundleItem(kvp.Key, kvp.Value);
                }

                if (sortedBundles.Count < _report.bundleSizes.Count)
                {
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField($"显示 {sortedBundles.Count} / {_report.bundleSizes.Count}", EditorStyles.centeredGreyMiniLabel);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBundleItem(string bundleName, long size)
        {
            EditorGUILayout.BeginHorizontal();

            // Bundle 名称
            EditorGUILayout.LabelField(bundleName, GUILayout.Width(250));

            // 大小
            EditorGUILayout.LabelField(FormatBytes(size), GUILayout.Width(80));

            // 百分比和进度条
            var percentage = (_report.totalSize > 0) ? (size / (float)_report.totalSize * 100) : 0;
            var rect = EditorGUILayout.GetControlRect(GUILayout.Height(16));
            EditorGUI.ProgressBar(rect, percentage / 100f, $"{percentage:F1}%");

            EditorGUILayout.EndHorizontal();
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F2} KB";
            else if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / 1024.0 / 1024.0:F2} MB";
            else
                return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
        }
        #endregion
        #region Build History

        private void LoadHistoryRecords()
        {
            // 只在必要时清空
            if (_historyRecords == null)
            {
                // 从报告中获取包名
                if (_report != null && !string.IsNullOrEmpty(_report.outputPath))
                {
                    // outputPath 格式: E:\GameFrameworkKit\Build\AssetBundles\NewPackage
                    // 我们需要获取最后一级目录名，即包名
                    var packageName = Path.GetFileName(_report.outputPath);

                    Debug.Log($"[BuildReportWindow] Loading history for package: {packageName}, path: {_report.outputPath}");

                    _historyRecords = BuildHistoryManager.LoadHistory(packageName);

                    Debug.Log($"[BuildReportWindow] Loaded {_historyRecords.Count} history records");
                }
                else
                {
                    Debug.Log("[BuildReportWindow] No output path in report, cannot load history");
                    _historyRecords = new List<BuildHistoryRecord>();
                }
            }
        }

        private void RefreshHistoryRecords()
        {
            // 强制重新加载
            _historyRecords = null;
            _compareResult = null;
            LoadHistoryRecords();
        }

        private void AutoCompareWithPrevious()
        {
            Debug.Log($"[BuildReportWindow] AutoCompareWithPrevious 被调用, 历史记录数: {_historyRecords?.Count ?? 0}");

            if (_historyRecords == null || _historyRecords.Count < 2)
            {
                Debug.Log($"[BuildReportWindow] 历史记录不足（需要>=2条，当前{_historyRecords?.Count ?? 0}条），无法自动对比");
                return;
            }

            // 找到最新的两次成功构建进行对比
            var successfulBuilds = _historyRecords
                .Select((record, index) => new { record, index })
                .Where(x => x.record.success)
                .ToList();

            if (successfulBuilds.Count < 2)
            {
                Debug.Log($"[BuildReportWindow] 成功构建记录不足（需要>=2条，当前{successfulBuilds.Count}条），无法自动对比");
                return;
            }

            // 自动对比最新的两次成功构建
            var index1 = successfulBuilds[0].index;
            var index2 = successfulBuilds[1].index;
            Debug.Log($"[BuildReportWindow] 自动对比最新两次成功构建: #{index1 + 1} vs #{index2 + 1}");
            CompareBuilds(index1, index2);
        }

        private void DrawBuildHistory()
        {
            if (_historyRecords == null || _historyRecords.Count == 0)
            {
                EditorGUILayout.HelpBox("没有构建历史记录", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("构建历史记录", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            // 历史记录列表
            DrawHistoryList();

            EditorGUILayout.Space(10);

            // 对比选择器
            DrawCompareSelector();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // 对比结果
            if (_compareResult != null)
            {
                DrawCompareResult();
            }
        }

        /// <summary>
        /// 加载报告内容
        /// </summary>
        private void LoadReportContent(BuildHistoryRecord record)
        {
            var backupFolder = Path.Combine("Build/Backup", record.buildTime.Replace(":", "-"));
            var reportPath = Path.Combine(backupFolder, "BuildReport.txt");

            if (File.Exists(reportPath))
            {
                try
                {
                    _currentReportContent = File.ReadAllText(reportPath);
                }
                catch (Exception ex)
                {
                    _currentReportContent = $"加载报告失败: {ex.Message}";
                }
            }
            else
            {
                _currentReportContent = $"找不到报告文件:\n{reportPath}";
            }
        }

        private void DrawHistoryList()
        {
            EditorGUILayout.LabelField("历史记录列表:", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            for (int i = 0; i < _historyRecords.Count; i++)
            {
                var record = _historyRecords[i];
                EditorGUILayout.BeginHorizontal("box");

                var statusIcon = record.success ? "✓" : "✗";
                var statusStyle = new GUIStyle(GUI.skin.label);
                statusStyle.normal.textColor = record.success ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.8f, 0.3f, 0.3f);

                EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(30));
                EditorGUILayout.LabelField(statusIcon, statusStyle, GUILayout.Width(20));

                EditorGUILayout.LabelField(record.buildTime, GUILayout.Width(150));
                EditorGUILayout.LabelField($"v{record.packageVersion}", GUILayout.Width(80));
                EditorGUILayout.LabelField($"{record.buildDuration:F2}s", GUILayout.Width(60));

                if (record.success)
                {
                    EditorGUILayout.LabelField($"{record.totalBundles} bundles", GUILayout.Width(100));
                    EditorGUILayout.LabelField(FormatBytes(record.totalSize), GUILayout.Width(100));
                }
                else
                {
                    // 失败时显示失败任务和错误信息
                    var errorStyle = new GUIStyle(EditorStyles.miniLabel);
                    errorStyle.normal.textColor = new Color(0.8f, 0.3f, 0.3f);

                    var failureInfo = "";
                    if (!string.IsNullOrEmpty(record.failedTask))
                    {
                        failureInfo = $"[{record.failedTask}] ";
                    }
                    failureInfo += record.errorMessage ?? "构建失败";

                    EditorGUILayout.LabelField(failureInfo, errorStyle);
                }

                // 添加"查看报告"按钮
                if (GUILayout.Button("查看报告", GUILayout.Width(80)))
                {
                    OpenBackupReport(record);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 打开备份文件夹中的构建报告
        /// </summary>
        private void OpenBackupReport(BuildHistoryRecord record)
        {
            var backupFolder = Path.Combine("Build/Backup", record.buildTime.Replace(":", "-"));
            var reportPath = Path.Combine(backupFolder, "BuildReport.txt");

            if (File.Exists(reportPath))
            {
                // 在系统默认编辑器中打开
                System.Diagnostics.Process.Start(reportPath);
            }
            else
            {
                EditorUtility.DisplayDialog("错误", $"找不到报告文件:\n{reportPath}", "确定");
            }
        }

        private void DrawCompareSelector()
        {
            // 检查是否有足够的成功构建记录
            var successfulBuilds = _historyRecords.Where(r => r.success).ToList();

            if (_historyRecords.Count < 2)
            {
                EditorGUILayout.HelpBox("至少需要2条构建记录才能进行对比", MessageType.Info);
                return;
            }

            if (successfulBuilds.Count < 2)
            {
                EditorGUILayout.HelpBox("至少需要2条成功的构建记录才能进行对比", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("对比构建:", GUILayout.Width(80));

            // 创建选项数组（标记失败的构建）
            var options = new string[_historyRecords.Count];
            for (int i = 0; i < _historyRecords.Count; i++)
            {
                var record = _historyRecords[i];
                var status = record.success ? "" : " [失败-不可选]";
                options[i] = $"#{i + 1} - {record.buildTime}{status}";
            }

            _selectedCompareIndex1 = EditorGUILayout.Popup(_selectedCompareIndex1, options, GUILayout.Width(250));
            EditorGUILayout.LabelField("vs", GUILayout.Width(30));
            _selectedCompareIndex2 = EditorGUILayout.Popup(_selectedCompareIndex2, options, GUILayout.Width(250));

            // 检查选中的是否都是成功的构建
            var canCompare = _historyRecords[_selectedCompareIndex1].success &&
                           _historyRecords[_selectedCompareIndex2].success;

            GUI.enabled = canCompare;
            if (GUILayout.Button("对比", GUILayout.Width(80)))
            {
                CompareBuilds(_selectedCompareIndex1, _selectedCompareIndex2);
            }
            GUI.enabled = true;

            if (!canCompare)
            {
                EditorGUILayout.LabelField("⚠️ 只能对比成功的构建", EditorStyles.miniLabel, GUILayout.Width(150));
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("导出报告", GUILayout.Width(100)))
            {
                ExportCompareReport();
            }

            if (GUILayout.Button("清除历史", GUILayout.Width(100)))
            {
                ClearHistory();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void CompareBuilds(int index1, int index2)
        {
            Debug.Log($"[BuildReportWindow] 开始对比构建 #{index1 + 1} vs #{index2 + 1}");

            if (index1 < 0 || index1 >= _historyRecords.Count ||
                index2 < 0 || index2 >= _historyRecords.Count)
            {
                EditorUtility.DisplayDialog("错误", "无效的构建索引", "确定");
                return;
            }

            var record1 = _historyRecords[index1];
            var record2 = _historyRecords[index2];

            Debug.Log($"[BuildReportWindow] Record1: {record1.buildTime}, {record1.assetSnapshots.Count} assets");
            Debug.Log($"[BuildReportWindow] Record2: {record2.buildTime}, {record2.assetSnapshots.Count} assets");

            _compareResult = BuildHistoryManager.CompareBuild(record1, record2);

            if (_compareResult != null)
            {
                Debug.Log($"[BuildReportWindow] 对比完成: +{_compareResult.assetsAdded} -{_compareResult.assetsRemoved} ~{_compareResult.assetsModified}");
                Repaint(); // 强制重绘窗口
            }
            else
            {
                Debug.LogError("[BuildReportWindow] 对比结果为空");
            }
        }

        private void DrawCompareResult()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("变动摘要", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // 摘要信息
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Bundles: +{_compareResult.bundlesAdded.Count} 新增, -{_compareResult.bundlesRemoved.Count} 删除, ~{_compareResult.bundleSizeChanges.Count} 修改");
            EditorGUILayout.LabelField($"Assets:  +{_compareResult.assetsAdded} 新增, -{_compareResult.assetsRemoved} 删除, ~{_compareResult.assetsModified} 修改, ↔{_compareResult.assetsMoved} 移动");
            EditorGUILayout.LabelField($"Size:    {FormatSizeDiff(_compareResult.totalSizeDiff)} ({FormatPercentage(_compareResult.totalSizeDiff, _compareResult.previous.totalSize)})");
            EditorGUILayout.LabelField($"Time:    {FormatTimeDiff(_compareResult.buildTimeDiff)}");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Bundle变动
            if (_compareResult.bundlesAdded.Count > 0 || _compareResult.bundlesRemoved.Count > 0)
            {
                _foldoutStates[4] = EditorGUILayout.Foldout(_foldoutStates[4], $"Bundle变动 (+{_compareResult.bundlesAdded.Count}/-{_compareResult.bundlesRemoved.Count})", true);
                if (_foldoutStates[4])
                {
                    DrawBundleChanges();
                }
                EditorGUILayout.Space(5);
            }

            // 资源变动详情
            DrawAssetChanges();

            EditorGUILayout.EndVertical();
        }

        private void DrawBundleChanges()
        {
            EditorGUILayout.BeginVertical("box");

            if (_compareResult.bundlesAdded.Count > 0)
            {
                EditorGUILayout.LabelField("新增Bundle:", EditorStyles.boldLabel);
                foreach (var bundleName in _compareResult.bundlesAdded)
                {
                    EditorGUILayout.LabelField($"  + {bundleName}", GUILayout.Height(16));
                }
                EditorGUILayout.Space(5);
            }

            if (_compareResult.bundlesRemoved.Count > 0)
            {
                EditorGUILayout.LabelField("删除Bundle:", EditorStyles.boldLabel);
                foreach (var bundleName in _compareResult.bundlesRemoved)
                {
                    EditorGUILayout.LabelField($"  - {bundleName}", GUILayout.Height(16));
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAssetChanges()
        {
            var addedAssets = _compareResult.assetChanges.Where(c => c.type == AssetChangeType.Added).ToList();
            var removedAssets = _compareResult.assetChanges.Where(c => c.type == AssetChangeType.Removed).ToList();
            var modifiedAssets = _compareResult.assetChanges.Where(c => c.type == AssetChangeType.ContentModified).ToList();
            var movedAssets = _compareResult.assetChanges.Where(c => c.type == AssetChangeType.BundleMoved).ToList();

            // 新增资源
            if (addedAssets.Count > 0)
            {
                _foldoutStates[0] = EditorGUILayout.Foldout(_foldoutStates[0], $"✅ 新增资源 ({addedAssets.Count})", true);
                if (_foldoutStates[0])
                {
                    DrawAssetChangeList(addedAssets, Color.green);
                }
                EditorGUILayout.Space(5);
            }

            // 删除资源
            if (removedAssets.Count > 0)
            {
                _foldoutStates[1] = EditorGUILayout.Foldout(_foldoutStates[1], $"❌ 删除资源 ({removedAssets.Count})", true);
                if (_foldoutStates[1])
                {
                    DrawAssetChangeList(removedAssets, Color.red);
                }
                EditorGUILayout.Space(5);
            }

            // 修改资源
            if (modifiedAssets.Count > 0)
            {
                _foldoutStates[2] = EditorGUILayout.Foldout(_foldoutStates[2], $"⚠️ 修改资源 ({modifiedAssets.Count})", true);
                if (_foldoutStates[2])
                {
                    DrawAssetChangeList(modifiedAssets, Color.yellow);
                }
                EditorGUILayout.Space(5);
            }

            // 移动资源
            if (movedAssets.Count > 0)
            {
                _foldoutStates[3] = EditorGUILayout.Foldout(_foldoutStates[3], $"🔄 移动资源 ({movedAssets.Count})", true);
                if (_foldoutStates[3])
                {
                    DrawAssetChangeList(movedAssets, Color.cyan);
                }
            }
        }

        private void DrawAssetChangeList(List<AssetChangeDetail> changes, Color labelColor)
        {
            EditorGUILayout.BeginVertical("box");

            var displayCount = Mathf.Min(changes.Count, 50); // 最多显示50个
            for (int i = 0; i < displayCount; i++)
            {
                var change = changes[i];
                var coloredStyle = new GUIStyle(EditorStyles.boldLabel);
                coloredStyle.normal.textColor = labelColor;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField(change.assetPath, coloredStyle);

                EditorGUILayout.LabelField($"  类型: {change.assetType}", GUILayout.Height(16));
                EditorGUILayout.LabelField($"  {change.GetDetailedDescription()}", GUILayout.Height(16));

                if (change.type == AssetChangeType.BundleMoved)
                {
                    EditorGUILayout.LabelField($"  {change.fromBundle} → {change.toBundle}", GUILayout.Height(16));
                }
                else if (change.type == AssetChangeType.Added)
                {
                    EditorGUILayout.LabelField($"  → {change.toBundle}", GUILayout.Height(16));
                }
                else if (change.type == AssetChangeType.Removed)
                {
                    EditorGUILayout.LabelField($"  ← {change.fromBundle}", GUILayout.Height(16));
                }
                else if (change.type == AssetChangeType.ContentModified)
                {
                    EditorGUILayout.LabelField($"  Bundle: {change.toBundle}", GUILayout.Height(16));

                    // 显示文件差异
                    if (change.fileDifferences != null && change.fileDifferences.Count > 0)
                    {
                        EditorGUILayout.Space(3);
                        EditorGUILayout.LabelField("  文件差异:", EditorStyles.boldLabel);

                        var displayDiffCount = Math.Min(change.fileDifferences.Count, 20);
                        for (int j = 0; j < displayDiffCount; j++)
                        {
                            DrawFileDiffLine(change.fileDifferences[j]);
                        }

                        if (change.fileDifferences.Count > displayDiffCount)
                        {
                            EditorGUILayout.LabelField($"    ... 还有 {change.fileDifferences.Count - displayDiffCount} 行变化",
                                EditorStyles.miniLabel);
                        }
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(3);
            }

            if (changes.Count > displayCount)
            {
                EditorGUILayout.LabelField($"... 还有 {changes.Count - displayCount} 个变动未显示", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void ExportCompareReport()
        {
            if (_compareResult == null)
            {
                EditorUtility.DisplayDialog("错误", "请先进行构建对比", "确定");
                return;
            }

            var reportText = BuildHistoryManager.ExportCompareReport(_compareResult);
            var path = EditorUtility.SaveFilePanel("导出对比报告", "", "BuildCompare.txt", "txt");

            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, reportText);
                EditorUtility.DisplayDialog("成功", $"对比报告已导出到:\n{path}", "确定");
            }
        }

        private void ClearHistory()
        {
            if (EditorUtility.DisplayDialog("确认", "确定要清除所有构建历史记录吗?", "确定", "取消"))
            {
                if (_report != null && !string.IsNullOrEmpty(_report.outputPath))
                {
                    var packageName = Path.GetFileName(_report.outputPath);
                    BuildHistoryManager.ClearHistory(packageName);
                    _historyRecords = null;
                    _compareResult = null;
                    LoadHistoryRecords();
                    Repaint();
                }
            }
        }

        private string FormatSizeDiff(long sizeDiff)
        {
            var sign = sizeDiff >= 0 ? "+" : "";
            var formatted = FormatBytes(Math.Abs(sizeDiff));
            return $"{sign}{formatted}";
        }

        private string FormatTimeDiff(float timeDiff)
        {
            var sign = timeDiff >= 0 ? "+" : "";
            return $"{sign}{timeDiff:F2}s";
        }

        private string FormatPercentage(long diff, long total)
        {
            if (total == 0)
                return "0%";

            var percentage = (diff / (float)total) * 100;
            var sign = percentage >= 0 ? "+" : "";
            return $"{sign}{percentage:F1}%";
        }

        #endregion
    }
}
