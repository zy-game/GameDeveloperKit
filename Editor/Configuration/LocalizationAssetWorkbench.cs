using System;
using System.Collections.Generic;
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
        private const float KeyColumnWidth = 260;
        private const float LocaleColumnWidth = 240;

        private readonly ILocalizationAuthoringService m_Service;
        private readonly ILocalizationImportService m_ImportService;
        private readonly Action<string> m_ErrorChanged;
        private readonly Dictionary<long, VisualElement> m_Rows = new Dictionary<long, VisualElement>();

        private string m_SearchQuery = string.Empty;
        private string m_NewKey = string.Empty;
        private string m_NewLocale = string.Empty;
        private string m_NewLocaleFallback = string.Empty;
        private long m_SelectedKeyId;
        private bool m_IsAddingKey;
        private bool m_IsAddingLocale;
        private bool m_ShowImport;
        private Button m_DeleteKeyButton;

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
            style.minHeight = 0;
            style.minWidth = 0;
            Rebuild();
        }

        public void SetSearchQuery(string query)
        {
            query = query ?? string.Empty;
            if (string.Equals(m_SearchQuery, query, StringComparison.Ordinal))
            {
                return;
            }

            m_SearchQuery = query;
            Rebuild();
        }

        public void Rebuild()
        {
            Clear();
            m_Rows.Clear();
            var snapshot = m_Service.Refresh();
            var locales = GetLocaleNames(snapshot);
            AddToolbar(snapshot, locales);

            if (snapshot.Catalog == null || snapshot.IsValid is false)
            {
                AddUnavailableState();
                AddDiagnostics(snapshot);
                return;
            }

            if (m_ShowImport)
            {
                AddImportWorkbench();
                AddDiagnostics(snapshot);
                return;
            }

            if (m_IsAddingLocale)
            {
                AddLocaleCreator(snapshot, locales);
            }

            AddLocalizationTable(snapshot, locales);
            AddDiagnostics(snapshot);
        }

        private void AddToolbar(LocalizationAuthoringSnapshot snapshot, IReadOnlyList<string> locales)
        {
            var toolbar = new Toolbar { name = "localization-toolbar" };
            toolbar.style.flexShrink = 0;

            var addKey = new ToolbarButton(() =>
            {
                m_IsAddingKey = !m_IsAddingKey;
                m_NewKey = string.Empty;
                Rebuild();
            })
            {
                name = "localization-add-key-button",
                text = m_IsAddingKey ? "取消新增" : "新增 Key"
            };
            addKey.SetEnabled(snapshot.IsValid && m_ShowImport is false);
            toolbar.Add(addKey);

            m_DeleteKeyButton = new ToolbarButton(() => RemoveSelectedKey(snapshot))
            {
                name = "localization-delete-key-button",
                text = "删除 Key"
            };
            m_DeleteKeyButton.SetEnabled(
                snapshot.IsValid &&
                m_ShowImport is false &&
                snapshot.Entries.Any(entry => entry.KeyId == m_SelectedKeyId));
            toolbar.Add(m_DeleteKeyButton);

            var languageMenu = new ToolbarMenu
            {
                name = "localization-language-menu",
                text = "语言管理"
            };
            languageMenu.menu.AppendAction("新增语言...", _ =>
            {
                m_IsAddingLocale = true;
                m_ShowImport = false;
                Rebuild();
            });
            if (snapshot.Catalog != null)
            {
                languageMenu.menu.AppendSeparator();
                foreach (var locale in locales)
                {
                    var currentLocale = locale;
                    languageMenu.menu.AppendAction(
                        $"设为默认/{currentLocale}",
                        _ => ApplyResult(m_Service.SetDefaultLocale(currentLocale)),
                        _ => string.Equals(
                            snapshot.Catalog.DefaultLocale,
                            currentLocale,
                            StringComparison.OrdinalIgnoreCase)
                            ? DropdownMenuAction.Status.Disabled
                            : DropdownMenuAction.Status.Normal);
                }

                languageMenu.menu.AppendSeparator();
                foreach (var locale in locales)
                {
                    var currentLocale = locale;
                    languageMenu.menu.AppendAction(
                        $"移除/{currentLocale}",
                        _ => RemoveLocale(snapshot, currentLocale),
                        _ => string.Equals(
                            snapshot.Catalog.DefaultLocale,
                            currentLocale,
                            StringComparison.OrdinalIgnoreCase)
                            ? DropdownMenuAction.Status.Disabled
                            : DropdownMenuAction.Status.Normal);
                }
            }

            languageMenu.SetEnabled(snapshot.IsValid && m_ShowImport is false);
            toolbar.Add(languageMenu);

            if (locales.Count > 0)
            {
                var previewLabel = new Label("预览");
                previewLabel.style.marginLeft = 8;
                toolbar.Add(previewLabel);
                var previewLocale = new DropdownField(
                    locales.ToList(),
                    Math.Max(0, locales.ToList().FindIndex(locale => string.Equals(
                        locale,
                        snapshot.PreviewLocale,
                        StringComparison.OrdinalIgnoreCase))))
                {
                    name = "localization-preview-locale"
                };
                previewLocale.style.width = 120;
                previewLocale.RegisterValueChangedCallback(evt => SetPreviewLocale(evt.newValue));
                previewLocale.SetEnabled(snapshot.IsValid && m_ShowImport is false);
                toolbar.Add(previewLocale);
            }

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            toolbar.Add(spacer);

            var catalogMenu = CreateCatalogMenu(snapshot);
            catalogMenu.style.marginRight = 8;
            toolbar.Add(catalogMenu);

            var import = new ToolbarButton(() =>
            {
                m_ShowImport = !m_ShowImport;
                m_IsAddingKey = false;
                m_IsAddingLocale = false;
                Rebuild();
            })
            {
                name = "localization-import-button",
                text = m_ShowImport ? "返回词条" : "导入配置表"
            };
            import.SetEnabled(snapshot.IsValid);
            toolbar.Add(import);
            Add(toolbar);
        }

        private ToolbarMenu CreateCatalogMenu(LocalizationAuthoringSnapshot snapshot)
        {
            var menu = new ToolbarMenu
            {
                name = "localization-catalog-menu",
                text = snapshot.Catalog == null
                    ? "绑定 Catalog"
                    : Path.GetFileNameWithoutExtension(snapshot.CatalogPath),
                tooltip = snapshot.Catalog == null
                    ? "选择项目内已有的本地化 Catalog，或新建一个"
                    : snapshot.CatalogPath
            };
            var catalogs = AssetDatabase.FindAssets("t:LocalizationCatalogAsset")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(path => string.IsNullOrWhiteSpace(path) is false)
                .Select(path => new
                {
                    Path = path,
                    Asset = AssetDatabase.LoadAssetAtPath<LocalizationCatalogAsset>(path)
                })
                .Where(item => item.Asset != null)
                .OrderBy(item => item.Path, StringComparer.Ordinal)
                .ToArray();
            if (catalogs.Length == 0)
            {
                menu.menu.AppendAction(
                    "绑定/未找到 Catalog 资产",
                    _ => { },
                    DropdownMenuAction.Status.Disabled);
            }
            else
            {
                foreach (var item in catalogs)
                {
                    var catalog = item.Asset;
                    var path = item.Path;
                    menu.menu.AppendAction(
                        $"绑定/{Path.GetFileNameWithoutExtension(path)}  ({path})",
                        _ => ApplyResult(m_Service.BindCatalog(catalog)),
                        _ => ReferenceEquals(snapshot.Catalog, catalog)
                            ? DropdownMenuAction.Status.Disabled
                            : DropdownMenuAction.Status.Normal);
                }
            }

            menu.menu.AppendSeparator();
            menu.menu.AppendAction("新建 Catalog...", _ => CreateCatalog());
            if (snapshot.Catalog != null)
            {
                menu.menu.AppendAction("在 Project 中定位", _ =>
                {
                    Selection.activeObject = snapshot.Catalog;
                    EditorGUIUtility.PingObject(snapshot.Catalog);
                });
            }

            return menu;
        }

        private void CreateCatalog()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "新建本地化 Catalog",
                "LocalizationCatalog",
                "asset",
                "选择 Catalog 与语言资产的保存位置");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var folder = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
            var catalogName = Path.GetFileNameWithoutExtension(path);
            var initialLocale = LocalizationAuthoringService.NormalizeLocale(
                EditorGlobalConfig.LoadOrCreate().Localization.PreviewLocale);
            if (initialLocale.Length == 0)
            {
                initialLocale = "zh-CN";
            }

            ApplyResult(m_Service.CreateCatalog(folder, catalogName, initialLocale));
        }

        private void AddLocaleCreator(LocalizationAuthoringSnapshot snapshot, IReadOnlyList<string> locales)
        {
            var row = new VisualElement { name = "localization-add-locale-row" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = DividerColor();

            var locale = new TextField("语言")
            {
                name = "localization-new-locale",
                value = m_NewLocale,
                isDelayed = false
            };
            locale.style.width = 220;
            locale.RegisterValueChangedCallback(evt => m_NewLocale = evt.newValue ?? string.Empty);
            row.Add(locale);

            var fallbackChoices = new List<string> { EmptyChoice };
            fallbackChoices.AddRange(locales);
            var fallbackIndex = Math.Max(0, fallbackChoices.FindIndex(value => string.Equals(
                value,
                m_NewLocaleFallback,
                StringComparison.OrdinalIgnoreCase)));
            var fallback = new DropdownField("回退语言", fallbackChoices, fallbackIndex)
            {
                name = "localization-new-locale-fallback"
            };
            fallback.style.width = 220;
            fallback.style.marginLeft = 8;
            fallback.RegisterValueChangedCallback(evt =>
                m_NewLocaleFallback = evt.newValue == EmptyChoice ? string.Empty : evt.newValue);
            row.Add(fallback);

            var confirm = new Button(() => AddLocale(snapshot)) { text = "添加" };
            confirm.style.marginLeft = 8;
            row.Add(confirm);
            var cancel = new Button(() =>
            {
                m_IsAddingLocale = false;
                m_NewLocale = string.Empty;
                m_NewLocaleFallback = string.Empty;
                Rebuild();
            }) { text = "取消" };
            cancel.style.marginLeft = 4;
            row.Add(cancel);
            Add(row);
            schedule.Execute(() => locale.Focus());
        }

        private void AddLocalizationTable(LocalizationAuthoringSnapshot snapshot, IReadOnlyList<string> locales)
        {
            var query = m_SearchQuery.Trim();
            var visibleEntries = snapshot.Entries
                .Where(entry => Matches(snapshot, locales, entry, query))
                .ToArray();

            var host = new VisualElement { name = "localization-table" };
            host.style.flexGrow = 1;
            host.style.minHeight = 0;
            host.style.minWidth = 0;
            host.style.marginLeft = 8;
            host.style.marginRight = 8;
            host.style.marginTop = 8;
            host.style.marginBottom = 6;
            host.style.borderLeftWidth = 1;
            host.style.borderRightWidth = 1;
            host.style.borderTopWidth = 1;
            host.style.borderBottomWidth = 1;
            host.style.borderLeftColor = DividerColor();
            host.style.borderRightColor = DividerColor();
            host.style.borderTopColor = DividerColor();
            host.style.borderBottomColor = DividerColor();

            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal)
            {
                name = "localization-key-list"
            };
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            var table = new VisualElement();
            table.style.minWidth = KeyColumnWidth + LocaleColumnWidth * locales.Count;
            table.Add(CreateTableHeader(snapshot, locales));
            if (m_IsAddingKey)
            {
                table.Add(CreateNewKeyRow(locales));
            }

            for (var index = 0; index < visibleEntries.Length; index++)
            {
                table.Add(CreateKeyRow(snapshot, locales, visibleEntries[index], index));
            }

            if (visibleEntries.Length == 0 && m_IsAddingKey is false)
            {
                var empty = new Label(query.Length == 0 ? "当前 Catalog 没有本地化 Key。" : "没有匹配的本地化内容。");
                empty.name = "localization-empty-state";
                empty.style.paddingLeft = 12;
                empty.style.paddingTop = 18;
                empty.style.paddingBottom = 18;
                empty.style.color = SecondaryTextColor();
                table.Add(empty);
            }

            scroll.Add(table);
            host.Add(scroll);
            Add(host);

            var count = new Label($"显示 {visibleEntries.Length} / {snapshot.Entries.Count} 个 Key · {locales.Count} 种语言")
            {
                name = "localization-key-count"
            };
            count.style.color = SecondaryTextColor();
            count.style.marginLeft = 10;
            count.style.marginBottom = 4;
            Add(count);
        }

        private VisualElement CreateTableHeader(
            LocalizationAuthoringSnapshot snapshot,
            IReadOnlyList<string> locales)
        {
            var header = new VisualElement { name = "localization-table-header" };
            header.style.flexDirection = FlexDirection.Row;
            header.style.flexShrink = 0;
            header.style.minHeight = 30;
            header.style.backgroundColor = HeaderBackgroundColor();
            header.Add(CreateHeaderCell("Key", "localization-key-column", KeyColumnWidth));
            foreach (var locale in locales)
            {
                var suffix = string.Empty;
                if (string.Equals(locale, snapshot.Catalog.DefaultLocale, StringComparison.OrdinalIgnoreCase))
                {
                    suffix += "  默认";
                }

                if (string.Equals(locale, snapshot.PreviewLocale, StringComparison.OrdinalIgnoreCase))
                {
                    suffix += "  预览";
                }

                header.Add(CreateHeaderCell(
                    locale + suffix,
                    $"localization-locale-column-{SafeName(locale)}",
                    LocaleColumnWidth));
            }

            return header;
        }

        private VisualElement CreateNewKeyRow(IReadOnlyList<string> locales)
        {
            var row = CreateRowContainer("localization-new-key-row", -1);
            var keyCell = CreateCell(KeyColumnWidth);
            keyCell.style.flexDirection = FlexDirection.Row;
            var field = new TextField
            {
                name = "localization-new-key-editor",
                value = m_NewKey,
                isDelayed = false
            };
            field.style.flexGrow = 1;
            field.style.minWidth = 0;
            field.RegisterValueChangedCallback(evt => m_NewKey = evt.newValue ?? string.Empty);
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    CommitNewKey();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    m_IsAddingKey = false;
                    m_NewKey = string.Empty;
                    Rebuild();
                    evt.StopPropagation();
                }
            });
            keyCell.Add(field);
            var confirm = new Button(CommitNewKey) { text = "添加" };
            confirm.style.marginLeft = 4;
            keyCell.Add(confirm);
            row.Add(keyCell);
            foreach (var unused in locales)
            {
                var localeCell = CreateCell(LocaleColumnWidth);
                var hint = new Label("创建后双击填写");
                hint.style.color = SecondaryTextColor();
                localeCell.Add(hint);
                row.Add(localeCell);
            }

            schedule.Execute(() =>
            {
                field.Focus();
                field.SelectAll();
            });
            return row;
        }

        private VisualElement CreateKeyRow(
            LocalizationAuthoringSnapshot snapshot,
            IReadOnlyList<string> locales,
            LocalizationAuthoringEntry entry,
            int index)
        {
            var row = CreateRowContainer($"localization-key-{entry.KeyId}", index);
            row.userData = index;
            row.style.backgroundColor = RowBackgroundColor(index, entry.KeyId == m_SelectedKeyId);
            m_Rows[entry.KeyId] = row;
            row.Add(CreateKeyCell(entry));
            foreach (var locale in locales)
            {
                row.Add(CreateTextCell(snapshot, entry, locale));
            }

            return row;
        }

        private VisualElement CreateKeyCell(LocalizationAuthoringEntry entry)
        {
            var cell = CreateCell(KeyColumnWidth);
            cell.name = $"localization-key-cell-{entry.KeyId}";
            var label = CreateCellLabel(entry.Key, $"KeyId: {entry.KeyId}", false);
            label.name = $"localization-key-label-{entry.KeyId}";
            cell.Add(label);
            cell.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                SelectKey(entry.KeyId);
                if (evt.clickCount == 2)
                {
                    BeginCellEdit(
                        cell,
                        entry.Key,
                        $"localization-key-editor-{entry.KeyId}",
                        value => m_Service.RenameKey(entry.KeyId, value));
                    evt.StopPropagation();
                }
            });
            cell.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("编辑 Key", _ => BeginCellEdit(
                    cell,
                    entry.Key,
                    $"localization-key-editor-{entry.KeyId}",
                    value => m_Service.RenameKey(entry.KeyId, value)));
                evt.menu.AppendAction("删除 Key", _ => RemoveKey(entry));
            }));
            return cell;
        }

        private VisualElement CreateTextCell(
            LocalizationAuthoringSnapshot snapshot,
            LocalizationAuthoringEntry entry,
            string locale)
        {
            var cell = CreateCell(LocaleColumnWidth);
            cell.name = $"localization-text-cell-{entry.KeyId}-{SafeName(locale)}";
            var hasText = snapshot.TryGetText(entry.KeyId, locale, out var text);
            var display = hasText
                ? string.IsNullOrEmpty(text) ? "(空文本)" : text
                : "缺翻译";
            var tooltip = hasText
                ? "双击编辑；右键可设为缺翻译"
                : "双击补充翻译";
            var label = CreateCellLabel(display, tooltip, hasText is false);
            label.name = $"localization-text-label-{entry.KeyId}-{SafeName(locale)}";
            cell.Add(label);
            cell.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                SelectKey(entry.KeyId);
                if (evt.clickCount == 2)
                {
                    BeginCellEdit(
                        cell,
                        hasText ? text : string.Empty,
                        $"localization-text-editor-{entry.KeyId}-{SafeName(locale)}",
                        value => m_Service.SetText(entry.KeyId, locale, value));
                    evt.StopPropagation();
                }
            });
            cell.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("编辑翻译", _ => BeginCellEdit(
                    cell,
                    hasText ? text : string.Empty,
                    $"localization-text-editor-{entry.KeyId}-{SafeName(locale)}",
                    value => m_Service.SetText(entry.KeyId, locale, value)));
                evt.menu.AppendAction(
                    "设为缺翻译",
                    _ => ApplyResult(m_Service.RemoveText(entry.KeyId, locale)),
                    _ => hasText ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }));
            return cell;
        }

        private void BeginCellEdit(
            VisualElement cell,
            string currentValue,
            string editorName,
            Func<string, LocalizationMutationResult> commit)
        {
            if (cell == null || cell.Q<TextField>() != null)
            {
                return;
            }

            cell.Clear();
            var completed = false;
            var field = new TextField
            {
                name = editorName,
                value = currentValue ?? string.Empty,
                isDelayed = false
            };
            field.style.flexGrow = 1;
            field.style.minWidth = 0;

            void Finish(bool save)
            {
                if (completed)
                {
                    return;
                }

                completed = true;
                if (save)
                {
                    ApplyResult(commit(field.value));
                }
                else
                {
                    Rebuild();
                }
            }

            field.RegisterCallback<FocusOutEvent>(_ => Finish(true));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    Finish(true);
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    Finish(false);
                    evt.StopPropagation();
                }
            });
            cell.Add(field);
            field.Focus();
            field.SelectAll();
        }

        private void SelectKey(long keyId)
        {
            m_SelectedKeyId = keyId;
            foreach (var pair in m_Rows)
            {
                var index = pair.Value.userData is int value ? value : 0;
                pair.Value.style.backgroundColor = RowBackgroundColor(index, pair.Key == keyId);
            }

            m_DeleteKeyButton?.SetEnabled(true);
        }

        private void CommitNewKey()
        {
            var result = m_Service.CreateKey(m_NewKey, string.Empty, string.Empty);
            if (result.Succeeded)
            {
                m_SelectedKeyId = result.KeyId;
                m_IsAddingKey = false;
                m_NewKey = string.Empty;
            }

            ApplyResult(result);
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
                m_IsAddingLocale = false;
                m_NewLocale = string.Empty;
                m_NewLocaleFallback = string.Empty;
            }

            ApplyResult(result);
        }

        private void RemoveSelectedKey(LocalizationAuthoringSnapshot snapshot)
        {
            var entry = snapshot.Entries.FirstOrDefault(item => item.KeyId == m_SelectedKeyId);
            if (entry != null)
            {
                RemoveKey(entry);
            }
        }

        private void RemoveLocale(LocalizationAuthoringSnapshot snapshot, string locale)
        {
            if (snapshot.TryGetLocale(locale, out var authoringLocale) is false)
            {
                return;
            }

            if (EditorUtility.DisplayDialog(
                    "移除语言",
                    $"从 Catalog 移除 {locale}？\n\n文本资产会保留：\n{authoringLocale.AssetPath}",
                    "移除",
                    "取消") is false)
            {
                return;
            }

            ApplyResult(m_Service.RemoveLocale(locale));
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

            m_SelectedKeyId = 0;
            ApplyResult(m_Service.RemoveKey(entry.KeyId));
        }

        private void SetPreviewLocale(string locale)
        {
            var config = EditorGlobalConfig.LoadOrCreate();
            var previous = config.Localization.PreviewLocale;
            config.Localization.PreviewLocale = locale;
            try
            {
                config.Save();
                m_ErrorChanged?.Invoke(null);
                Rebuild();
            }
            catch (Exception exception)
            {
                config.Localization.PreviewLocale = previous;
                m_ErrorChanged?.Invoke($"保存预览语言失败：{exception.Message}");
            }
        }

        private void AddImportWorkbench()
        {
            var import = new LocalizationImportWorkbench(
                m_Service,
                m_ImportService,
                m_ErrorChanged,
                () =>
                {
                    m_ShowImport = false;
                    schedule.Execute(Rebuild);
                });
            import.style.flexGrow = 1;
            import.style.minHeight = 0;
            Add(import);
        }

        private void AddUnavailableState()
        {
            var empty = new Label("使用上方“绑定 Catalog”选择已有资产，或从菜单中新建 Catalog。")
            {
                name = "localization-unavailable-state"
            };
            empty.style.marginLeft = 16;
            empty.style.marginTop = 18;
            empty.style.marginBottom = 8;
            empty.style.unityFontStyleAndWeight = FontStyle.Bold;
            Add(empty);
        }

        private void AddDiagnostics(LocalizationAuthoringSnapshot snapshot)
        {
            if (snapshot.Diagnostics.Count == 0)
            {
                return;
            }

            var diagnostics = new VisualElement { name = "localization-diagnostics" };
            diagnostics.style.flexShrink = 0;
            diagnostics.style.marginLeft = 10;
            diagnostics.style.marginRight = 10;
            diagnostics.style.marginBottom = 6;
            foreach (var diagnostic in snapshot.Diagnostics)
            {
                if (snapshot.Catalog == null &&
                    string.Equals(diagnostic.Code, "catalog_not_bound", StringComparison.Ordinal))
                {
                    continue;
                }

                var label = new Label(diagnostic.Message);
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.marginBottom = 3;
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

        private void ApplyResult(LocalizationMutationResult result)
        {
            if (result == null)
            {
                return;
            }

            m_ErrorChanged?.Invoke(result.Succeeded ? null : result.Message);
            schedule.Execute(Rebuild);
        }

        private static IReadOnlyList<string> GetLocaleNames(LocalizationAuthoringSnapshot snapshot)
        {
            return (snapshot.Catalog?.Locales ?? Array.Empty<LocalizationLocaleDescriptor>())
                .Where(locale => locale != null && string.IsNullOrWhiteSpace(locale.Locale) is false)
                .Select(locale => locale.Locale)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool Matches(
            LocalizationAuthoringSnapshot snapshot,
            IReadOnlyList<string> locales,
            LocalizationAuthoringEntry entry,
            string query)
        {
            if (query.Length == 0 || entry.Key.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return locales.Any(locale =>
                snapshot.TryGetText(entry.KeyId, locale, out var text) &&
                (text ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static VisualElement CreateTableHeaderCell(string text, string name, float width)
        {
            var cell = CreateCell(width);
            cell.name = name;
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            cell.Add(label);
            return cell;
        }

        private static VisualElement CreateHeaderCell(string text, string name, float width)
        {
            return CreateTableHeaderCell(text, name, width);
        }

        private static VisualElement CreateRowContainer(string name, int index)
        {
            var row = new VisualElement { name = name };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexShrink = 0;
            row.style.minHeight = 32;
            row.style.backgroundColor = RowBackgroundColor(index, false);
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = DividerColor();
            return row;
        }

        private static VisualElement CreateCell(float width)
        {
            var cell = new VisualElement();
            cell.style.width = width;
            cell.style.minWidth = width;
            cell.style.maxWidth = width;
            cell.style.flexGrow = 0;
            cell.style.flexShrink = 0;
            cell.style.justifyContent = Justify.Center;
            cell.style.paddingLeft = 8;
            cell.style.paddingRight = 8;
            cell.style.borderRightWidth = 1;
            cell.style.borderRightColor = DividerColor();
            return cell;
        }

        private static Label CreateCellLabel(string text, string tooltip, bool missing)
        {
            var label = new Label(text ?? string.Empty) { tooltip = tooltip };
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            if (missing)
            {
                label.style.color = new Color(0.95f, 0.55f, 0.2f);
            }

            return label;
        }

        private static string SafeName(string value)
        {
            return (value ?? string.Empty).Replace('/', '-').Replace(' ', '-');
        }

        private static Color RowBackgroundColor(int index, bool selected)
        {
            if (selected)
            {
                return EditorGUIUtility.isProSkin
                    ? new Color(0.18f, 0.43f, 0.55f)
                    : new Color(0.58f, 0.78f, 0.9f);
            }

            if (index < 0 || index % 2 == 0)
            {
                return Color.clear;
            }

            return EditorGUIUtility.isProSkin
                ? new Color(0.18f, 0.19f, 0.21f)
                : new Color(0.92f, 0.94f, 0.96f);
        }

        private static Color HeaderBackgroundColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.22f, 0.23f, 0.25f)
                : new Color(0.83f, 0.86f, 0.89f);
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
