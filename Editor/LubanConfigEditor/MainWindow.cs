using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.LocalizationEditor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace GameDeveloperKit.LubanConfigEditor.UI
{
    public sealed partial class MainWindow : EditorWindow
    {
        private const string WindowTitle = "配置表工具";
        private const string StylePath = "Editor/LubanConfigEditor/MainWindow.uss";

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

        private Label m_TitleLabel;
        private Label m_StatusLabel;
        private Label m_VersionLabel;
        private Label m_ErrorLabel;
        private TextField m_CommandField;
        private TextField m_LogField;
        private VisualElement m_SourceTableBody;
        private Label m_SourceSummaryLabel;
        private ToolbarSearchField m_SearchField;
        private Toggle m_GenerateSelectedTableToggle;
        private Button m_SourceTablesToggle;
        private Button m_GlobalSettingsToggle;
        private Button m_CloudSettingsToggle;
        private Button m_LocalizationToggle;
        private LocalizationAssetWorkbench m_LocalizationWorkbench;
        private VisualElement m_ContentHost;
        private Page m_Page;

        private CancellationTokenSource m_RunCancellation;

        [MenuItem("GameDeveloperKit/" + WindowTitle)]
        public static void Open()
        {
            var window = GetWindow<MainWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(920, 560);
            window.Show();
        }

        public static void OpenCloudConfiguration()
        {
            Open();
            var window = GetWindow<MainWindow>();
            window.SetPage(Page.Cloud);
            window.Focus();
        }

        public static void OpenGlobalConfiguration()
        {
            Open();
            var window = GetWindow<MainWindow>();
            window.SetPage(Page.GlobalSettings);
            window.Focus();
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
            var styleSheet = GameDeveloperKitEditorPaths.LoadPackageAsset<StyleSheet>(StylePath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            var root = new VisualElement();
            root.AddToClassList("luban-config-editor");
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
            var header = new VisualElement { name = "configuration-header" };
            header.AddToClassList("luban-config-editor__header");

            var primaryToolbar = new Toolbar { name = "configuration-toolbar" };
            primaryToolbar.AddToClassList("luban-config-editor__toolbar");
            var brand = new Label(WindowTitle) { name = "configuration-window-title" };
            brand.AddToClassList("luban-config-editor__brand");
            primaryToolbar.Add(brand);

            var navigation = new VisualElement { name = "configuration-page-navigation" };
            navigation.AddToClassList("luban-config-editor__page-navigation");
            m_SourceTablesToggle = CreatePageButton("配置表", () => SetPage(Page.SourceTables));
            m_SourceTablesToggle.name = "source-tables-toggle";
            navigation.Add(m_SourceTablesToggle);
            m_GlobalSettingsToggle = CreatePageButton("全局设置", ToggleGlobalSettingsMode);
            m_GlobalSettingsToggle.name = "global-settings-toggle";
            navigation.Add(m_GlobalSettingsToggle);
            m_CloudSettingsToggle = CreatePageButton("云配置", ToggleCloudSettingsMode);
            m_CloudSettingsToggle.name = "cloud-settings-toggle";
            navigation.Add(m_CloudSettingsToggle);
            m_LocalizationToggle = CreatePageButton("本地化", ToggleLocalizationMode);
            m_LocalizationToggle.name = "localization-toggle";
            navigation.Add(m_LocalizationToggle);
            primaryToolbar.Add(navigation);

            var spacer = new VisualElement();
            spacer.AddToClassList("luban-config-editor__toolbar-spacer");
            primaryToolbar.Add(spacer);

            m_HeaderRefreshButton = CreateToolbarActionButton(
                RefreshCurrentPage,
                "刷新",
                "刷新当前页面",
                "Refresh");
            primaryToolbar.Add(m_HeaderRefreshButton);
            m_HeaderCheckButton = CreateToolbarActionButton(
                RunCheck,
                "检查",
                "检查配置表",
                "TestPassed");
            primaryToolbar.Add(m_HeaderCheckButton);
            m_HeaderGenerateButton = CreateToolbarActionButton(
                RunGenerate,
                "生成",
                "生成配置表代码与数据",
                "BuildSettings.Editor");
            m_HeaderGenerateButton.AddToClassList("luban-config-editor__toolbar-action--primary");
            primaryToolbar.Add(m_HeaderGenerateButton);
            m_HeaderCancelButton = CreateToolbarActionButton(
                CancelCurrentRun,
                "取消",
                "取消当前任务",
                "winbtn_win_close");
            primaryToolbar.Add(m_HeaderCancelButton);
            header.Add(primaryToolbar);

            var contextToolbar = new Toolbar { name = "configuration-context-toolbar" };
            contextToolbar.AddToClassList("luban-config-editor__context-toolbar");
            m_TitleLabel = new Label("配置表");
            m_TitleLabel.AddToClassList("luban-config-editor__page-title");
            contextToolbar.Add(m_TitleLabel);

            m_SourceSummaryLabel = new Label();
            m_SourceSummaryLabel.AddToClassList("luban-config-editor__page-summary");
            contextToolbar.Add(m_SourceSummaryLabel);

            m_GenerateSelectedTableToggle = new ToolbarToggle { text = "仅生成当前表" };
            m_GenerateSelectedTableToggle.tooltip = "开启后只生成当前选中的配置表。";
            m_GenerateSelectedTableToggle.AddToClassList("luban-config-editor__scope-toggle");
            m_GenerateSelectedTableToggle.RegisterValueChangedCallback(_ =>
            {
                RefreshCommandPreview();
                RefreshActionState();
                RebuildSourceTable();
            });
            contextToolbar.Add(m_GenerateSelectedTableToggle);

            m_SearchField = new ToolbarSearchField();
            m_SearchField.name = "configuration-search-field";
            m_SearchField.tooltip = "搜索配置表、Sheet、TableId 或本地化文本";
            m_SearchField.AddToClassList("luban-config-editor__search");
            m_SearchField.RegisterValueChangedCallback(evt =>
            {
                if (m_Page == Page.Localization)
                {
                    m_LocalizationWorkbench?.SetSearchQuery(evt.newValue);
                }
                else if (m_Page == Page.SourceTables)
                {
                    RebuildSourceTable();
                }
            });
            contextToolbar.Add(m_SearchField);
            header.Add(contextToolbar);

            RefreshPageToggleStyles();
            return header;
        }

        private static Button CreatePageButton(string text, Action clicked)
        {
            var button = new ToolbarButton(clicked) { text = text };
            button.AddToClassList("luban-config-editor__page-button");
            return button;
        }

        private static Button CreateToolbarActionButton(
            Action clicked,
            string text,
            string tooltip,
            string iconName)
        {
            var button = new ToolbarButton(clicked) { tooltip = tooltip };
            button.AddToClassList("luban-config-editor__toolbar-action");
            var icon = new Image
            {
                image = EditorGUIUtility.IconContent(iconName).image,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            icon.AddToClassList("luban-config-editor__toolbar-action-icon");
            button.Add(icon);
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("luban-config-editor__toolbar-action-label");
            button.Add(label);
            return button;
        }

        private void SetHeaderSummary(string summary)
        {
            if (m_SourceSummaryLabel != null)
            {
                m_SourceSummaryLabel.text = summary ?? string.Empty;
            }
        }

        private void SetHeaderTitle(string title)
        {
            if (m_TitleLabel != null)
            {
                m_TitleLabel.text = title ?? string.Empty;
            }
        }

        private void RefreshSourceSummary()
        {
            var sourceCount = m_SourceSnapshot?.Sources.Count ?? 0;
            var tableCount = m_SourceSnapshot?.Tables.Count() ?? 0;
            var errorCount = m_SourceSnapshot?.Diagnostics.Count(x => x.Severity == LubanDiagnosticSeverity.Error) ?? 0;
            SetHeaderSummary($"{sourceCount} 个 Excel · {tableCount} 张表 · {errorCount} 个错误");
        }

        private VisualElement CreateSourceTable()
        {
            var table = new VisualElement();
            table.name = "configuration-source-table";
            table.AddToClassList("luban-config-editor__table");

            var header = new VisualElement();
            header.name = "configuration-source-table-header";
            header.AddToClassList("luban-config-editor__table-header");
            header.Add(CreateColumnLabel("名称", 4, 260));
            header.Add(CreateColumnLabel("来源", 3, 220));
            header.Add(CreateColumnLabel("状态", 1, 110));
            header.Add(CreateColumnLabel(string.Empty, 0, 72));
            table.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.name = "configuration-source-table-scroll";
            scroll.AddToClassList("luban-config-editor__table-scroll");
            m_SourceTableBody = new VisualElement { name = "configuration-source-table-body" };
            m_SourceTableBody.AddToClassList("luban-config-editor__table-body");
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
            label.AddToClassList("luban-config-editor__table-column");
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
                empty.AddToClassList("luban-config-editor__empty-state");
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
            row.AddToClassList("luban-config-editor__table-row");
            row.EnableInClassList("luban-config-editor__table-row--group", group);
            row.EnableInClassList("luban-config-editor__table-row--selected", selected);
            row.style.paddingLeft = 8 + indent;

            var foldout = new Foldout { text = string.Empty };
            foldout.name = "row-foldout";
            foldout.tooltip = expanded ? "收起" : "展开";
            foldout.SetValueWithoutNotify(expanded);
            foldout.AddToClassList("luban-config-editor__row-foldout");
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != expanded)
                {
                    toggle();
                }

                evt.StopPropagation();
            });
            row.Add(foldout);

            var displayNameLabel = CreateRowLabel(displayName, 4, 232);
            displayNameLabel.name = "row-name";
            displayNameLabel.AddToClassList("luban-config-editor__row-name");
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
            sourceLabel.AddToClassList("luban-config-editor__row-source");
            row.Add(sourceLabel);

            var statusLabel = CreateRowLabel(status, 1, 110);
            statusLabel.name = "row-status";
            statusLabel.AddToClassList("luban-config-editor__row-status");
            statusLabel.EnableInClassList(
                "luban-config-editor__row-status--error",
                status.IndexOf("错误", StringComparison.Ordinal) >= 0);
            row.Add(statusLabel);

            var actions = new VisualElement();
            actions.AddToClassList("luban-config-editor__row-actions");
            if (open != null)
            {
                var openButton = new Button(open) { tooltip = "在项目中打开" };
                openButton.name = "row-open";
                openButton.AddToClassList("luban-config-editor__row-open");
                var openIcon = new Image
                {
                    image = EditorGUIUtility.IconContent("FolderOpened Icon").image,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                };
                openIcon.AddToClassList("luban-config-editor__row-open-icon");
                openButton.Add(openIcon);
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
            label.style.textOverflow = TextOverflow.Ellipsis;
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
            details.AddToClassList("luban-config-editor__table-details");
            details.style.paddingLeft = indent;
            return details;
        }

        private static Label CreateDetailLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("luban-config-editor__detail-label");
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

            if (m_Page == Page.SourceTables)
            {
                RefreshSourceSummary();
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
