using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.LocalizationEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace GameDeveloperKit.LubanConfigEditor.UI
{
    public sealed partial class MainWindow : EditorWindow
    {
        private const string WindowTitle = "配置表工具";

        private sealed class SourceListItem
        {
            public SourceListItem(LubanSourceDescriptor source)
            {
                Source = source;
            }

            public SourceListItem(LubanSourceDescriptor source, LubanTableDescriptor table)
            {
                Source = source;
                Table = table;
            }

            public LubanSourceDescriptor Source { get; }

            public LubanTableDescriptor Table { get; }

            public bool IsTable => Table != null;

            public string StableId => IsTable ? Table.TableId : Source?.SourceId ?? string.Empty;
        }

        private EditorGlobalConfig m_GlobalConfig;
        private EditorUserConfig m_UserConfig;
        private ILubanSourceCatalog m_SourceCatalog;
        private LubanSourceSnapshot m_SourceSnapshot;
        private SourceListItem m_SelectedSourceItem;
        private LubanRunReport m_ReleaseReport;
        private LubanConfModel m_ConfModel;

        private readonly List<SourceListItem> m_SourceItems = new List<SourceListItem>();
        private readonly HashSet<string> m_ExpandedSourceIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> m_ExpandedTableIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> m_KnownSourceIds = new HashSet<string>(StringComparer.Ordinal);

        private Button m_HeaderRefreshButton;
        private Button m_HeaderCheckButton;
        private Button m_HeaderGenerateButton;
        private Button m_HeaderCancelButton;

        private Label m_StatusLabel;
        private Label m_VersionLabel;
        private Label m_ErrorLabel;
        private TextField m_CommandField;
        private TextField m_LogField;
        private VisualElement m_SourceTableBody;
        private Label m_SourceSummaryLabel;
        private TextField m_SearchField;
        private Toggle m_GenerateSelectedTableToggle;
        private Button m_GlobalSettingsToggle;
        private VisualElement m_ContentHost;
        private bool m_ShowGlobalSettings;

        private CancellationTokenSource m_RunCancellation;

        [MenuItem("GameDeveloperKit/" + WindowTitle)]
        public static void Open()
        {
            var window = GetWindow<MainWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(1020, 620);
            window.Show();
        }

        public void CreateGUI()
        {
            m_GlobalConfig = EditorGlobalConfig.LoadOrCreate();
            m_UserConfig = EditorUserConfig.LoadOrCreate();
            m_SourceCatalog = LubanSourceCatalog.Shared;
            BuildLayout();
            RefreshSourceCatalog();
            DetectRelease();
        }

        private void OnDisable()
        {
            CancelCurrentRun();
        }

        private CancellationToken BeginRun()
        {
            CancelCurrentRun();
            m_RunCancellation = new CancellationTokenSource();
            return m_RunCancellation.Token;
        }

        private void CancelCurrentRun()
        {
            m_RunCancellation?.Cancel();
            m_RunCancellation?.Dispose();
            m_RunCancellation = null;
            RefreshActionState();
        }

        private void BuildLayout()
        {
            rootVisualElement.Clear();

            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.minWidth = 0;
            root.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.15f, 0.16f, 0.18f)
                : new Color(0.94f, 0.96f, 0.98f);
            rootVisualElement.Add(root);

            root.Add(CreateHeader());

            m_ContentHost = new VisualElement { name = "configuration-content-host" };
            m_ContentHost.style.flexGrow = 1;
            m_ContentHost.style.minHeight = 0;
            m_ContentHost.style.minWidth = 0;
            root.Add(m_ContentHost);
            RefreshContentMode();

            root.Add(CreateStatusPanel());

            RebuildSourceTable();
            RefreshActionState();
        }

        private VisualElement CreateHeader()
        {
            var titleBar = new VisualElement();
            titleBar.name = "configuration-toolbar";
            titleBar.style.flexDirection = FlexDirection.Row;
            titleBar.style.alignItems = Align.Center;
            titleBar.style.minHeight = 38;
            titleBar.style.paddingLeft = 8;
            titleBar.style.paddingRight = 8;
            titleBar.style.borderBottomWidth = 1;
            titleBar.style.borderBottomColor = EditorGUIUtility.isProSkin
                ? new Color(0.28f, 0.3f, 0.33f)
                : new Color(0.82f, 0.86f, 0.9f);
            titleBar.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.18f, 0.19f, 0.21f)
                : Color.white;

            var title = new Label("配置表");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginRight = 12;
            titleBar.Add(title);

            m_SourceSummaryLabel = new Label();
            m_SourceSummaryLabel.style.color = EditorGUIUtility.isProSkin
                ? new Color(0.68f, 0.7f, 0.73f)
                : new Color(0.3f, 0.32f, 0.35f);
            m_SourceSummaryLabel.style.flexGrow = 1;
            titleBar.Add(m_SourceSummaryLabel);

            m_GenerateSelectedTableToggle = new Toggle("仅生成当前表");
            m_GenerateSelectedTableToggle.tooltip = "开启后只生成当前选中的配置表。";
            m_GenerateSelectedTableToggle.style.marginRight = 8;
            m_GenerateSelectedTableToggle.RegisterValueChangedCallback(_ =>
            {
                RefreshCommandPreview();
                RefreshActionState();
                RebuildSourceTable();
            });
            titleBar.Add(m_GenerateSelectedTableToggle);

            m_GlobalSettingsToggle = new Button(ToggleGlobalSettingsMode) { text = "全局设置" };
            m_GlobalSettingsToggle.name = "global-settings-toggle";
            m_GlobalSettingsToggle.tooltip = "在配置表列表与全局设置之间切换";
            m_GlobalSettingsToggle.style.height = 22;
            m_GlobalSettingsToggle.style.marginRight = 8;
            titleBar.Add(m_GlobalSettingsToggle);
            RefreshGlobalSettingsToggleStyle();

            var searchLabel = new Label("搜索");
            searchLabel.style.marginRight = 4;
            titleBar.Add(searchLabel);

            m_SearchField = new TextField();
            m_SearchField.name = "configuration-search-field";
            m_SearchField.style.width = 180;
            m_SearchField.style.marginRight = 6;
            m_SearchField.RegisterValueChangedCallback(_ => RebuildSourceTable());
            titleBar.Add(m_SearchField);

            m_HeaderRefreshButton = new Button(RefreshSourceCatalog) { text = "刷新" };
            AddHeaderButton(titleBar, m_HeaderRefreshButton);
            m_HeaderCheckButton = new Button(RunCheck) { text = "检查" };
            AddHeaderButton(titleBar, m_HeaderCheckButton);
            m_HeaderGenerateButton = new Button(RunGenerate) { text = "生成" };
            AddHeaderButton(titleBar, m_HeaderGenerateButton);
            m_HeaderCancelButton = new Button(CancelCurrentRun) { text = "取消" };
            AddHeaderButton(titleBar, m_HeaderCancelButton);
            return titleBar;
        }

        private static void AddHeaderButton(VisualElement parent, Button button)
        {
            button.style.marginLeft = 4;
            button.style.minWidth = 64;
            parent.Add(button);
        }

        private VisualElement CreateGlobalConfigurationView()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "global-settings-view" };
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            var panel = new EditorConfigurationPanel(() =>
                rootVisualElement.schedule.Execute(RefreshSourceCatalog));
            panel.name = "global-settings-content";
            scroll.Add(panel);
            return scroll;
        }

        private void ToggleGlobalSettingsMode()
        {
            m_ShowGlobalSettings = !m_ShowGlobalSettings;
            RefreshContentMode();
            RefreshActionState();
        }

        private void RefreshContentMode()
        {
            if (m_ContentHost == null)
            {
                return;
            }

            m_ContentHost.Clear();
            if (m_ShowGlobalSettings)
            {
                m_SourceTableBody = null;
                m_ContentHost.Add(CreateGlobalConfigurationView());
            }
            else
            {
                m_ContentHost.Add(CreateSourceTable());
                RebuildSourceTable();
            }

            m_SearchField?.SetEnabled(m_ShowGlobalSettings is false);
            m_GenerateSelectedTableToggle?.SetEnabled(m_ShowGlobalSettings is false);
            RefreshGlobalSettingsToggleStyle();
        }

        private void RefreshGlobalSettingsToggleStyle()
        {
            if (m_GlobalSettingsToggle == null)
            {
                return;
            }

            m_GlobalSettingsToggle.style.backgroundColor = m_ShowGlobalSettings
                ? (EditorGUIUtility.isProSkin ? new Color(0.14f, 0.43f, 0.38f) : new Color(0.55f, 0.82f, 0.76f))
                : (EditorGUIUtility.isProSkin ? new Color(0.24f, 0.25f, 0.27f) : new Color(0.82f, 0.83f, 0.85f));
            m_GlobalSettingsToggle.style.color = m_ShowGlobalSettings && EditorGUIUtility.isProSkin
                ? Color.white
                : EditorGUIUtility.isProSkin
                    ? new Color(0.82f, 0.83f, 0.85f)
                    : new Color(0.14f, 0.15f, 0.17f);
        }

        private VisualElement CreateSourceTable()
        {
            var table = new VisualElement();
            table.name = "configuration-source-table";
            table.style.flexGrow = 1;
            table.style.minHeight = 0;
            table.style.marginLeft = 8;
            table.style.marginRight = 8;
            table.style.marginTop = 8;
            table.style.borderLeftWidth = 1;
            table.style.borderRightWidth = 1;
            table.style.borderTopWidth = 1;
            table.style.borderBottomWidth = 1;
            var borderColor = EditorGUIUtility.isProSkin
                ? new Color(0.28f, 0.29f, 0.31f)
                : new Color(0.72f, 0.74f, 0.77f);
            table.style.borderLeftColor = borderColor;
            table.style.borderRightColor = borderColor;
            table.style.borderTopColor = borderColor;
            table.style.borderBottomColor = borderColor;

            var header = new VisualElement();
            header.name = "configuration-source-table-header";
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.height = 28;
            header.style.paddingLeft = 8;
            header.style.paddingRight = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = borderColor;
            header.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.2f, 0.21f, 0.23f)
                : new Color(0.84f, 0.86f, 0.89f);
            header.Add(CreateColumnLabel("名称", 4, 260));
            header.Add(CreateColumnLabel("来源", 3, 220));
            header.Add(CreateColumnLabel("状态", 1, 110));
            header.Add(CreateColumnLabel(string.Empty, 0, 72));
            table.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.name = "configuration-source-table-scroll";
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            m_SourceTableBody = new VisualElement { name = "configuration-source-table-body" };
            scroll.Add(m_SourceTableBody);
            table.Add(scroll);
            return table;
        }

        private static Label CreateColumnLabel(string text, float grow, float basis)
        {
            var label = new Label(text);
            label.style.flexGrow = grow;
            label.style.flexShrink = 1;
            label.style.flexBasis = basis;
            label.style.minWidth = 0;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private void RebuildSourceTable()
        {
            if (m_SourceTableBody == null)
            {
                return;
            }

            m_SourceTableBody.Clear();

            var query = m_SearchField?.value?.Trim() ?? string.Empty;
            var visibleSourceCount = 0;
            foreach (var source in m_SourceSnapshot?.Sources ?? Array.Empty<LubanSourceDescriptor>())
            {
                var sourceMatches = MatchesSearch(query, source.DisplayName, source.SourceId);
                var matchingTables = source.Tables
                    .Where(table => sourceMatches || MatchesSearch(
                        query,
                        table.TableName,
                        table.SheetName,
                        table.TableId))
                    .ToArray();
                if (query.Length > 0 && sourceMatches is false && matchingTables.Length == 0)
                {
                    continue;
                }

                visibleSourceCount++;
                AddSourceSection(source, matchingTables, query.Length > 0);
            }

            if (visibleSourceCount == 0)
            {
                var empty = new Label(query.Length == 0
                    ? "未找到 Excel 配置表，请检查上方的配置表目录。"
                    : "没有匹配的配置表。");
                empty.name = "configuration-source-empty-state";
                empty.style.paddingLeft = 36;
                empty.style.paddingTop = 18;
                empty.style.paddingBottom = 18;
                empty.style.color = Color.gray;
                m_SourceTableBody.Add(empty);
            }
        }

        private void AddSourceSection(
            LubanSourceDescriptor source,
            IReadOnlyList<LubanTableDescriptor> tables,
            bool searchActive)
        {
            var expanded = m_ExpandedSourceIds.Contains(source.SourceId);
            var errorCount = CountErrors(source.SourceId, null);
            var row = CreateHierarchyRow(
                $"source-row-{source.SourceId}",
                source.DisplayName,
                source.SourceId,
                errorCount > 0 ? $"{errorCount} 个错误" : $"{source.Tables.Count} 张表",
                0,
                expanded,
                () => ToggleSource(source.SourceId),
                () => OpenProjectFile(source.SourceId),
                HasProjectFile(source.SourceId),
                true,
                false);
            m_SourceTableBody.Add(row);

            if (expanded is false && searchActive is false)
            {
                return;
            }

            foreach (var table in tables)
            {
                AddTableSection(source, table);
            }
        }

        private void AddTableSection(LubanSourceDescriptor source, LubanTableDescriptor table)
        {
            var expanded = m_ExpandedTableIds.Contains(table.TableId);
            var selected = string.Equals(m_SelectedSourceItem?.StableId, table.TableId, StringComparison.Ordinal);
            var errorCount = CountErrors(source.SourceId, table.TableId);
            var row = CreateHierarchyRow(
                $"table-row-{table.TableId}",
                table.TableName,
                table.SheetName,
                errorCount > 0 ? $"{errorCount} 个错误" : $"{table.Fields.Count} 个字段",
                24,
                expanded,
                () => SelectAndToggleTable(source, table),
                () => OpenProjectFile(source.SourceId),
                HasProjectFile(source.SourceId),
                false,
                selected);
            m_SourceTableBody.Add(row);

            if (expanded)
            {
                m_SourceTableBody.Add(CreateTableDetails(table));
            }
        }

        private VisualElement CreateHierarchyRow(
            string name,
            string displayName,
            string source,
            string status,
            float indent,
            bool expanded,
            Action toggle,
            Action open,
            bool openEnabled,
            bool group,
            bool selected)
        {
            var row = new VisualElement { name = name };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = group ? 30 : 28;
            row.style.paddingLeft = 8 + indent;
            row.style.paddingRight = 8;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = EditorGUIUtility.isProSkin
                ? new Color(0.25f, 0.26f, 0.28f)
                : new Color(0.8f, 0.81f, 0.83f);
            row.style.backgroundColor = selected
                ? (EditorGUIUtility.isProSkin ? new Color(0.2f, 0.32f, 0.31f) : new Color(0.84f, 0.94f, 0.92f))
                : group
                    ? (EditorGUIUtility.isProSkin ? new Color(0.2f, 0.21f, 0.23f) : new Color(0.86f, 0.87f, 0.89f))
                    : Color.clear;

            var foldout = new Button(toggle) { text = expanded ? "▼" : "▶" };
            foldout.name = "row-foldout";
            foldout.style.width = 24;
            foldout.style.minWidth = 24;
            foldout.style.height = 22;
            foldout.style.marginRight = 4;
            row.Add(foldout);

            var displayNameLabel = CreateRowLabel(displayName, 4, 232);
            displayNameLabel.name = "row-name";
            displayNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            displayNameLabel.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    toggle();
                    evt.StopPropagation();
                }
            });
            row.Add(displayNameLabel);

            var sourceLabel = CreateRowLabel(source, 3, 220);
            sourceLabel.name = "row-source";
            sourceLabel.tooltip = source;
            sourceLabel.style.color = EditorGUIUtility.isProSkin
                ? new Color(0.68f, 0.7f, 0.73f)
                : new Color(0.28f, 0.3f, 0.33f);
            row.Add(sourceLabel);

            var statusLabel = CreateRowLabel(status, 1, 110);
            statusLabel.name = "row-status";
            row.Add(statusLabel);

            var actions = new VisualElement();
            actions.style.width = 72;
            actions.style.minWidth = 72;
            actions.style.alignItems = Align.FlexEnd;
            if (open != null)
            {
                var openButton = new Button(open) { text = "打开" };
                openButton.SetEnabled(openEnabled);
                actions.Add(openButton);
            }

            row.Add(actions);
            return row;
        }

        private static Label CreateRowLabel(string text, float grow, float basis)
        {
            var label = new Label(text);
            label.style.flexGrow = grow;
            label.style.flexShrink = 1;
            label.style.flexBasis = basis;
            label.style.minWidth = 0;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow = Overflow.Hidden;
            return label;
        }

        private VisualElement CreateTableDetails(LubanTableDescriptor table)
        {
            var details = CreateInlineDetails($"table-details-{table.TableId}", 56);
            var rowText = m_SourceCatalog.TryReadTable(table.TableId, out var data, out var diagnostic)
                ? data.Rows.Count.ToString()
                : $"读取失败：{diagnostic?.Message}";
            details.Add(CreateDetailLabel(
                $"表标识：{table.TableId}\nSheet：{table.SheetName}\n数据行：{rowText}"));
            details.Add(CreateDetailLabel(
                "字段：\n" + string.Join(
                    "\n",
                    table.Fields.Select(field =>
                        $"  {field.Name}: {field.Type} · column {field.SourceColumn} · {field.Comment}"))));
            details.Add(CreateDetailLabel("诊断：\n" + BuildDiagnostics(table.SourceId, table.TableId)));
            return details;
        }

        private static VisualElement CreateInlineDetails(string name, float indent)
        {
            var details = new VisualElement { name = name };
            details.style.paddingLeft = indent;
            details.style.paddingRight = 16;
            details.style.paddingTop = 10;
            details.style.paddingBottom = 12;
            details.style.borderBottomWidth = 1;
            details.style.borderBottomColor = EditorGUIUtility.isProSkin
                ? new Color(0.25f, 0.26f, 0.28f)
                : new Color(0.8f, 0.81f, 0.83f);
            details.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.17f, 0.18f, 0.2f)
                : new Color(0.92f, 0.93f, 0.95f);
            return details;
        }

        private static Label CreateDetailLabel(string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 8;
            label.style.unityTextAlign = TextAnchor.UpperLeft;
            return label;
        }

        private void ToggleSource(string sourceId)
        {
            if (m_ExpandedSourceIds.Remove(sourceId) is false)
            {
                m_ExpandedSourceIds.Add(sourceId);
            }

            RebuildSourceTable();
        }

        private void SelectAndToggleTable(LubanSourceDescriptor source, LubanTableDescriptor table)
        {
            m_SelectedSourceItem = new SourceListItem(source, table);
            if (m_ExpandedTableIds.Remove(table.TableId) is false)
            {
                m_ExpandedTableIds.Add(table.TableId);
            }

            RefreshCommandPreview();
            RefreshActionState();
            RebuildSourceTable();
        }

        private int CountErrors(string sourceId, string tableId)
        {
            return m_SourceSnapshot?.Diagnostics.Count(diagnostic =>
                diagnostic.Severity == LubanDiagnosticSeverity.Error &&
                (string.IsNullOrWhiteSpace(diagnostic.SourceId) ||
                 string.Equals(diagnostic.SourceId, sourceId, StringComparison.Ordinal)) &&
                (string.IsNullOrWhiteSpace(tableId) ||
                 string.IsNullOrWhiteSpace(diagnostic.TableId) ||
                 string.Equals(diagnostic.TableId, tableId, StringComparison.Ordinal))) ?? 0;
        }

        private static bool MatchesSearch(string query, params string[] values)
        {
            return query.Length == 0 || values.Any(value =>
                string.IsNullOrWhiteSpace(value) is false &&
                value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void RefreshSourceCatalog()
        {
            m_GlobalConfig = EditorGlobalConfig.LoadOrCreate();
            m_UserConfig = EditorUserConfig.LoadOrCreate();
            try
            {
                if (m_GlobalConfig.TryValidate(out var error) is false)
                {
                    throw new InvalidDataException(error);
                }

                LoadConfiguredConf();
                m_SourceSnapshot = m_SourceCatalog.Refresh(m_GlobalConfig.Luban);
            }
            catch (Exception exception)
            {
                m_SourceSnapshot = new LubanSourceSnapshot(
                    0,
                    Array.Empty<LubanSourceDescriptor>(),
                    new[]
                    {
                        new LubanDiagnostic(
                            LubanDiagnosticSeverity.Error,
                            $"刷新配置表目录失败：{exception.Message}")
                    });
            }

            RefreshSourceList();
            RefreshCommandPreview();
            RefreshActionState();
        }

        private void LoadConfiguredConf()
        {
            var config = m_GlobalConfig.Luban;
            var confPath = LubanCommandRunner.GetAbsoluteProjectPath(
                $"{config.TableDirectory.TrimEnd('/', '\\')}/luban.conf");
            if (IOFile.Exists(confPath) is false)
            {
                m_ConfModel = null;
                return;
            }

            m_ConfModel = LubanConfModel.Load(confPath);
            if (m_ConfModel.EnsureTargetTopModule("client", config.CodeNamespace))
            {
                m_ConfModel.Save();
            }
        }

        private void RefreshSourceList()
        {
            m_SourceItems.Clear();
            if (m_SourceSnapshot != null)
            {
                foreach (var source in m_SourceSnapshot.Sources)
                {
                    m_SourceItems.Add(new SourceListItem(source));
                    if (m_KnownSourceIds.Add(source.SourceId))
                    {
                        m_ExpandedSourceIds.Add(source.SourceId);
                    }

                    foreach (var table in source.Tables)
                    {
                        m_SourceItems.Add(new SourceListItem(source, table));
                    }
                }
            }

            if (m_SelectedSourceItem == null ||
                m_SourceItems.Any(item => string.Equals(item.StableId, m_SelectedSourceItem.StableId, StringComparison.Ordinal)) is false)
            {
                m_SelectedSourceItem = m_SourceItems.FirstOrDefault(item => item.IsTable) ?? m_SourceItems.FirstOrDefault();
            }

            var sourceCount = m_SourceSnapshot?.Sources.Count ?? 0;
            var tableCount = m_SourceSnapshot?.Tables.Count() ?? 0;
            var errorCount = m_SourceSnapshot?.Diagnostics.Count(x => x.Severity == LubanDiagnosticSeverity.Error) ?? 0;
            if (m_SourceSummaryLabel != null)
            {
                m_SourceSummaryLabel.text = $"{sourceCount} 个 Excel · {tableCount} 张表 · {errorCount} 个错误";
            }

            RebuildSourceTable();
        }

        private string BuildDiagnostics(string sourceId, string tableId)
        {
            var lines = new List<string>();
            if (m_SourceSnapshot != null)
            {
                foreach (var diagnostic in m_SourceSnapshot.Diagnostics)
                {
                    var sourceMatches = string.IsNullOrWhiteSpace(diagnostic.SourceId) ||
                                        string.Equals(diagnostic.SourceId, sourceId, StringComparison.Ordinal);
                    var tableMatches = string.IsNullOrWhiteSpace(tableId) ||
                                       string.IsNullOrWhiteSpace(diagnostic.TableId) ||
                                       string.Equals(diagnostic.TableId, tableId, StringComparison.Ordinal);
                    if (sourceMatches && tableMatches)
                    {
                        lines.Add($"{diagnostic.Severity}: {diagnostic.Message}");
                    }
                }
            }

            if (lines.Count == 0)
            {
                lines.Add("Ready.");
            }

            return string.Join("\n", lines);
        }

        private string BuildSnapshotDiagnostics()
        {
            if (m_SourceSnapshot == null || m_SourceSnapshot.Diagnostics.Count == 0)
            {
                return "Ready.";
            }

            return string.Join(
                "\n",
                m_SourceSnapshot.Diagnostics.Select(diagnostic => $"{diagnostic.Severity}: {diagnostic.Message}"));
        }

        private LubanWorkspaceProfile CreateFixedWorkspaceProfile()
        {
            var config = m_GlobalConfig?.Luban ?? EditorGlobalConfig.LoadOrCreate().Luban;
            var workspaceRoot = config.TableDirectory;
            var workspace = new LubanWorkspaceProfile
            {
                Name = "Global Config",
                WorkspaceRoot = workspaceRoot,
                ConfPath = $"{workspaceRoot.TrimEnd('/', '\\')}/luban.conf",
                SchemaDirectory = $"{workspaceRoot.TrimEnd('/', '\\')}/Defines",
                DataDirectory = $"{workspaceRoot.TrimEnd('/', '\\')}/Datas",
                DefaultTarget = "client"
            };

            if (m_ConfModel != null)
            {
                workspace.SchemaDirectory = MakeProjectRelativeChildPath(
                    m_ConfModel.WorkspaceRoot,
                    m_ConfModel.SchemaFiles.FirstOrDefault() ?? "Defines");
                workspace.DataDirectory = MakeProjectRelativeChildPath(
                    m_ConfModel.WorkspaceRoot,
                    m_ConfModel.DataDirectory);
            }

            workspace.EnsureDefaults();
            return workspace;
        }

        private LubanGenerationProfile CreateFixedGenerationProfile()
        {
            var config = m_GlobalConfig?.Luban ?? EditorGlobalConfig.LoadOrCreate().Luban;
            var profile = new LubanGenerationProfile
            {
                Name = "Client Json",
                Target = "client",
                CodeTarget = "cs-simple-json",
                DataTarget = "json",
                IncludeTag = string.Empty,
                ExcludeTag = string.Empty,
                Variant = string.Empty,
                Pipeline = string.Empty,
                Xargs = string.Empty,
                OutputCodeDirectory = config.GeneratedCodeDirectory,
                OutputDataDirectory = config.GeneratedDataDirectory,
                UseCustomTemplateDir = false,
                CustomTemplateDirectory = string.Empty,
                ValidationFailAsError = true
            };
            profile.EnsureDefaults();
            ConfigureSelectedTableScope(profile);
            return profile;
        }

        private void ConfigureSelectedTableScope(LubanGenerationProfile profile)
        {
            if (m_GenerateSelectedTableToggle?.value != true || m_SelectedSourceItem?.IsTable != true)
            {
                profile.TableSelection.Scope = LubanTableScope.AllTables;
                profile.TableSelection.SelectedTableNames.Clear();
                return;
            }

            profile.TableSelection.Scope = LubanTableScope.SelectedTables;
            profile.TableSelection.SelectedTableNames.Clear();
            profile.TableSelection.SetSelected(m_SelectedSourceItem.Table.TableName, true);
        }

        private bool TryGetSelectedCatalogTable(out LubanTableDescriptor table)
        {
            table = m_SelectedSourceItem?.Table;
            return table != null;
        }

        private Func<string, LocalizationPackExportResult> CreateLocalizationPackExport()
        {
            var configured = m_GlobalConfig?.Localization;
            if (configured == null || string.IsNullOrWhiteSpace(configured.TableId))
            {
                return null;
            }

            var frozen = new LocalizationProjectConfig
            {
                TableId = configured.TableId,
                KeyField = configured.KeyField,
                PreviewLocale = configured.PreviewLocale
            };
            frozen.EnsureDefaults();
            foreach (var mapping in configured.LocaleFields)
            {
                frozen.LocaleFields.Add(new LocalizationLocaleField
                {
                    Locale = mapping.Locale,
                    FieldName = mapping.FieldName
                });
            }

            var snapshot = m_SourceCatalog.Refresh(m_GlobalConfig.Luban);
            var contract = LocalizationTableContractValidator.Validate(snapshot, m_SourceCatalog, frozen);
            if (contract.IsValid is false)
            {
                throw new InvalidDataException(string.Join(
                    Environment.NewLine,
                    contract.Diagnostics.Select(diagnostic => diagnostic.Message)));
            }

            var table = contract.Data;
            return stagingDataDirectory =>
                LocalizationPackExporter.Shared.Export(table, frozen, stagingDataDirectory);
        }

        private static void OpenProjectFile(string path)
        {
            if (HasProjectFile(path) is false)
            {
                return;
            }

            var absolutePath = LubanCommandRunner.GetAbsoluteProjectPath(path);
            var assetPath = LubanCommandRunner.ToProjectRelativePath(absolutePath);
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                AssetDatabase.OpenAsset(asset);
                return;
            }

            EditorUtility.RevealInFinder(absolutePath);
        }

        private static bool HasProjectFile(string path)
        {
            return string.IsNullOrWhiteSpace(path) is false
                && IOFile.Exists(LubanCommandRunner.GetAbsoluteProjectPath(path));
        }

        private static string MakeProjectRelativeChildPath(string workspaceRoot, string childPath)
        {
            if (string.IsNullOrWhiteSpace(childPath))
            {
                return LubanCommandRunner.ToProjectRelativePath(workspaceRoot);
            }

            var absolutePath = IOPath.IsPathRooted(childPath)
                ? childPath
                : IOPath.Combine(workspaceRoot, childPath);
            return LubanCommandRunner.ToProjectRelativePath(absolutePath);
        }

    }
}
