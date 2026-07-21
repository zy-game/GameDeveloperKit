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

        private Button m_HeaderRefreshButton;
        private Button m_HeaderCheckButton;
        private Button m_HeaderGenerateButton;
        private Button m_HeaderCancelButton;

        private Label m_StatusLabel;
        private Label m_VersionLabel;
        private Label m_ErrorLabel;
        private TextField m_CommandField;
        private TextField m_LogField;
        private ListView m_SourceListView;
        private Label m_SourceSummaryLabel;
        private TextField m_SourceDetailField;
        private TextField m_SourceFieldsField;
        private TextField m_SourceDiagnosticsField;
        private Button m_OpenSourceFileButton;
        private Toggle m_GenerateSelectedTableToggle;

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

            var body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.minWidth = 0;
            body.style.minHeight = 0;
            body.style.flexDirection = FlexDirection.Row;
            body.style.paddingLeft = 12;
            body.style.paddingRight = 12;
            body.style.paddingTop = 12;
            body.style.paddingBottom = 8;
            root.Add(body);

            body.Add(CreateSourceListPane());
            body.Add(CreateSourceDetailPane());

            var status = CreateStatusPanel();
            status.style.marginLeft = 12;
            status.style.marginRight = 12;
            status.style.marginBottom = 12;
            root.Add(status);

            RefreshActionState();
        }

        private VisualElement CreateHeader()
        {
            var titleBar = new VisualElement();
            titleBar.style.flexDirection = FlexDirection.Row;
            titleBar.style.alignItems = Align.Center;
            titleBar.style.minHeight = 52;
            titleBar.style.paddingLeft = 14;
            titleBar.style.paddingRight = 14;
            titleBar.style.borderBottomWidth = 1;
            titleBar.style.borderBottomColor = EditorGUIUtility.isProSkin
                ? new Color(0.28f, 0.3f, 0.33f)
                : new Color(0.82f, 0.86f, 0.9f);
            titleBar.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.18f, 0.19f, 0.21f)
                : Color.white;

            var title = new Label("Luban 配置表工作台");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 16;
            title.style.flexGrow = 1;
            titleBar.Add(title);

            AddHeaderButton(titleBar, new Button(OpenConfigurationSettings) { text = "配置" });
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
            button.style.marginLeft = 6;
            button.style.minWidth = 64;
            parent.Add(button);
        }

        private VisualElement CreateSourceListPane()
        {
            var pane = CreatePanel();
            pane.style.width = 360;
            pane.style.minWidth = 300;
            pane.style.maxWidth = 420;
            pane.style.flexShrink = 0;
            pane.style.marginRight = 12;
            pane.style.marginBottom = 0;
            pane.style.minHeight = 0;
            pane.style.flexGrow = 0;

            pane.Add(CreateSectionHeader("Excel 数据源"));
            m_SourceSummaryLabel = new Label();
            m_SourceSummaryLabel.style.whiteSpace = WhiteSpace.Normal;
            m_SourceSummaryLabel.style.marginBottom = 8;
            pane.Add(m_SourceSummaryLabel);

            m_SourceListView = new ListView();
            m_SourceListView.itemsSource = m_SourceItems;
            m_SourceListView.selectionType = SelectionType.Single;
            m_SourceListView.fixedItemHeight = 56;
            m_SourceListView.makeItem = MakeSourceRow;
            m_SourceListView.bindItem = BindSourceRow;
            m_SourceListView.selectionChanged += OnSourceSelectionChanged;
            m_SourceListView.style.flexGrow = 1;
            m_SourceListView.style.minHeight = 0;
            m_SourceListView.style.borderLeftWidth = 1;
            m_SourceListView.style.borderRightWidth = 1;
            m_SourceListView.style.borderTopWidth = 1;
            m_SourceListView.style.borderBottomWidth = 1;
            m_SourceListView.style.borderLeftColor = new Color(0.28f, 0.28f, 0.28f);
            m_SourceListView.style.borderRightColor = new Color(0.28f, 0.28f, 0.28f);
            m_SourceListView.style.borderTopColor = new Color(0.28f, 0.28f, 0.28f);
            m_SourceListView.style.borderBottomColor = new Color(0.28f, 0.28f, 0.28f);
            pane.Add(m_SourceListView);

            m_GenerateSelectedTableToggle = CreateToggleField("仅生成选中表");
            m_GenerateSelectedTableToggle.tooltip = "开启后只对当前选中的表传递 Luban -o 参数。";
            m_GenerateSelectedTableToggle.RegisterValueChangedCallback(_ =>
            {
                RefreshCommandPreview();
                RefreshActionState();
                RefreshSourceDetail();
            });
            pane.Add(m_GenerateSelectedTableToggle);
            return pane;
        }

        private VisualElement CreateSourceDetailPane()
        {
            var pane = CreatePanel();
            pane.style.flexGrow = 1;
            pane.style.minWidth = 0;
            pane.style.marginBottom = 0;
            pane.style.minHeight = 0;

            pane.Add(CreateSectionHeader("表详情"));

            pane.Add(CreateFieldHeader("Definition"));
            m_SourceDetailField = CreateTextField(string.Empty);
            m_SourceDetailField.isReadOnly = true;
            m_SourceDetailField.multiline = true;
            m_SourceDetailField.style.height = 112;
            m_SourceDetailField.style.marginBottom = 8;
            pane.Add(m_SourceDetailField);

            pane.Add(CreateFieldHeader("Fields"));
            m_SourceFieldsField = CreateTextField(string.Empty);
            m_SourceFieldsField.isReadOnly = true;
            m_SourceFieldsField.multiline = true;
            m_SourceFieldsField.style.height = 120;
            m_SourceFieldsField.style.marginBottom = 8;
            pane.Add(m_SourceFieldsField);

            pane.Add(CreateFieldHeader("Diagnostics"));
            m_SourceDiagnosticsField = CreateTextField(string.Empty);
            m_SourceDiagnosticsField.isReadOnly = true;
            m_SourceDiagnosticsField.multiline = true;
            m_SourceDiagnosticsField.style.height = 96;
            m_SourceDiagnosticsField.style.marginBottom = 8;
            pane.Add(m_SourceDiagnosticsField);

            var actions = CreateButtonRow();
            pane.Add(actions);
            m_OpenSourceFileButton = new Button(OpenSelectedSourceFile) { text = "打开源文件" };
            AddRowButton(actions, m_OpenSourceFileButton);
            return pane;
        }

        private VisualElement MakeSourceRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.28f, 0.28f, 0.28f);

            var indicator = new VisualElement { name = "indicator" };
            indicator.style.width = 3;
            indicator.style.height = 38;
            indicator.style.marginRight = 8;
            indicator.style.borderTopLeftRadius = 2;
            indicator.style.borderTopRightRadius = 2;
            indicator.style.borderBottomLeftRadius = 2;
            indicator.style.borderBottomRightRadius = 2;
            row.Add(indicator);

            var texts = new VisualElement();
            texts.style.flexGrow = 1;
            texts.style.minWidth = 0;
            row.Add(texts);

            var name = new Label { name = "name" };
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.whiteSpace = WhiteSpace.NoWrap;
            texts.Add(name);

            var meta = new Label { name = "meta" };
            meta.style.fontSize = 11;
            meta.style.color = Color.gray;
            meta.style.whiteSpace = WhiteSpace.NoWrap;
            meta.style.marginTop = 3;
            texts.Add(meta);
            return row;
        }

        private void BindSourceRow(VisualElement element, int index)
        {
            if (index < 0 || index >= m_SourceItems.Count)
            {
                return;
            }

            var item = m_SourceItems[index];
            var selected = string.Equals(item.StableId, m_SelectedSourceItem?.StableId, StringComparison.Ordinal);
            element.style.backgroundColor = selected
                ? (EditorGUIUtility.isProSkin ? new Color(0.22f, 0.24f, 0.27f) : new Color(0.9f, 0.96f, 0.95f))
                : Color.clear;

            var indicator = element.Q<VisualElement>("indicator");
            indicator.style.backgroundColor = selected
                ? (EditorGUIUtility.isProSkin ? new Color(0.22f, 0.74f, 0.66f) : new Color(0.05f, 0.56f, 0.5f))
                : Color.clear;

            var name = element.Q<Label>("name");
            var meta = element.Q<Label>("meta");
            if (item.IsTable)
            {
                name.text = "  " + item.Table.TableName;
                meta.text = $"{item.Table.SheetName} · {item.Table.Fields.Count} fields";
            }
            else
            {
                name.text = item.Source.DisplayName;
                meta.text = $"{item.Source.Tables.Count} tables · {item.Source.SourceId}";
            }
        }

        private void OnSourceSelectionChanged(IEnumerable<object> selection)
        {
            var item = selection.OfType<SourceListItem>().FirstOrDefault();
            if (item == null)
            {
                return;
            }

            m_SelectedSourceItem = item;
            RefreshSourceDetail();
            RefreshCommandPreview();
            RefreshActionState();
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
            RefreshSourceDetail();
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

            m_SourceListView?.Rebuild();
            var selectedIndex = m_SourceItems.FindIndex(item => string.Equals(item.StableId, m_SelectedSourceItem?.StableId, StringComparison.Ordinal));
            if (selectedIndex >= 0)
            {
                m_SourceListView?.SetSelectionWithoutNotify(new[] { selectedIndex });
            }

            var sourceCount = m_SourceSnapshot?.Sources.Count ?? 0;
            var tableCount = m_SourceSnapshot?.Tables.Count() ?? 0;
            var errorCount = m_SourceSnapshot?.Diagnostics.Count(x => x.Severity == LubanDiagnosticSeverity.Error) ?? 0;
            m_SourceSummaryLabel.text =
                $"目录：{m_GlobalConfig?.Luban.TableDirectory ?? string.Empty}\n" +
                $"Source {sourceCount} · Table {tableCount} · Error {errorCount}";
        }

        private void RefreshSourceDetail()
        {
            var item = m_SelectedSourceItem;
            m_OpenSourceFileButton?.SetEnabled(item?.Source != null && HasProjectFile(item.Source.SourceId));
            if (m_SourceDetailField == null || m_SourceFieldsField == null || m_SourceDiagnosticsField == null)
            {
                return;
            }

            if (item == null)
            {
                m_SourceDetailField.SetValueWithoutNotify(string.Empty);
                m_SourceFieldsField.SetValueWithoutNotify(string.Empty);
                m_SourceDiagnosticsField.SetValueWithoutNotify(BuildSnapshotDiagnostics());
                return;
            }

            if (item.IsTable)
            {
                RefreshTableDescriptorDetail(item.Table);
                return;
            }

            m_SourceDetailField.SetValueWithoutNotify(
                $"sourceId={item.Source.SourceId}\n" +
                $"displayName={item.Source.DisplayName}\n" +
                $"lastWriteUtcTicks={item.Source.LastWriteUtcTicks}\n" +
                $"tables={item.Source.Tables.Count}");
            m_SourceFieldsField.SetValueWithoutNotify(string.Join(
                "\n",
                item.Source.Tables.Select(table => $"{table.TableName} · {table.SheetName} · {table.Fields.Count} fields")));
            m_SourceDiagnosticsField.SetValueWithoutNotify(BuildDiagnostics(item.Source.SourceId, null));
        }

        private void RefreshTableDescriptorDetail(LubanTableDescriptor table)
        {
            var rowText = m_SourceCatalog.TryReadTable(table.TableId, out var data, out var diagnostic)
                ? data.Rows.Count.ToString()
                : $"读取失败：{diagnostic?.Message}";
            m_SourceDetailField.SetValueWithoutNotify(
                $"tableId={table.TableId}\n" +
                $"sourceId={table.SourceId}\n" +
                $"sheet={table.SheetName}\n" +
                $"tableName={table.TableName}\n" +
                $"rows={rowText}\n" +
                $"generateSelectedOnly={m_GenerateSelectedTableToggle?.value == true}");
            m_SourceFieldsField.SetValueWithoutNotify(string.Join(
                "\n",
                table.Fields.Select(field => $"{field.Name}: {field.Type} · column {field.SourceColumn} · {field.Comment}")));
            m_SourceDiagnosticsField.SetValueWithoutNotify(BuildDiagnostics(table.SourceId, table.TableId));
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

        private void OpenConfigurationSettings()
        {
            SettingsService.OpenProjectSettings("Project/GameDeveloperKit/Configuration");
        }

        private void OpenSelectedSourceFile()
        {
            OpenProjectFile(m_SelectedSourceItem?.Source?.SourceId);
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
