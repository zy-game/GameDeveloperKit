using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.LocalizationEditor;
using GameDeveloperKit.LubanConfigEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.EditorConfiguration
{
    internal sealed class EditorConfigurationPanel : VisualElement
    {
        private const string EmptyChoice = "(未选择)";

        private readonly EditorGlobalConfig m_ProjectConfig;
        private readonly EditorUserConfig m_UserConfig;
        private readonly Action m_OnSaved;

        private LubanSourceSnapshot m_SourceSnapshot;
        private VisualElement m_LocalizationContent;
        private Label m_ErrorLabel;

        public EditorConfigurationPanel(Action onSaved = null)
        {
            name = "global-config-panel";
            m_ProjectConfig = EditorGlobalConfig.LoadOrCreate();
            m_UserConfig = EditorUserConfig.LoadOrCreate();
            m_OnSaved = onSaved;

            style.flexShrink = 0;
            style.minWidth = 0;
            style.paddingLeft = 8;
            style.paddingRight = 8;
            style.paddingTop = 5;
            style.paddingBottom = 5;
            style.borderBottomWidth = 1;
            style.borderBottomColor = EditorGUIUtility.isProSkin
                ? new Color(0.28f, 0.29f, 0.31f)
                : new Color(0.72f, 0.74f, 0.77f);
            style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.19f, 0.2f, 0.22f)
                : new Color(0.86f, 0.87f, 0.89f);
            Build();
        }

        private void Build()
        {
            var basicRow = CreateToolbarRow("全局配置");
            AddToolbarField(basicRow, CreateTextField(
                "table-directory-field",
                "配置表目录",
                m_ProjectConfig.Luban.TableDirectory,
                value =>
                {
                    m_ProjectConfig.Luban.TableDirectory = value;
                    RefreshSourceCatalog();
                    SaveConfigs();
                    RebuildLocalizationContent();
                }), 210, 72);
            AddToolbarField(basicRow, CreateTextField(
                "generated-code-directory-field",
                "代码目录",
                m_ProjectConfig.Luban.GeneratedCodeDirectory,
                value =>
                {
                    m_ProjectConfig.Luban.GeneratedCodeDirectory = value;
                    SaveConfigs();
                }), 220, 60);
            AddToolbarField(basicRow, CreateTextField(
                "generated-data-directory-field",
                "数据目录",
                m_ProjectConfig.Luban.GeneratedDataDirectory,
                value =>
                {
                    m_ProjectConfig.Luban.GeneratedDataDirectory = value;
                    SaveConfigs();
                }), 220, 60);
            AddToolbarField(basicRow, CreateTextField(
                "code-namespace-field",
                "命名空间",
                m_ProjectConfig.Luban.CodeNamespace,
                value =>
                {
                    m_ProjectConfig.Luban.CodeNamespace = value;
                    SaveConfigs();
                }), 150, 60);
            AddToolbarField(basicRow, CreateTextField(
                "luban-dll-path-field",
                "Luban.dll",
                m_UserConfig.LubanDllPath,
                value =>
                {
                    m_UserConfig.LubanDllPath = value;
                    SaveConfigs();
                }), 190, 62);
            Add(basicRow);

            m_LocalizationContent = new VisualElement { name = "localization-config-content" };
            Add(m_LocalizationContent);
            RefreshSourceCatalog();
            RebuildLocalizationContent();

            m_ErrorLabel = new Label { name = "global-config-validation" };
            m_ErrorLabel.style.whiteSpace = WhiteSpace.Normal;
            m_ErrorLabel.style.color = new Color(0.95f, 0.35f, 0.3f);
            m_ErrorLabel.style.marginLeft = 72;
            m_ErrorLabel.style.marginTop = 3;
            Add(m_ErrorLabel);
            RefreshValidationMessage(null);
        }

        private void RebuildLocalizationContent()
        {
            m_LocalizationContent.Clear();
            var tables = m_SourceSnapshot?.Tables
                .OrderBy(table => table.TableId, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<LubanTableDescriptor>();
            var localization = m_ProjectConfig.Localization;

            var row = CreateToolbarRow("本地化");
            AddToolbarField(row, CreateChoiceField(
                "localization-table-field",
                "文本表",
                localization.TableId,
                tables.Select(table => table.TableId),
                value =>
                {
                    localization.TableId = value;
                    SaveConfigs();
                    schedule.Execute(RebuildLocalizationContent);
                }), 360, 48);

            var selectedTable = tables.FirstOrDefault(table =>
                string.Equals(table.TableId, localization.TableId, StringComparison.Ordinal));
            var fields = selectedTable?.Fields.Select(field => field.Name).ToArray() ?? Array.Empty<string>();
            AddToolbarField(row, CreateChoiceField(
                "localization-key-field",
                "Key",
                localization.KeyField,
                fields,
                value =>
                {
                    localization.KeyField = value;
                    SaveConfigs();
                }), 180, 30);

            AddToolbarField(row, CreateChoiceField(
                "localization-preview-locale-field",
                "预览语言",
                localization.PreviewLocale,
                localization.LocaleFields.Select(mapping => mapping.Locale),
                value =>
                {
                    localization.PreviewLocale = value;
                    SaveConfigs();
                }), 180, 60);

            var addMappingButton = new Button(AddLocaleMapping) { text = "+" };
            addMappingButton.name = "add-locale-mapping-button";
            addMappingButton.tooltip = "添加语言字段映射";
            addMappingButton.style.width = 28;
            addMappingButton.style.height = 22;
            row.Add(addMappingButton);

            var contractStatus = CreateContractStatus(localization);
            contractStatus.style.flexGrow = 1;
            contractStatus.style.minWidth = 0;
            contractStatus.style.marginLeft = 8;
            contractStatus.style.whiteSpace = WhiteSpace.NoWrap;
            contractStatus.style.overflow = Overflow.Hidden;
            row.Add(contractStatus);
            m_LocalizationContent.Add(row);

            for (var index = 0; index < localization.LocaleFields.Count; index++)
            {
                AddLocaleMappingRow(index, localization.LocaleFields[index], fields);
            }
        }

        private void AddLocaleMappingRow(
            int index,
            LocalizationLocaleField mapping,
            IReadOnlyCollection<string> fields)
        {
            var row = new VisualElement { name = $"locale-mapping-{index}" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minWidth = 0;
            row.style.marginLeft = 72;
            row.style.marginTop = 3;

            var localeField = new TextField
            {
                value = mapping.Locale ?? string.Empty,
                isDelayed = true
            };
            localeField.style.flexGrow = 1;
            localeField.style.flexBasis = 150;
            localeField.style.minWidth = 80;
            localeField.tooltip = "语言标识，例如 zh-CN";
            localeField.RegisterValueChangedCallback(evt =>
            {
                mapping.Locale = evt.newValue;
                SaveConfigs();
                schedule.Execute(RebuildLocalizationContent);
            });
            row.Add(localeField);

            var fieldChoice = CreateChoiceField(
                string.Empty,
                string.Empty,
                mapping.FieldName,
                fields,
                value =>
                {
                    mapping.FieldName = value;
                    SaveConfigs();
                });
            fieldChoice.style.flexGrow = 1;
            fieldChoice.style.flexBasis = 240;
            fieldChoice.style.marginLeft = 6;
            row.Add(fieldChoice);

            var removeButton = new Button(() => RemoveLocaleMapping(mapping)) { text = "−" };
            removeButton.tooltip = "删除语言字段映射";
            removeButton.style.width = 28;
            removeButton.style.marginLeft = 6;
            row.Add(removeButton);
            m_LocalizationContent.Add(row);
        }

        private void AddLocaleMapping()
        {
            m_ProjectConfig.Localization.LocaleFields.Add(new LocalizationLocaleField());
            RebuildLocalizationContent();
        }

        private void RemoveLocaleMapping(LocalizationLocaleField mapping)
        {
            m_ProjectConfig.Localization.LocaleFields.Remove(mapping);
            SaveConfigs();
            RebuildLocalizationContent();
        }

        private Label CreateContractStatus(LocalizationProjectConfig localization)
        {
            var label = new Label();
            label.name = "localization-contract-status";
            if (string.IsNullOrWhiteSpace(localization.TableId) || m_SourceSnapshot == null)
            {
                label.text = "选择一张 Luban 表后可校验本地化字段契约。";
                label.style.color = Color.gray;
                return label;
            }

            try
            {
                var result = LocalizationTableContractValidator.Validate(
                    m_SourceSnapshot,
                    LubanSourceCatalog.Shared,
                    localization);
                label.text = result.IsValid
                    ? $"契约有效：{result.Data.Rows.Count} 条文本，{localization.LocaleFields.Count} 个语言字段。"
                    : string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message));
                label.style.color = result.IsValid
                    ? new Color(0.35f, 0.8f, 0.45f)
                    : new Color(0.95f, 0.35f, 0.3f);
            }
            catch (Exception exception)
            {
                label.text = $"本地化契约校验失败：{exception.Message}";
                label.style.color = new Color(0.95f, 0.35f, 0.3f);
            }

            return label;
        }

        private void SaveConfigs()
        {
            if (m_ProjectConfig.TryValidate(out var error) is false)
            {
                RefreshValidationMessage(error);
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(m_ProjectConfig.Localization.TableId) is false)
                {
                    var contract = LocalizationTableContractValidator.Validate(
                        m_SourceSnapshot,
                        LubanSourceCatalog.Shared,
                        m_ProjectConfig.Localization);
                    if (contract.IsValid is false)
                    {
                        RefreshValidationMessage(string.Join(
                            Environment.NewLine,
                            contract.Diagnostics
                                .Where(diagnostic => diagnostic.Severity == LocalizationContractDiagnosticSeverity.Error)
                                .Select(diagnostic => diagnostic.Message)));
                        return;
                    }
                }

                m_ProjectConfig.Save();
                m_UserConfig.Save();
                RefreshValidationMessage(null);
                m_OnSaved?.Invoke();
            }
            catch (Exception exception)
            {
                RefreshValidationMessage($"保存 Editor 配置失败：{exception.Message}");
                Debug.LogException(exception);
            }
        }

        private void RefreshSourceCatalog()
        {
            try
            {
                m_SourceSnapshot = LubanSourceCatalog.Shared.Refresh(m_ProjectConfig.Luban);
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
                            $"刷新本地化配置表失败：{exception.Message}")
                    });
            }
        }

        private void RefreshValidationMessage(string error)
        {
            if (m_ErrorLabel == null)
            {
                return;
            }

            m_ErrorLabel.text = error ?? string.Empty;
            m_ErrorLabel.style.display = string.IsNullOrWhiteSpace(error) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static VisualElement CreateToolbarRow(string title)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minWidth = 0;
            row.style.minHeight = 25;

            var label = new Label(title);
            label.style.width = 64;
            label.style.minWidth = 64;
            label.style.marginRight = 8;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(label);
            return row;
        }

        private static void AddToolbarField<TValue>(
            VisualElement row,
            BaseField<TValue> field,
            float basis,
            float labelWidth)
        {
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            field.style.flexBasis = basis;
            field.style.minWidth = Math.Min(100, basis);
            field.style.marginRight = 8;
            field.style.marginBottom = 0;
            field.labelElement.style.width = labelWidth;
            field.labelElement.style.minWidth = labelWidth;
            field.labelElement.style.maxWidth = labelWidth;
            row.Add(field);
        }

        private static TextField CreateTextField(
            string name,
            string label,
            string value,
            Action<string> changed)
        {
            var field = new TextField(label)
            {
                name = name,
                value = value ?? string.Empty,
                isDelayed = true
            };
            ConfigureField(field);
            field.RegisterValueChangedCallback(evt => changed(evt.newValue));
            return field;
        }

        private static DropdownField CreateChoiceField(
            string name,
            string label,
            string current,
            IEnumerable<string> values,
            Action<string> changed)
        {
            var choices = values
                .Where(value => string.IsNullOrWhiteSpace(value) is false)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            current = current?.Trim() ?? string.Empty;
            if (current.Length > 0 && choices.Contains(current) is false)
            {
                choices.Add(current);
            }

            choices.Insert(0, EmptyChoice);
            var selected = current.Length == 0 ? EmptyChoice : current;
            var field = new DropdownField(label, choices, choices.IndexOf(selected)) { name = name };
            ConfigureField(field);
            field.RegisterValueChangedCallback(evt => changed(evt.newValue == EmptyChoice ? string.Empty : evt.newValue));
            return field;
        }

        private static void ConfigureField<TValue>(BaseField<TValue> field)
        {
            field.style.flexGrow = 1;
            field.style.minWidth = 0;
            field.style.marginBottom = 5;
            field.labelElement.style.width = 126;
            field.labelElement.style.minWidth = 126;
            field.labelElement.style.maxWidth = 126;
        }
    }
}
