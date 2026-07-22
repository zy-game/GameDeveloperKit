using System;
using System.Collections.Generic;
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
        private readonly Dictionary<string, DropdownField> m_MappingFields =
            new Dictionary<string, DropdownField>(StringComparer.OrdinalIgnoreCase);

        private LubanSourceSnapshot m_SourceSnapshot;
        private LubanTableDescriptor m_SelectedTable;
        private LocalizationImportPlan m_Plan;
        private DropdownField m_TableField;
        private DropdownField m_KeyField;
        private VisualElement m_MappingRows;
        private VisualElement m_Preview;
        private Label m_Status;
        private Button m_PreviewButton;
        private Button m_ApplyButton;
        private Button m_UseAssetButton;
        private Button m_UseSourceButton;

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
            style.marginTop = 18;
            style.paddingTop = 8;
            style.borderTopWidth = 1;
            style.borderTopColor = DividerColor();
            Build();
        }

        private void Build()
        {
            Add(CreateSectionHeader("配置表导入"));
            var toolbar = new VisualElement { name = "localization-import-toolbar" };
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.FlexEnd;

            m_TableField = new DropdownField("配置表") { name = "localization-import-table" };
            m_TableField.style.flexGrow = 1;
            m_TableField.style.minWidth = 240;
            m_TableField.RegisterValueChangedCallback(_ => SelectTable());
            toolbar.Add(m_TableField);

            m_KeyField = new DropdownField("Key 字段") { name = "localization-import-key-field" };
            m_KeyField.style.flexGrow = 1;
            m_KeyField.style.minWidth = 180;
            m_KeyField.style.marginLeft = 8;
            m_KeyField.RegisterValueChangedCallback(_ => InvalidatePlan());
            toolbar.Add(m_KeyField);

            var refresh = new Button(RefreshSource)
            {
                name = "localization-import-refresh",
                text = "刷新表"
            };
            refresh.tooltip = "重新读取配置表目录";
            refresh.style.marginLeft = 8;
            toolbar.Add(refresh);
            Add(toolbar);

            m_MappingRows = new VisualElement { name = "localization-import-mappings" };
            m_MappingRows.style.marginTop = 8;
            Add(m_MappingRows);

            var actions = new VisualElement { name = "localization-import-actions" };
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.alignItems = Align.Center;
            actions.style.marginTop = 8;

            m_PreviewButton = new Button(CreatePlan)
            {
                name = "localization-import-preview-button",
                text = "预览导入"
            };
            actions.Add(m_PreviewButton);

            m_UseAssetButton = new Button(() => ResolveAll(LocalizationConflictResolution.UseAsset))
            {
                name = "localization-import-use-asset",
                text = "全部用资产"
            };
            m_UseAssetButton.style.marginLeft = 6;
            actions.Add(m_UseAssetButton);

            m_UseSourceButton = new Button(() => ResolveAll(LocalizationConflictResolution.UseSource))
            {
                name = "localization-import-use-source",
                text = "全部用配置表"
            };
            m_UseSourceButton.style.marginLeft = 6;
            actions.Add(m_UseSourceButton);

            m_ApplyButton = new Button(Apply)
            {
                name = "localization-import-apply-button",
                text = "应用导入"
            };
            m_ApplyButton.style.marginLeft = 6;
            actions.Add(m_ApplyButton);
            Add(actions);

            m_Status = new Label { name = "localization-import-status" };
            m_Status.style.whiteSpace = WhiteSpace.Normal;
            m_Status.style.marginTop = 6;
            m_Status.style.color = SecondaryTextColor();
            Add(m_Status);

            m_Preview = new ScrollView(ScrollViewMode.Vertical) { name = "localization-import-preview" };
            m_Preview.style.maxHeight = 360;
            m_Preview.style.minHeight = 80;
            m_Preview.style.marginTop = 8;
            m_Preview.style.borderTopWidth = 1;
            m_Preview.style.borderBottomWidth = 1;
            m_Preview.style.borderTopColor = DividerColor();
            m_Preview.style.borderBottomColor = DividerColor();
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
                SetDropdownChoices(m_TableField, tables.Select(table => table.TableId), m_SelectedTable?.TableId);
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
            var tableId = m_TableField?.value ?? string.Empty;
            m_SelectedTable = m_SourceSnapshot?.Tables.FirstOrDefault(table =>
                string.Equals(table.TableId, tableId, StringComparison.Ordinal));
            var fields = m_SelectedTable?.Fields
                .Where(field => field != null)
                .Select(field => field.Name)
                .ToArray() ?? Array.Empty<string>();
            SetDropdownChoices(m_KeyField, fields, fields.FirstOrDefault());
            RebuildMappings();
            InvalidatePlan();
        }

        private void RebuildMappings()
        {
            m_MappingFields.Clear();
            m_MappingRows.Clear();
            var snapshot = m_Authoring.Refresh();
            var fields = m_SelectedTable?.Fields
                .Where(field => field != null)
                .Select(field => field.Name)
                .ToArray() ?? Array.Empty<string>();
            var locales = (snapshot?.Catalog?.Locales ?? Array.Empty<LocalizationLocaleDescriptor>())
                .Where(locale => locale != null)
                .OrderBy(locale => locale.Locale, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var locale in locales)
            {
                var choices = new[] { EmptyChoice }.Concat(fields).ToList();
                var field = new DropdownField
                {
                    label = locale.Locale,
                    choices = choices,
                    index = 0,
                    name = $"localization-import-mapping-{locale.Locale}"
                };
                field.style.flexGrow = 1;
                field.style.marginBottom = 4;
                field.RegisterValueChangedCallback(_ => InvalidatePlan());
                m_MappingFields.Add(locale.Locale, field);
                m_MappingRows.Add(field);
            }

            if (m_MappingFields.Count == 0)
            {
                m_MappingRows.Add(new Label("当前 Catalog 没有可导入的目标语言。"));
            }
        }

        private void CreatePlan()
        {
            try
            {
                var snapshot = m_Authoring.Refresh();
                var columns = m_MappingFields
                    .Select(pair => new LocalizationImportColumn(
                        pair.Key,
                        pair.Value.value == EmptyChoice ? string.Empty : pair.Value.value))
                    .Where(column => column.SourceField.Length > 0)
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
            if (m_Plan?.CanApply != true)
            {
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
                    SetStatus(result.Message, true);
                    RefreshPlanView();
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
            m_PreviewButton?.SetEnabled(hasTable && m_MappingFields.Values.Any(field => field.value != EmptyChoice));
            m_UseAssetButton?.SetEnabled(m_Plan != null && m_Plan.Entries.Any(entry => entry.RequiresResolution));
            m_UseSourceButton?.SetEnabled(m_Plan != null && m_Plan.Entries.Any(entry => entry.RequiresResolution));
            m_ApplyButton?.SetEnabled(m_Plan?.CanApply == true);
            if (m_Plan == null)
            {
                m_Status.text = hasTable ? "选择语言源字段后生成导入预览。" : "请选择配置表和 Key 字段。";
                return;
            }

            var header = new Label("Key | Locale | Base | 资产 | 配置表 | 状态")
            {
                name = "localization-import-preview-header"
            };
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.paddingTop = 5;
            header.style.paddingBottom = 5;
            m_Preview.Add(header);
            foreach (var entry in m_Plan.Entries)
            {
                m_Preview.Add(CreateEntryRow(entry));
            }

            SetStatus(
                m_Plan.Diagnostics.Count == 0
                    ? $"共 {m_Plan.Entries.Count} 项，待解决 {m_Plan.UnresolvedCount} 项。"
                    : string.Join("\n", m_Plan.Diagnostics.Select(diagnostic => diagnostic.Message)),
                m_Plan.Diagnostics.Any(diagnostic =>
                    diagnostic.Severity == LocalizationImportDiagnosticSeverity.Error));
        }

        private VisualElement CreateEntryRow(LocalizationImportMergeEntry entry)
        {
            var row = new VisualElement
            {
                name = $"localization-import-entry-{entry.KeyId}-{entry.TargetLocale}"
            };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = DividerColor();

            row.Add(CreateCell(entry.DisplayKey, 180));
            row.Add(CreateCell(entry.TargetLocale, 70));
            row.Add(CreateCell(FormatValue(entry.BaseValue), 120));
            row.Add(CreateCell(FormatValue(entry.AssetValue), 120));
            row.Add(CreateCell(FormatValue(entry.SourceValue), 120));
            row.Add(CreateCell(FormatKind(entry.Kind), 110));
            if (entry.RequiresResolution)
            {
                var useAsset = new Button(() => Resolve(entry, LocalizationConflictResolution.UseAsset))
                {
                    text = "资产",
                    tooltip = "保留 Unity 资产版本"
                };
                useAsset.style.marginLeft = 4;
                row.Add(useAsset);
                var useSource = new Button(() => Resolve(entry, LocalizationConflictResolution.UseSource))
                {
                    text = "表",
                    tooltip = "采用配置表版本"
                };
                useSource.style.marginLeft = 4;
                row.Add(useSource);
            }

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
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            return label;
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

        private static Label CreateSectionHeader(string text)
        {
            var header = new Label(text);
            header.style.fontSize = 13;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 8;
            header.style.marginBottom = 6;
            header.style.paddingBottom = 5;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = DividerColor();
            return header;
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
