using System;
using System.IO;
using System.Linq;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.Localization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.LocalizationEditor
{
    internal sealed class LocalizationAssetWorkbench : VisualElement
    {
        private const string EmptyChoice = "(无)";

        private readonly ILocalizationAuthoringService m_Service;
        private readonly ILocalizationImportService m_ImportService;
        private readonly Action<string> m_ErrorChanged;
        private string m_SearchQuery = string.Empty;
        private string m_NewCatalogName = "LocalizationCatalog";
        private string m_NewCatalogLocale = "zh-CN";
        private string m_NewLocale = string.Empty;
        private string m_NewLocaleFallback = string.Empty;
        private string m_NewKey = string.Empty;
        private string m_NewText = string.Empty;

        public LocalizationAssetWorkbench(
            ILocalizationAuthoringService service = null,
            Action<string> errorChanged = null,
            ILocalizationImportService importService = null)
        {
            m_Service = service ?? LocalizationAuthoringService.Shared;
            m_ImportService = importService ?? LocalizationImportService.Shared;
            m_ErrorChanged = errorChanged;
            name = "localization-asset-workbench";
            style.flexGrow = 1;
            style.minWidth = 0;
            Rebuild();
        }

        public void Rebuild()
        {
            Clear();
            var snapshot = m_Service.Refresh();
            AddCatalogSection(snapshot);
            if (snapshot.Catalog == null || snapshot.IsValid is false)
            {
                AddDiagnostics(snapshot);
                return;
            }

            AddLocaleSection(snapshot);
            AddKeySection(snapshot);
            Add(new LocalizationImportWorkbench(
                m_Service,
                m_ImportService,
                m_ErrorChanged,
                () => schedule.Execute(Rebuild)));
            AddDiagnostics(snapshot);
        }

        private void AddCatalogSection(LocalizationAuthoringSnapshot snapshot)
        {
            var toolbar = new VisualElement { name = "localization-catalog-toolbar" };
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.FlexEnd;
            toolbar.style.marginBottom = 12;

            var catalogField = new ObjectField("Catalog")
            {
                name = "localization-catalog-field",
                objectType = typeof(LocalizationCatalogAsset),
                allowSceneObjects = false,
                value = snapshot.Catalog
            };
            catalogField.style.flexGrow = 1;
            catalogField.style.minWidth = 260;
            catalogField.labelElement.style.width = 110;
            catalogField.RegisterValueChangedCallback(evt =>
            {
                var result = m_Service.BindCatalog(evt.newValue as LocalizationCatalogAsset);
                ApplyResult(result);
            });
            toolbar.Add(catalogField);

            var refresh = new Button(Rebuild)
            {
                name = "localization-refresh-button",
                text = "刷新",
                tooltip = "重新读取 Catalog 与所有语言资产"
            };
            refresh.style.marginLeft = 8;
            toolbar.Add(refresh);
            Add(toolbar);

            var createRow = new VisualElement { name = "localization-create-row" };
            createRow.style.flexDirection = FlexDirection.Row;
            createRow.style.alignItems = Align.FlexEnd;
            createRow.style.marginBottom = 14;
            var nameField = CreateTextField("localization-catalog-name", "新 Catalog 名称", m_NewCatalogName);
            nameField.RegisterValueChangedCallback(evt => m_NewCatalogName = evt.newValue);
            createRow.Add(nameField);
            var localeField = CreateTextField("localization-catalog-locale", "初始语言", m_NewCatalogLocale, 180);
            localeField.RegisterValueChangedCallback(evt => m_NewCatalogLocale = evt.newValue);
            createRow.Add(localeField);
            var createButton = new Button(CreateCatalog)
            {
                name = "localization-create-button",
                text = "创建到文件夹"
            };
            createButton.style.marginLeft = 8;
            createButton.style.height = 22;
            createRow.Add(createButton);
            Add(createRow);

            if (snapshot.Catalog == null)
            {
                return;
            }

            var identity = new Label($"Catalog ID  {snapshot.Catalog.CatalogId}\n资产路径  {snapshot.CatalogPath}")
            {
                name = "localization-catalog-identity"
            };
            identity.style.whiteSpace = WhiteSpace.Normal;
            identity.style.color = SecondaryTextColor();
            identity.style.marginBottom = 12;
            Add(identity);
        }

        private void AddLocaleSection(LocalizationAuthoringSnapshot snapshot)
        {
            Add(CreateSectionHeader("语言与资源"));
            var localeNames = snapshot.Catalog.Locales
                .Where(locale => locale != null)
                .Select(locale => locale.Locale)
                .OrderBy(locale => locale, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var settingsRow = new VisualElement();
            settingsRow.style.flexDirection = FlexDirection.Row;
            settingsRow.style.marginBottom = 10;

            var defaultLocale = CreateDropdown(
                "localization-default-locale",
                "默认语言",
                snapshot.Catalog.DefaultLocale,
                localeNames);
            defaultLocale.RegisterValueChangedCallback(evt =>
                ApplyResult(m_Service.SetDefaultLocale(evt.newValue)));
            settingsRow.Add(defaultLocale);

            var previewLocale = CreateDropdown(
                "localization-preview-locale",
                "预览语言",
                snapshot.PreviewLocale,
                localeNames);
            previewLocale.RegisterValueChangedCallback(evt =>
            {
                var config = EditorGlobalConfig.LoadOrCreate();
                var previous = config.Localization.PreviewLocale;
                config.Localization.PreviewLocale = evt.newValue;
                try
                {
                    config.Save();
                    m_ErrorChanged?.Invoke(null);
                    schedule.Execute(Rebuild);
                }
                catch (Exception exception)
                {
                    config.Localization.PreviewLocale = previous;
                    m_ErrorChanged?.Invoke($"保存预览语言失败：{exception.Message}");
                }
            });
            settingsRow.Add(previewLocale);
            Add(settingsRow);

            var localeTable = new VisualElement { name = "localization-locale-list" };
            localeTable.style.marginBottom = 12;
            foreach (var locale in snapshot.Locales.Values.OrderBy(
                         item => item.Descriptor.Locale,
                         StringComparer.OrdinalIgnoreCase))
            {
                localeTable.Add(CreateLocaleRow(snapshot, locale, localeNames));
            }

            Add(localeTable);

            var addRow = new VisualElement { name = "localization-add-locale-row" };
            addRow.style.flexDirection = FlexDirection.Row;
            addRow.style.alignItems = Align.FlexEnd;
            var localeInput = CreateTextField("localization-new-locale", "新增语言", m_NewLocale, 180);
            localeInput.RegisterValueChangedCallback(evt => m_NewLocale = evt.newValue);
            addRow.Add(localeInput);
            var fallback = CreateDropdown(
                "localization-new-locale-fallback",
                "回退语言",
                m_NewLocaleFallback,
                localeNames,
                true);
            fallback.RegisterValueChangedCallback(evt =>
                m_NewLocaleFallback = evt.newValue == EmptyChoice ? string.Empty : evt.newValue);
            addRow.Add(fallback);
            var addButton = new Button(() => AddLocale(snapshot))
            {
                name = "localization-add-locale-button",
                text = "添加语言"
            };
            addButton.style.marginLeft = 8;
            addButton.style.height = 22;
            addRow.Add(addButton);
            Add(addRow);
        }

        private VisualElement CreateLocaleRow(
            LocalizationAuthoringSnapshot snapshot,
            LocalizationAuthoringLocale locale,
            string[] localeNames)
        {
            var row = new VisualElement { name = $"localization-locale-{locale.Descriptor.Locale}" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexEnd;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = DividerColor();

            var localeLabel = new Label(locale.Descriptor.Locale);
            localeLabel.style.width = 90;
            localeLabel.style.minWidth = 90;
            localeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            localeLabel.style.marginBottom = 4;
            row.Add(localeLabel);

            var locationField = CreateTextField(
                $"localization-location-{locale.Descriptor.Locale}",
                "Resource location",
                locale.Descriptor.ResourceLocation);
            row.Add(locationField);

            var fallback = CreateDropdown(
                $"localization-fallback-{locale.Descriptor.Locale}",
                "回退",
                locale.Descriptor.FallbackLocale,
                localeNames.Where(name =>
                    string.Equals(name, locale.Descriptor.Locale, StringComparison.OrdinalIgnoreCase) is false),
                true);
            fallback.style.maxWidth = 210;
            row.Add(fallback);

            var apply = new Button(() => ApplyResult(m_Service.SetLocaleDescriptor(
                locale.Descriptor.Locale,
                locationField.value,
                fallback.value == EmptyChoice ? string.Empty : fallback.value)))
            {
                text = "应用",
                tooltip = "保存资源位置和回退语言"
            };
            apply.style.marginLeft = 6;
            apply.style.height = 22;
            row.Add(apply);

            var remove = new Button(() => RemoveLocale(snapshot, locale))
            {
                text = "移除",
                tooltip = "从 Catalog 移除语言，保留对应文本资产"
            };
            remove.SetEnabled(string.Equals(
                snapshot.Catalog.DefaultLocale,
                locale.Descriptor.Locale,
                StringComparison.OrdinalIgnoreCase) is false);
            remove.style.marginLeft = 4;
            remove.style.height = 22;
            row.Add(remove);
            return row;
        }

        private void AddKeySection(LocalizationAuthoringSnapshot snapshot)
        {
            Add(CreateSectionHeader("Key 与预览文本"));
            var search = new ToolbarSearchField
            {
                name = "localization-key-search",
                value = m_SearchQuery
            };
            search.style.width = Length.Percent(100);
            search.style.marginBottom = 10;
            search.RegisterValueChangedCallback(evt =>
            {
                m_SearchQuery = evt.newValue ?? string.Empty;
                schedule.Execute(Rebuild);
            });
            Add(search);

            var addRow = new VisualElement { name = "localization-add-key-row" };
            addRow.style.flexDirection = FlexDirection.Row;
            addRow.style.alignItems = Align.FlexEnd;
            addRow.style.marginBottom = 10;
            var keyInput = CreateTextField("localization-key-input", "新 Key", m_NewKey);
            keyInput.RegisterValueChangedCallback(evt => m_NewKey = evt.newValue);
            addRow.Add(keyInput);
            var textInput = CreateTextField("localization-text-input", "预览文本", m_NewText);
            textInput.RegisterValueChangedCallback(evt => m_NewText = evt.newValue);
            addRow.Add(textInput);
            var addButton = new Button(() =>
            {
                var result = m_Service.CreateKey(m_NewKey, snapshot.PreviewLocale, m_NewText);
                if (result.Succeeded)
                {
                    m_NewKey = string.Empty;
                    m_NewText = string.Empty;
                }

                ApplyResult(result);
            })
            {
                name = "localization-add-key-button",
                text = "新增 Key"
            };
            addButton.style.marginLeft = 8;
            addButton.style.height = 22;
            addRow.Add(addButton);
            Add(addRow);

            var list = new ScrollView(ScrollViewMode.Vertical) { name = "localization-key-list" };
            list.style.maxHeight = 460;
            list.style.minHeight = 160;
            list.style.borderTopWidth = 1;
            list.style.borderBottomWidth = 1;
            list.style.borderTopColor = DividerColor();
            list.style.borderBottomColor = DividerColor();
            var query = m_SearchQuery.Trim();
            foreach (var entry in snapshot.Entries.Where(entry => Matches(snapshot, entry, query)).Take(300))
            {
                list.Add(CreateKeyRow(snapshot, entry));
            }

            Add(list);
            var count = new Label($"显示 {list.childCount} / {snapshot.Entries.Count} 个 Key")
            {
                name = "localization-key-count"
            };
            count.style.color = SecondaryTextColor();
            count.style.marginTop = 6;
            Add(count);
        }

        private VisualElement CreateKeyRow(
            LocalizationAuthoringSnapshot snapshot,
            LocalizationAuthoringEntry entry)
        {
            var row = new VisualElement { name = $"localization-key-{entry.KeyId}" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 5;
            row.style.paddingBottom = 5;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = DividerColor();

            var keyField = new TextField
            {
                value = entry.Key,
                isDelayed = true,
                tooltip = $"KeyId: {entry.KeyId}"
            };
            keyField.style.flexGrow = 1;
            keyField.style.minWidth = 180;
            keyField.RegisterValueChangedCallback(evt =>
                ApplyResult(m_Service.RenameKey(entry.KeyId, evt.newValue)));
            row.Add(keyField);

            var hasText = snapshot.TryGetText(entry.KeyId, snapshot.PreviewLocale, out var text);
            var textField = new TextField
            {
                value = text ?? string.Empty,
                isDelayed = true,
                tooltip = hasText ? "已配置预览语言文本" : "当前预览语言缺少翻译"
            };
            textField.style.flexGrow = 1;
            textField.style.minWidth = 220;
            textField.style.marginLeft = 8;
            textField.RegisterValueChangedCallback(evt =>
                ApplyResult(m_Service.SetText(entry.KeyId, snapshot.PreviewLocale, evt.newValue)));
            row.Add(textField);

            var state = new Label(hasText ? "已翻译" : "缺翻译");
            state.style.width = 58;
            state.style.marginLeft = 8;
            state.style.color = hasText ? new Color(0.35f, 0.75f, 0.42f) : new Color(0.95f, 0.55f, 0.2f);
            row.Add(state);

            var clear = new Button(() => ApplyResult(m_Service.RemoveText(entry.KeyId, snapshot.PreviewLocale)))
            {
                text = "清除",
                tooltip = "删除当前语言的文本条目，使其恢复为缺翻译"
            };
            clear.SetEnabled(hasText);
            clear.style.marginLeft = 4;
            row.Add(clear);

            var remove = new Button(() => RemoveKey(entry))
            {
                text = "删除",
                tooltip = "删除共享 Key 和所有语言文本；会先扫描使用位置"
            };
            remove.style.marginLeft = 4;
            row.Add(remove);
            return row;
        }

        private void AddDiagnostics(LocalizationAuthoringSnapshot snapshot)
        {
            var diagnostics = new VisualElement { name = "localization-diagnostics" };
            diagnostics.style.marginTop = 12;
            foreach (var diagnostic in snapshot.Diagnostics)
            {
                var label = new Label(diagnostic.Message);
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.marginBottom = 4;
                label.style.color = diagnostic.Severity == LocalizationAuthoringDiagnosticSeverity.Error
                    ? new Color(0.95f, 0.35f, 0.3f)
                    : new Color(0.95f, 0.65f, 0.25f);
                diagnostics.Add(label);
            }

            if (diagnostics.childCount > 0)
            {
                Add(diagnostics);
            }
        }

        private void CreateCatalog()
        {
            var selected = EditorUtility.OpenFolderPanel("选择本地化资产保存目录", Application.dataPath, string.Empty);
            if (TryToAssetFolder(selected, out var folder) is false)
            {
                if (string.IsNullOrWhiteSpace(selected) is false)
                {
                    m_ErrorChanged?.Invoke("本地化资产只能创建在当前项目的 Assets 目录中。");
                }

                return;
            }

            ApplyResult(m_Service.CreateCatalog(folder, m_NewCatalogName, m_NewCatalogLocale));
        }

        private void AddLocale(LocalizationAuthoringSnapshot snapshot)
        {
            var locale = LocalizationAuthoringService.NormalizeLocale(m_NewLocale);
            if (locale.Length == 0)
            {
                m_ErrorChanged?.Invoke("新增语言不能为空。");
                return;
            }

            var folder = Path.GetDirectoryName(snapshot.CatalogPath)?.Replace('\\', '/') ?? "Assets";
            var stem = Path.GetFileNameWithoutExtension(snapshot.CatalogPath);
            var assetPath = $"{folder}/{stem}.{locale}.asset";
            var result = m_Service.AddLocale(new LocalizationLocaleDraft(
                locale,
                assetPath,
                assetPath,
                m_NewLocaleFallback));
            if (result.Succeeded)
            {
                m_NewLocale = string.Empty;
                m_NewLocaleFallback = string.Empty;
            }

            ApplyResult(result);
        }

        private void RemoveLocale(LocalizationAuthoringSnapshot snapshot, LocalizationAuthoringLocale locale)
        {
            if (EditorUtility.DisplayDialog(
                    "移除语言",
                    $"从 Catalog 移除 {locale.Descriptor.Locale}？\n\n文本资产会保留：\n{locale.AssetPath}",
                    "移除",
                    "取消") is false)
            {
                return;
            }

            ApplyResult(m_Service.RemoveLocale(locale.Descriptor.Locale));
        }

        private void RemoveKey(LocalizationAuthoringEntry entry)
        {
            var usages = m_Service.FindKeyUsages(entry.Key);
            var usageText = usages.Count == 0
                ? "未扫描到 Story/UI 序列化资产引用。"
                : "可能的使用位置：\n" + string.Join("\n", usages.Take(12)) +
                  (usages.Count > 12 ? $"\n...另有 {usages.Count - 12} 项" : string.Empty);
            if (EditorUtility.DisplayDialog(
                    "删除共享本地化 Key",
                    $"删除 {entry.Key} 会同时删除所有语言文本。\n\n{usageText}",
                    "确认删除",
                    "取消") is false)
            {
                return;
            }

            ApplyResult(m_Service.RemoveKey(entry.KeyId));
        }

        private void ApplyResult(LocalizationMutationResult result)
        {
            if (result == null)
            {
                return;
            }

            m_ErrorChanged?.Invoke(result.Succeeded ? null : result.Message);
            schedule.Execute(Rebuild);
        }

        private static bool Matches(
            LocalizationAuthoringSnapshot snapshot,
            LocalizationAuthoringEntry entry,
            string query)
        {
            if (query.Length == 0 || entry.Key.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return snapshot.TryGetText(entry.KeyId, snapshot.PreviewLocale, out var text) &&
                   (text ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static TextField CreateTextField(string name, string label, string value, float maxWidth = 0)
        {
            var field = new TextField(label)
            {
                name = name,
                value = value ?? string.Empty,
                isDelayed = true
            };
            field.style.flexGrow = 1;
            field.style.minWidth = 140;
            field.style.marginRight = 8;
            field.labelElement.style.width = 110;
            if (maxWidth > 0)
            {
                field.style.maxWidth = maxWidth;
            }

            return field;
        }

        private static DropdownField CreateDropdown(
            string name,
            string label,
            string current,
            System.Collections.Generic.IEnumerable<string> choices,
            bool allowEmpty = false)
        {
            var values = choices
                .Where(value => string.IsNullOrWhiteSpace(value) is false)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (allowEmpty)
            {
                values.Insert(0, EmptyChoice);
            }

            current = current?.Trim() ?? string.Empty;
            var selected = current.Length == 0 && allowEmpty ? EmptyChoice : current;
            if (selected.Length > 0 && values.Contains(selected, StringComparer.OrdinalIgnoreCase) is false)
            {
                values.Add(selected);
            }

            var index = Math.Max(0, values.FindIndex(value =>
                string.Equals(value, selected, StringComparison.OrdinalIgnoreCase)));
            var field = new DropdownField(label, values, index) { name = name };
            field.style.flexGrow = 1;
            field.style.minWidth = 170;
            field.style.marginRight = 8;
            field.labelElement.style.width = 90;
            return field;
        }

        private static Label CreateSectionHeader(string text)
        {
            var header = new Label(text);
            header.style.fontSize = 13;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 12;
            header.style.marginBottom = 10;
            header.style.paddingBottom = 5;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = DividerColor();
            return header;
        }

        private static bool TryToAssetFolder(string absolutePath, out string assetPath)
        {
            assetPath = string.Empty;
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return false;
            }

            var assetsRoot = Path.GetFullPath(Application.dataPath).Replace('\\', '/').TrimEnd('/');
            var selected = Path.GetFullPath(absolutePath).Replace('\\', '/').TrimEnd('/');
            if (string.Equals(selected, assetsRoot, StringComparison.OrdinalIgnoreCase))
            {
                assetPath = "Assets";
                return true;
            }

            if (selected.StartsWith(assetsRoot + "/", StringComparison.OrdinalIgnoreCase) is false)
            {
                return false;
            }

            assetPath = "Assets" + selected.Substring(assetsRoot.Length);
            return true;
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
