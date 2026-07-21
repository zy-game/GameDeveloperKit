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
    internal sealed class EditorConfigurationSettingsProvider : SettingsProvider
    {
        private SerializedObject m_ProjectConfig;
        private SerializedObject m_UserConfig;
        private string m_Error;
        private LubanSourceSnapshot m_SourceSnapshot;

        private EditorConfigurationSettingsProvider()
            : base("Project/GameDeveloperKit/Configuration", SettingsScope.Project)
        {
            label = "Configuration";
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new EditorConfigurationSettingsProvider
            {
                keywords = GetSearchKeywordsFromSerializedObject(
                    new SerializedObject(EditorGlobalConfig.LoadOrCreate()))
            };
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            m_ProjectConfig = new SerializedObject(EditorGlobalConfig.LoadOrCreate());
            m_UserConfig = new SerializedObject(EditorUserConfig.LoadOrCreate());
            RefreshSourceCatalog();
        }

        public override void OnGUI(string searchContext)
        {
            m_ProjectConfig.Update();
            m_UserConfig.Update();

            EditorGUILayout.LabelField("项目共享配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "这些值随项目共享。路径必须相对于项目根目录，不能使用绝对路径或跳出项目目录。",
                MessageType.Info);
            DrawLubanProjectConfig(m_ProjectConfig.FindProperty("m_Luban"));
            EditorGUILayout.Space(8f);
            DrawLocalizationProjectConfig(m_ProjectConfig.FindProperty("m_Localization"));

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("仅本机配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "这些值只保存在 UserSettings，不应提交或共享给其他开发者。",
                MessageType.Info);
            EditorGUILayout.PropertyField(
                m_UserConfig.FindProperty("m_LubanDllPath"),
                new GUIContent("Luban.dll 路径"));

            if (string.IsNullOrWhiteSpace(m_Error) is false)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox(m_Error, MessageType.Error);
            }

            if (m_ProjectConfig.hasModifiedProperties is false && m_UserConfig.hasModifiedProperties is false)
            {
                return;
            }

            m_ProjectConfig.ApplyModifiedProperties();
            m_UserConfig.ApplyModifiedProperties();
            SaveConfigs();
        }

        private void SaveConfigs()
        {
            var project = EditorGlobalConfig.LoadOrCreate();
            if (project.TryValidate(out var error) is false)
            {
                m_Error = error;
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(project.Localization.TableId) is false)
                {
                    m_SourceSnapshot ??= LubanSourceCatalog.Shared.Refresh(project.Luban);
                    var contract = LocalizationTableContractValidator.Validate(
                        m_SourceSnapshot,
                        LubanSourceCatalog.Shared,
                        project.Localization);
                    if (contract.IsValid is false)
                    {
                        m_Error = string.Join(
                            Environment.NewLine,
                            contract.Diagnostics
                                .Where(diagnostic => diagnostic.Severity == LocalizationContractDiagnosticSeverity.Error)
                                .Select(diagnostic => diagnostic.Message));
                        return;
                    }
                }

                project.Save();
                EditorUserConfig.LoadOrCreate().Save();
                m_Error = null;
            }
            catch (Exception exception)
            {
                m_Error = $"保存 Editor 配置失败：{exception.Message}";
                Debug.LogException(exception);
            }
        }

        private static void DrawLubanProjectConfig(SerializedProperty config)
        {
            EditorGUILayout.LabelField("配置表", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                config.FindPropertyRelative("m_TableDirectory"),
                new GUIContent("配置表目录"));
            EditorGUILayout.PropertyField(
                config.FindPropertyRelative("m_GeneratedCodeDirectory"),
                new GUIContent("生成代码目录"));
            EditorGUILayout.PropertyField(
                config.FindPropertyRelative("m_GeneratedDataDirectory"),
                new GUIContent("导出数据目录"));
            EditorGUILayout.PropertyField(
                config.FindPropertyRelative("m_CodeNamespace"),
                new GUIContent("代码命名空间"));
        }

        private void DrawLocalizationProjectConfig(SerializedProperty config)
        {
            EditorGUILayout.LabelField("本地化", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("数据源", GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
                if (GUILayout.Button("刷新配置表", GUILayout.Width(100f)))
                {
                    RefreshSourceCatalog();
                }
            }

            var tableProperty = config.FindPropertyRelative("m_TableId");
            var tables = m_SourceSnapshot?.Tables
                .OrderBy(table => table.TableId, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<LubanTableDescriptor>();
            tableProperty.stringValue = DrawStringPopup(
                "本地化表",
                tableProperty.stringValue,
                tables.Select(table => table.TableId));
            var selectedTable = tables.FirstOrDefault(table =>
                string.Equals(table.TableId, tableProperty.stringValue, StringComparison.Ordinal));
            var fields = selectedTable?.Fields.Select(field => field.Name) ?? Enumerable.Empty<string>();

            var keyProperty = config.FindPropertyRelative("m_KeyField");
            keyProperty.stringValue = DrawStringPopup("Key 字段", keyProperty.stringValue, fields);

            var localeFields = config.FindPropertyRelative("m_LocaleFields");
            EditorGUILayout.LabelField("语言字段映射");
            EditorGUI.indentLevel++;
            for (var i = 0; i < localeFields.arraySize; i++)
            {
                var mapping = localeFields.GetArrayElementAtIndex(i);
                var localeProperty = mapping.FindPropertyRelative("m_Locale");
                var fieldProperty = mapping.FindPropertyRelative("m_FieldName");
                using (new EditorGUILayout.HorizontalScope())
                {
                    localeProperty.stringValue = EditorGUILayout.TextField(localeProperty.stringValue);
                    fieldProperty.stringValue = DrawStringPopup(
                        GUIContent.none,
                        fieldProperty.stringValue,
                        fields,
                        GUILayout.MinWidth(140f));
                    if (GUILayout.Button("删除", GUILayout.Width(48f)))
                    {
                        localeFields.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }

            if (GUILayout.Button("添加语言映射", GUILayout.Width(110f)))
            {
                var index = localeFields.arraySize;
                localeFields.InsertArrayElementAtIndex(index);
                var mapping = localeFields.GetArrayElementAtIndex(index);
                mapping.FindPropertyRelative("m_Locale").stringValue = string.Empty;
                mapping.FindPropertyRelative("m_FieldName").stringValue = string.Empty;
            }

            EditorGUI.indentLevel--;
            var previewProperty = config.FindPropertyRelative("m_PreviewLocale");
            var locales = Enumerable.Range(0, localeFields.arraySize)
                .Select(index => localeFields.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("m_Locale").stringValue)
                .Where(locale => string.IsNullOrWhiteSpace(locale) is false);
            previewProperty.stringValue = DrawStringPopup("预览语言", previewProperty.stringValue, locales);

            DrawLocalizationDiagnostics(config);
        }

        private void DrawLocalizationDiagnostics(SerializedProperty config)
        {
            var tableId = config.FindPropertyRelative("m_TableId").stringValue;
            if (string.IsNullOrWhiteSpace(tableId) || m_SourceSnapshot == null)
            {
                EditorGUILayout.HelpBox("选择一张 Luban 表后可校验本地化字段契约。", MessageType.Info);
                return;
            }

            try
            {
                var draft = CreateLocalizationDraft(config);
                var result = LocalizationTableContractValidator.Validate(
                    m_SourceSnapshot,
                    LubanSourceCatalog.Shared,
                    draft);
                if (result.IsValid)
                {
                    EditorGUILayout.HelpBox(
                        $"契约有效：{result.Data.Rows.Count} 条文本，{draft.LocaleFields.Count} 个语言字段。",
                        MessageType.Info);
                    return;
                }

                EditorGUILayout.HelpBox(
                    string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)),
                    MessageType.Error);
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox($"本地化契约校验失败：{exception.Message}", MessageType.Error);
            }
        }

        private static LocalizationProjectConfig CreateLocalizationDraft(SerializedProperty config)
        {
            var draft = new LocalizationProjectConfig
            {
                TableId = config.FindPropertyRelative("m_TableId").stringValue,
                KeyField = config.FindPropertyRelative("m_KeyField").stringValue,
                PreviewLocale = config.FindPropertyRelative("m_PreviewLocale").stringValue
            };
            draft.EnsureDefaults();
            var mappings = config.FindPropertyRelative("m_LocaleFields");
            for (var i = 0; i < mappings.arraySize; i++)
            {
                var mapping = mappings.GetArrayElementAtIndex(i);
                draft.LocaleFields.Add(new LocalizationLocaleField
                {
                    Locale = mapping.FindPropertyRelative("m_Locale").stringValue,
                    FieldName = mapping.FindPropertyRelative("m_FieldName").stringValue
                });
            }

            return draft;
        }

        private void RefreshSourceCatalog()
        {
            try
            {
                m_SourceSnapshot = LubanSourceCatalog.Shared.Refresh(EditorGlobalConfig.LoadOrCreate().Luban);
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

        private static string DrawStringPopup(
            string label,
            string current,
            IEnumerable<string> values,
            params GUILayoutOption[] options)
        {
            return DrawStringPopup(new GUIContent(label), current, values, options);
        }

        private static string DrawStringPopup(
            GUIContent label,
            string current,
            IEnumerable<string> values,
            params GUILayoutOption[] options)
        {
            var items = new List<string> { string.Empty };
            items.AddRange(values
                .Where(value => string.IsNullOrWhiteSpace(value) is false)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal));
            current = current?.Trim() ?? string.Empty;
            if (current.Length > 0 && items.Contains(current) is false)
            {
                items.Add(current);
            }

            var display = items.Select(value => value.Length == 0 ? "(未选择)" : value).ToArray();
            var index = Math.Max(0, items.IndexOf(current));
            var selected = EditorGUILayout.Popup(label, index, display, options);
            return selected >= 0 && selected < items.Count ? items[selected] : current;
        }
    }
}
