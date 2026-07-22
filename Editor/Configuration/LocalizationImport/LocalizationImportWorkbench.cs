using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.Localization;
using GameDeveloperKit.LubanConfigEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.LocalizationEditor
{
    internal sealed class LocalizationImportWorkbench : VisualElement
    {
        private const string EmptyChoice = "(不导入)";

        private readonly ILocalizationImportService m_Service;
        private readonly ILocalizationAuthoringService m_Authoring;
        private readonly Action<string> m_ErrorChanged;
        private readonly Action m_RefreshParent;
        private readonly Dictionary<string, string> m_SelectedLanguageFields =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, LubanTableDescriptor> m_TablesByDisplayName =
            new Dictionary<string, LubanTableDescriptor>(StringComparer.Ordinal);

        private LubanSourceSnapshot m_SourceSnapshot;
        private LubanTableDescriptor m_SelectedTable;
        private LocalizationImportPlan m_Plan;
        private DropdownField m_TableField;
        private DropdownField m_KeyField;
        private VisualElement m_MappingRows;
        private VisualElement m_Preview;
        private VisualElement m_ConflictActions;
        private Label m_Status;
        private Button m_PreviewButton;
        private Button m_ApplyButton;
        private Button m_UseAssetButton;
        private Button m_UseSourceButton;
        private Button m_LanguageFieldMenu;

        public LocalizationImportWorkbench(
            ILocalizationAuthoringService authoring = null,
            ILocalizationImportService service = null,
            Action<string> errorChanged = null,
            Action refreshParent = null)
        {
            m_Authoring = authoring ?? LocalizationAuthoringService.Shared;
            m_Service = service ?? LocalizationImportService.Shared;
            m_ErrorChanged = errorChanged;
            m_RefreshParent = refreshParent;
            name = "localization-import-workbench";
            style.flexGrow = 1;
            style.minHeight = 0;
            style.minWidth = 0;
            Build();
        }

        private void Build()
        {
            var toolbar = new VisualElement { name = "localization-import-toolbar" };
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.flexShrink = 0;
            toolbar.style.minHeight = 30;
            toolbar.style.maxHeight = 30;
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = DividerColor();
            toolbar.style.backgroundColor = HeaderBackgroundColor();

            var title = new Label("配置表导入") { name = "localization-import-title" };
            title.style.width = 82;
            title.style.minWidth = 82;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolbar.Add(title);

            m_TableField = new DropdownField("配置表") { name = "localization-import-table" };
            m_TableField.style.flexGrow = 1;
            m_TableField.style.flexShrink = 1;
            m_TableField.style.minWidth = 180;
            m_TableField.style.maxWidth = 360;
            m_TableField.RegisterValueChangedCallback(_ => SelectTable());
            ConfigureToolbarField(m_TableField, 48);
            toolbar.Add(m_TableField);

            m_KeyField = new DropdownField("Key 字段") { name = "localization-import-key-field" };
            m_KeyField.style.width = 160;
            m_KeyField.style.minWidth = 140;
            m_KeyField.style.maxWidth = 180;
            m_KeyField.style.flexShrink = 1;
            m_KeyField.style.marginLeft = 8;
            m_KeyField.RegisterValueChangedCallback(_ => OnKeyFieldChanged());
            ConfigureToolbarField(m_KeyField, 58);
            toolbar.Add(m_KeyField);

            m_LanguageFieldMenu = new Button(ShowLanguageFieldMenu)
            {
                name = "localization-import-language-fields",
                text = "语言字段 (0) ▾",
                tooltip = "选择一个或多个配置表语言字段"
            };
            m_LanguageFieldMenu.style.width = 150;
            m_LanguageFieldMenu.style.minWidth = 130;
            m_LanguageFieldMenu.style.maxWidth = 170;
            m_LanguageFieldMenu.style.flexShrink = 1;
            m_LanguageFieldMenu.style.marginLeft = 8;
            toolbar.Add(m_LanguageFieldMenu);

            var refresh = new Button(RefreshSource)
            {
                name = "localization-import-refresh",
                text = "刷新表"
            };
            refresh.tooltip = "重新读取配置表目录";
            refresh.style.marginLeft = 8;
            ConfigureToolbarButton(refresh, 56);
            toolbar.Add(refresh);

            m_PreviewButton = new Button(CreatePlan)
            {
                name = "localization-import-preview-button",
                text = "预览导入"
            };
            m_PreviewButton.style.marginLeft = 6;
            ConfigureToolbarButton(m_PreviewButton, 72);
            toolbar.Add(m_PreviewButton);

            m_ApplyButton = new Button(Apply)
            {
                name = "localization-import-apply-button",
                text = "应用导入"
            };
            m_ApplyButton.style.marginLeft = 6;
            ConfigureToolbarButton(m_ApplyButton, 72);
            toolbar.Add(m_ApplyButton);
            Add(toolbar);

            m_MappingRows = new VisualElement { name = "localization-import-mappings" };
            m_MappingRows.style.flexShrink = 0;
            m_MappingRows.style.borderBottomWidth = 1;
            m_MappingRows.style.borderBottomColor = DividerColor();
            Add(m_MappingRows);

            m_ConflictActions = new VisualElement { name = "localization-import-conflict-actions" };
            m_ConflictActions.style.flexDirection = FlexDirection.Row;
            m_ConflictActions.style.alignItems = Align.Center;
            m_ConflictActions.style.flexShrink = 0;
            m_ConflictActions.style.minHeight = 30;
            m_ConflictActions.style.paddingLeft = 8;
            m_ConflictActions.style.paddingRight = 8;
            m_ConflictActions.style.borderBottomWidth = 1;
            m_ConflictActions.style.borderBottomColor = DividerColor();
            m_ConflictActions.style.display = DisplayStyle.None;

            var conflictLabel = new Label("冲突处理");
            conflictLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            conflictLabel.style.marginRight = 6;
            m_ConflictActions.Add(conflictLabel);

            m_UseAssetButton = new Button(() => ResolveAll(LocalizationConflictResolution.UseAsset))
            {
                name = "localization-import-use-asset",
                text = "全部用资产"
            };
            ConfigureToolbarButton(m_UseAssetButton, 86);
            m_ConflictActions.Add(m_UseAssetButton);

            m_UseSourceButton = new Button(() => ResolveAll(LocalizationConflictResolution.UseSource))
            {
                name = "localization-import-use-source",
                text = "全部用配置表"
            };
            m_UseSourceButton.style.marginLeft = 6;
            ConfigureToolbarButton(m_UseSourceButton, 98);
            m_ConflictActions.Add(m_UseSourceButton);
            Add(m_ConflictActions);

            m_Status = new Label { name = "localization-import-status" };
            m_Status.style.whiteSpace = WhiteSpace.Normal;
            m_Status.style.flexShrink = 0;
            m_Status.style.minHeight = 28;
            m_Status.style.paddingLeft = 8;
            m_Status.style.paddingRight = 8;
            m_Status.style.paddingTop = 5;
            m_Status.style.paddingBottom = 5;
            m_Status.style.borderBottomWidth = 1;
            m_Status.style.borderBottomColor = DividerColor();
            m_Status.style.color = SecondaryTextColor();
            Add(m_Status);

            m_Preview = new ScrollView(ScrollViewMode.VerticalAndHorizontal) { name = "localization-import-preview" };
            m_Preview.style.flexGrow = 1;
            m_Preview.style.minHeight = 0;
            m_Preview.style.minWidth = 0;
            Add(m_Preview);

            RefreshSource();
        }

        private void RefreshSource()
        {
            try
            {
                m_SourceSnapshot = m_Service.RefreshSource();
                var tables = m_SourceSnapshot?.Tables
                    .Where(table => table != null)
                    .OrderBy(table => table.TableId, StringComparer.Ordinal)
                    .ToArray() ?? Array.Empty<LubanTableDescriptor>();
                var selectedId = m_SelectedTable?.TableId;
                m_SelectedTable = tables.FirstOrDefault(table =>
                    string.Equals(table.TableId, selectedId, StringComparison.Ordinal)) ?? tables.FirstOrDefault();
                if (string.Equals(selectedId, m_SelectedTable?.TableId, StringComparison.Ordinal) is false)
                {
                    m_SelectedLanguageFields.Clear();
                }

                RebuildTableChoices(tables);
                SelectTable();
                var sourceError = m_SourceSnapshot?.Diagnostics.FirstOrDefault(diagnostic =>
                    diagnostic.Severity == LubanDiagnosticSeverity.Error);
                if (sourceError != null)
                {
                    SetStatus(sourceError.Message, true);
                }
            }
            catch (Exception exception)
            {
                m_SourceSnapshot = null;
                m_SelectedTable = null;
                InvalidatePlan();
                SetStatus($"读取配置表失败：{exception.Message}", true);
            }
        }

        private void SelectTable()
        {
            m_SelectedTable = m_TableField != null &&
                              m_TablesByDisplayName.TryGetValue(m_TableField.value ?? string.Empty, out var table)
                ? table
                : null;
            if (m_TableField != null)
            {
                m_TableField.tooltip = m_SelectedTable?.TableId ?? string.Empty;
            }

            var fields = m_SelectedTable?.Fields
                .Where(field => field != null)
                .Select(field => field.Name)
                .ToArray() ?? Array.Empty<string>();
            SetDropdownChoices(m_KeyField, fields, fields.FirstOrDefault());
            RetainAvailableLanguageFields(fields);
            UpdateLanguageFieldMenuText();
            RebuildMappings();
            InvalidatePlan();
        }

        private void RebuildTableChoices(IReadOnlyList<LubanTableDescriptor> tables)
        {
            m_TablesByDisplayName.Clear();
            foreach (var table in tables ?? Array.Empty<LubanTableDescriptor>())
            {
                var baseName = CreateTableDisplayName(table);
                var displayName = baseName;
                var suffix = 2;
                while (m_TablesByDisplayName.ContainsKey(displayName))
                {
                    displayName = $"{baseName} ({suffix++})";
                }

                m_TablesByDisplayName.Add(displayName, table);
            }

            var selectedDisplay = m_TablesByDisplayName
                .FirstOrDefault(pair => string.Equals(
                    pair.Value.TableId,
                    m_SelectedTable?.TableId,
                    StringComparison.Ordinal))
                .Key;
            SetDropdownChoices(m_TableField, m_TablesByDisplayName.Keys, selectedDisplay);
            if (m_TableField != null)
            {
                m_TableField.tooltip = m_SelectedTable?.TableId ?? string.Empty;
            }
        }

        private static string CreateTableDisplayName(LubanTableDescriptor table)
        {
            if (table == null)
            {
                return EmptyChoice;
            }

            var source = Path.GetFileNameWithoutExtension(table.SourceId);
            var tableName = string.IsNullOrWhiteSpace(table.TableName) ? table.SheetName : table.TableName;
            return string.IsNullOrWhiteSpace(source) ? tableName : $"{source} / {tableName}";
        }

        private void OnKeyFieldChanged()
        {
            var fields = m_SelectedTable?.Fields
                .Where(field => field != null)
                .Select(field => field.Name)
                .ToArray() ?? Array.Empty<string>();
            RetainAvailableLanguageFields(fields);
            UpdateLanguageFieldMenuText();
            RebuildMappings();
            InvalidatePlan();
        }

        private void RetainAvailableLanguageFields(IReadOnlyCollection<string> fields)
        {
            var available = new HashSet<string>(fields ?? Array.Empty<string>(), StringComparer.Ordinal);
            available.Remove(m_KeyField?.value ?? string.Empty);
            foreach (var selected in m_SelectedLanguageFields.Keys.Where(field =>
                         available.Contains(field) is false).ToArray())
            {
                m_SelectedLanguageFields.Remove(selected);
            }
        }

        private void ShowLanguageFieldMenu()
        {
            var fields = m_SelectedTable?.Fields
                .Where(field => field != null &&
                                string.IsNullOrWhiteSpace(field.Name) is false &&
                                string.Equals(field.Name, m_KeyField?.value, StringComparison.Ordinal) is false)
                .Select(field => field.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(field => field, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
            var menu = new GenericMenu();
            if (fields.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("没有可选语言字段"));
            }
            else
            {
                foreach (var field in fields)
                {
                    var sourceField = field;
                    menu.AddItem(
                        new GUIContent(sourceField),
                        m_SelectedLanguageFields.ContainsKey(sourceField),
                        () => ToggleLanguageField(sourceField));
                }
            }

            menu.DropDown(m_LanguageFieldMenu.worldBound);
        }

        private void ToggleLanguageField(string sourceField)
        {
            if (m_SelectedLanguageFields.Remove(sourceField) is false)
            {
                m_SelectedLanguageFields[sourceField] = LocalizationAuthoringService.NormalizeLocale(sourceField);
            }

            UpdateLanguageFieldMenuText();
            RebuildMappings();
            InvalidatePlan();
        }

        private void UpdateLanguageFieldMenuText()
        {
            if (m_LanguageFieldMenu == null)
            {
                return;
            }

            m_LanguageFieldMenu.text = $"语言字段 ({m_SelectedLanguageFields.Count}) ▾";
            m_LanguageFieldMenu.SetEnabled(m_SelectedTable?.Fields.Any(field =>
                field != null &&
                string.IsNullOrWhiteSpace(field.Name) is false &&
                string.Equals(field.Name, m_KeyField?.value, StringComparison.Ordinal) is false) == true);
        }

        private void RebuildMappings()
        {
            m_MappingRows.Clear();
            var snapshot = m_Authoring.Refresh();
            if (m_SelectedLanguageFields.Count == 0)
            {
                return;
            }

            m_MappingRows.Add(CreateMappingHeader());
            var index = 0;
            foreach (var pair in m_SelectedLanguageFields.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                m_MappingRows.Add(CreateMappingRow(snapshot, pair.Key, pair.Value, index++));
            }
        }

        private static VisualElement CreateMappingHeader()
        {
            var header = CreateTableRow("localization-import-mapping-header", -1, true);
            header.Add(CreateHeaderCell("配置表语言字段", 260));
            header.Add(CreateHeaderCell("目标语言", 220));
            var status = new Label("资产状态");
            status.style.flexGrow = 1;
            status.style.minWidth = 160;
            status.style.paddingLeft = 8;
            status.style.unityFontStyleAndWeight = FontStyle.Bold;
            status.style.unityTextAlign = TextAnchor.MiddleLeft;
            header.Add(status);
            return header;
        }

        private VisualElement CreateMappingRow(
            LocalizationAuthoringSnapshot snapshot,
            string sourceField,
            string targetLocale,
            int index)
        {
            var row = CreateTableRow($"localization-import-mapping-{SafeName(sourceField)}", index, false);
            row.Add(CreateCell(sourceField, 260));

            var targetCell = CreateFixedCell(220);
            var target = new TextField
            {
                name = $"localization-import-target-{SafeName(sourceField)}",
                value = targetLocale,
                isDelayed = true,
                tooltip = "配置表字段写入的 Locale；不存在时应用导入会创建对应资产"
            };
            target.style.flexGrow = 1;
            target.style.minWidth = 0;
            target.RegisterValueChangedCallback(evt =>
            {
                var locale = LocalizationAuthoringService.NormalizeLocale(evt.newValue);
                m_SelectedLanguageFields[sourceField] = locale;
                target.SetValueWithoutNotify(locale);
                InvalidatePlan();
                RebuildMappings();
            });
            targetCell.Add(target);
            row.Add(targetCell);

            var normalized = LocalizationAuthoringService.NormalizeLocale(targetLocale);
            var exists = normalized.Length > 0 && snapshot.TryGetLocale(normalized, out _);
            var status = new Label(normalized.Length == 0
                ? "目标语言不能为空"
                : exists ? "已有语言资产" : "应用时创建语言资产");
            status.name = $"localization-import-target-status-{SafeName(sourceField)}";
            status.style.flexGrow = 1;
            status.style.minWidth = 160;
            status.style.paddingLeft = 8;
            status.style.unityTextAlign = TextAnchor.MiddleLeft;
            status.style.color = normalized.Length == 0
                ? new Color(0.95f, 0.35f, 0.3f)
                : exists ? SuccessColor() : new Color(0.95f, 0.65f, 0.25f);
            row.Add(status);
            return row;
        }

        private void CreatePlan()
        {
            try
            {
                var snapshot = m_Authoring.Refresh();
                var columns = m_SelectedLanguageFields
                    .Select(pair => new LocalizationImportColumn(pair.Value, pair.Key))
                    .Where(column => column.SourceField.Length > 0 && column.TargetLocale.Length > 0)
                    .ToArray();
                var request = new LocalizationImportRequest(
                    snapshot.Catalog.CatalogId,
                    m_SelectedTable?.TableId,
                    m_KeyField?.value,
                    m_SourceSnapshot?.Revision ?? 0,
                    columns);
                m_Plan = m_Service.CreatePlan(request);
                RefreshPlanView();
            }
            catch (Exception exception)
            {
                m_Plan = null;
                RefreshPlanView();
                SetStatus($"生成导入预览失败：{exception.Message}", true);
            }
        }

        private void Apply()
        {
            if (m_Plan == null)
            {
                SetStatus("请先生成导入预览。", true);
                return;
            }

            if (m_Plan.CanApply is false)
            {
                var error = m_Plan.Diagnostics.FirstOrDefault(diagnostic =>
                    diagnostic.Severity == LocalizationImportDiagnosticSeverity.Error);
                var message = error?.Message;
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = m_Plan.UnresolvedCount > 0
                        ? $"仍有 {m_Plan.UnresolvedCount} 个冲突或删除候选未解决，请先选择使用资产版本或配置表版本。"
                        : "当前导入计划不能应用，请重新生成导入预览。";
                }

                SetStatus(message, true);
                return;
            }

            try
            {
                var result = m_Service.Apply(m_Plan);
                if (result.Succeeded)
                {
                    m_Plan = null;
                    m_RefreshParent?.Invoke();
                    RefreshSource();
                    SetStatus(result.Message, false);
                }
                else
                {
                    RefreshPlanView();
                    SetStatus(
                        string.IsNullOrWhiteSpace(result.Message) ? "应用导入失败。" : result.Message,
                        true);
                }
            }
            catch (Exception exception)
            {
                SetStatus($"应用导入失败：{exception.Message}", true);
            }
        }

        private void ResolveAll(LocalizationConflictResolution resolution)
        {
            if (m_Plan == null)
            {
                return;
            }

            m_Plan.ResolveAll(resolution);
            RefreshPlanView();
        }

        private void Resolve(
            LocalizationImportMergeEntry entry,
            LocalizationConflictResolution resolution)
        {
            m_Plan?.Resolve(entry.KeyId, entry.TargetLocale, resolution);
            RefreshPlanView();
        }

        private void InvalidatePlan()
        {
            m_Plan = null;
            RefreshPlanView();
        }

        private void RefreshPlanView()
        {
            m_Preview?.Clear();
            var hasTable = m_SelectedTable != null &&
                           m_KeyField?.value?.Length > 0 &&
                           string.Equals(m_KeyField.value, EmptyChoice, StringComparison.Ordinal) is false;
            var hasMappings = m_SelectedLanguageFields.Count > 0 &&
                              m_SelectedLanguageFields.Values.All(locale =>
                                  LocalizationAuthoringService.NormalizeLocale(locale).Length > 0);
            m_PreviewButton?.SetEnabled(hasTable && hasMappings);
            var hasConflicts = m_Plan != null && m_Plan.Entries.Any(entry => entry.RequiresResolution);
            if (m_ConflictActions != null)
            {
                m_ConflictActions.style.display = hasConflicts ? DisplayStyle.Flex : DisplayStyle.None;
            }

            m_UseAssetButton?.SetEnabled(hasConflicts);
            m_UseSourceButton?.SetEnabled(hasConflicts);
            m_ApplyButton?.SetEnabled(m_Plan != null);
            if (m_Plan == null)
            {
                m_Status.text = hasTable ? "选择一个或多个语言字段后生成导入预览。" : "请选择配置表和 Key 字段。";
                return;
            }

            m_Preview.Add(CreatePreviewHeader());
            for (var i = 0; i < m_Plan.Entries.Count; i++)
            {
                m_Preview.Add(CreateEntryRow(m_Plan.Entries[i], i));
            }

            var summary = $"共 {m_Plan.Entries.Count} 项，待解决 {m_Plan.UnresolvedCount} 项。";
            if (m_Plan.Diagnostics.Count > 0)
            {
                summary += "  " + string.Join("  ", m_Plan.Diagnostics.Select(diagnostic => diagnostic.Message));
            }

            SetStatus(
                summary,
                m_Plan.Diagnostics.Any(diagnostic =>
                    diagnostic.Severity == LocalizationImportDiagnosticSeverity.Error));
        }

        private VisualElement CreateEntryRow(LocalizationImportMergeEntry entry, int index)
        {
            var row = CreateTableRow(
                $"localization-import-entry-{entry.KeyId}-{entry.TargetLocale}",
                index,
                false);
            row.Add(CreateCell(entry.DisplayKey, 220));
            row.Add(CreateCell(entry.TargetLocale, 90));
            row.Add(CreateCell(FormatValue(entry.BaseValue), 170));
            row.Add(CreateCell(FormatValue(entry.AssetValue), 170));
            row.Add(CreateCell(FormatValue(entry.SourceValue), 170));
            row.Add(CreateCell(FormatKind(entry.Kind), 110));
            var actions = CreateFixedCell(160);
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.alignItems = Align.Center;
            if (entry.RequiresResolution)
            {
                var useAsset = new Button(() => Resolve(entry, LocalizationConflictResolution.UseAsset))
                {
                    text = "资产",
                    tooltip = "保留 Unity 资产版本"
                };
                ConfigureRowActionButton(useAsset, 66);
                actions.Add(useAsset);
                var useSource = new Button(() => Resolve(entry, LocalizationConflictResolution.UseSource))
                {
                    text = "配置表",
                    tooltip = "采用配置表版本"
                };
                useSource.style.marginLeft = 4;
                ConfigureRowActionButton(useSource, 66);
                actions.Add(useSource);
            }

            row.Add(actions);
            return row;
        }

        private static Label CreateCell(string text, float width)
        {
            var label = new Label(text)
            {
                tooltip = text
            };
            label.style.width = width;
            label.style.minWidth = width;
            label.style.maxWidth = width;
            label.style.paddingLeft = 8;
            label.style.paddingRight = 8;
            label.style.borderRightWidth = 1;
            label.style.borderRightColor = DividerColor();
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            return label;
        }

        private static Label CreateHeaderCell(string text, float width)
        {
            var label = CreateCell(text, width);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static VisualElement CreateFixedCell(float width)
        {
            var cell = new VisualElement();
            cell.style.width = width;
            cell.style.minWidth = width;
            cell.style.maxWidth = width;
            cell.style.paddingLeft = 6;
            cell.style.paddingRight = 6;
            cell.style.justifyContent = Justify.Center;
            cell.style.borderRightWidth = 1;
            cell.style.borderRightColor = DividerColor();
            return cell;
        }

        private static VisualElement CreatePreviewHeader()
        {
            var header = CreateTableRow("localization-import-preview-header", -1, true);
            header.Add(CreateHeaderCell("Key", 220));
            header.Add(CreateHeaderCell("语言", 90));
            header.Add(CreateHeaderCell("Base", 170));
            header.Add(CreateHeaderCell("资产", 170));
            header.Add(CreateHeaderCell("配置表", 170));
            header.Add(CreateHeaderCell("状态", 110));
            header.Add(CreateHeaderCell("冲突选择", 160));
            return header;
        }

        private static VisualElement CreateTableRow(string name, int index, bool header)
        {
            var row = new VisualElement { name = name };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Stretch;
            row.style.flexShrink = 0;
            row.style.minHeight = header ? 26 : 28;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = DividerColor();
            row.style.backgroundColor = header
                ? HeaderBackgroundColor()
                : RowBackgroundColor(index);
            return row;
        }

        private static void ConfigureRowActionButton(Button button, float width)
        {
            button.style.width = width;
            button.style.minWidth = width;
            button.style.maxWidth = width;
            button.style.height = 20;
            button.style.minHeight = 20;
            button.style.maxHeight = 20;
            button.style.flexShrink = 0;
        }

        private static string SafeName(string value)
        {
            return (value ?? string.Empty)
                .Replace('/', '-')
                .Replace('\\', '-')
                .Replace(' ', '-');
        }

        private static string FormatValue(LocalizationImportValue value)
        {
            return value.Exists ? value.Value : "(缺失)";
        }

        private static string FormatKind(LocalizationMergeKind kind)
        {
            return kind switch
            {
                LocalizationMergeKind.Add => "新增",
                LocalizationMergeKind.UpdateFromSource => "配置表更新",
                LocalizationMergeKind.KeepAsset => "保留资产",
                LocalizationMergeKind.Conflict => "冲突待选",
                LocalizationMergeKind.DeleteCandidate => "删除候选",
                _ => "未变化"
            };
        }

        private void SetStatus(string message, bool error)
        {
            if (m_Status != null)
            {
                m_Status.text = message ?? string.Empty;
                m_Status.style.color = error
                    ? new Color(0.95f, 0.35f, 0.3f)
                    : SecondaryTextColor();
            }

            m_ErrorChanged?.Invoke(error ? message : null);
        }

        private static void SetDropdownChoices(
            DropdownField field,
            IEnumerable<string> values,
            string selected)
        {
            if (field == null)
            {
                return;
            }

            var choices = (values ?? Array.Empty<string>())
                .Where(value => string.IsNullOrWhiteSpace(value) is false)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            if (choices.Count == 0)
            {
                choices.Add(EmptyChoice);
            }

            selected = string.IsNullOrWhiteSpace(selected) ? choices[0] : selected;
            if (choices.Contains(selected, StringComparer.Ordinal) is false)
            {
                choices.Add(selected);
            }

            field.choices = choices;
            field.SetValueWithoutNotify(selected);
        }

        private static void ConfigureToolbarField(BaseField<string> field, float labelWidth)
        {
            field.style.minHeight = 22;
            field.style.maxHeight = 22;
            field.labelElement.style.width = labelWidth;
            field.labelElement.style.minWidth = labelWidth;
            field.labelElement.style.maxWidth = labelWidth;
            field.labelElement.style.flexShrink = 0;
            var input = field.Q<VisualElement>(className: BaseField<string>.inputUssClassName);
            if (input != null)
            {
                input.style.flexGrow = 1;
                input.style.flexShrink = 1;
                input.style.minWidth = 0;
            }
        }

        private static void ConfigureToolbarButton(Button button, float width)
        {
            button.style.width = width;
            button.style.minWidth = width;
            button.style.maxWidth = width;
            button.style.minHeight = 22;
            button.style.maxHeight = 22;
            button.style.flexShrink = 0;
        }

        private static Color HeaderBackgroundColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.22f, 0.23f, 0.25f)
                : new Color(0.83f, 0.86f, 0.89f);
        }

        private static Color RowBackgroundColor(int index)
        {
            if (index < 0 || index % 2 == 0)
            {
                return EditorGUIUtility.isProSkin
                    ? new Color(0.17f, 0.18f, 0.2f)
                    : new Color(0.95f, 0.96f, 0.97f);
            }

            return EditorGUIUtility.isProSkin
                ? new Color(0.19f, 0.2f, 0.22f)
                : new Color(0.9f, 0.92f, 0.94f);
        }

        private static Color SuccessColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.38f, 0.78f, 0.46f)
                : new Color(0.16f, 0.48f, 0.24f);
        }

        private static Color DividerColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.27f, 0.28f, 0.3f)
                : new Color(0.76f, 0.77f, 0.79f);
        }

        private static Color SecondaryTextColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.68f, 0.69f, 0.71f)
                : new Color(0.35f, 0.36f, 0.38f);
        }
    }
}
