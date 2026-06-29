using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using IOFile = System.IO.File;

namespace GameDeveloperKit.LubanConfigEditor
{
    public sealed partial class LubanConfigEditorWindow
    {
        /// <summary>
        /// 创建 Table Panel。
        /// </summary>
        /// <returns>执行结果。</returns>
        private VisualElement CreateTablePanel()
        {
            var panel = CreatePanel();
            panel.style.flexGrow = 1;
            panel.style.marginBottom = 0;
            panel.style.minWidth = 0;

            panel.Add(CreateSectionHeader("配置表"));

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.minWidth = 0;
            toolbar.style.marginBottom = 8;
            panel.Add(toolbar);

            m_TableScopeField = CreateDropdownField("生成范围");
            m_TableScopeField.style.flexGrow = 1;
            m_TableScopeField.RegisterValueChangedCallback(evt =>
            {
                SaveProfileEdit(profile => profile.TableSelection.Scope = TableScopeFromLabel(evt.newValue));
            });
            toolbar.Add(m_TableScopeField);

            var refreshButton = new Button(() =>
            {
                RefreshWorkspaceStatus(m_ConfModel != null, m_ConfModel == null ? "No workspace selected." : "Workspace loaded.");
            })
            {
                text = "刷新"
            };
            refreshButton.style.marginLeft = 8;
            refreshButton.style.flexShrink = 0;
            toolbar.Add(refreshButton);

            var layout = new VisualElement();
            layout.style.flexDirection = FlexDirection.Row;
            layout.style.flexGrow = 1;
            layout.style.minWidth = 0;
            layout.style.minHeight = 420;
            panel.Add(layout);

            var listPane = new VisualElement();
            listPane.style.width = 300;
            listPane.style.minWidth = 260;
            listPane.style.maxWidth = 320;
            listPane.style.marginRight = 12;
            listPane.style.flexShrink = 0;
            listPane.style.minHeight = 0;
            layout.Add(listPane);

            var listHeader = new Label("表列表");
            listHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            listHeader.style.marginBottom = 6;
            listPane.Add(listHeader);

            m_TableListView = new ListView();
            m_TableListView.itemsSource = m_TableItems;
            m_TableListView.selectionType = SelectionType.Single;
            m_TableListView.fixedItemHeight = 62;
            m_TableListView.makeItem = MakeTableRow;
            m_TableListView.bindItem = BindTableRow;
            m_TableListView.selectionChanged += OnTableSelectionChanged;
            m_TableListView.style.flexGrow = 1;
            m_TableListView.style.borderLeftWidth = 1;
            m_TableListView.style.borderRightWidth = 1;
            m_TableListView.style.borderTopWidth = 1;
            m_TableListView.style.borderBottomWidth = 1;
            m_TableListView.style.borderLeftColor = new Color(0.28f, 0.28f, 0.28f);
            m_TableListView.style.borderRightColor = new Color(0.28f, 0.28f, 0.28f);
            m_TableListView.style.borderTopColor = new Color(0.28f, 0.28f, 0.28f);
            m_TableListView.style.borderBottomColor = new Color(0.28f, 0.28f, 0.28f);
            m_TableListView.style.borderTopLeftRadius = 6;
            m_TableListView.style.borderTopRightRadius = 6;
            m_TableListView.style.borderBottomLeftRadius = 6;
            m_TableListView.style.borderBottomRightRadius = 6;
            m_TableListView.style.minWidth = 0;
            listPane.Add(m_TableListView);

            var detail = new VisualElement();
            detail.style.flexGrow = 1;
            detail.style.minWidth = 0;
            detail.style.minHeight = 0;
            layout.Add(detail);

            detail.Add(CreateFieldHeader("Definition"));
            m_TableDetailField = CreateTextField(string.Empty);
            m_TableDetailField.isReadOnly = true;
            m_TableDetailField.multiline = true;
            m_TableDetailField.style.height = 130;
            m_TableDetailField.style.marginBottom = 8;
            detail.Add(m_TableDetailField);

            detail.Add(CreateFieldHeader("Fields"));
            m_TableFieldsField = CreateTextField(string.Empty);
            m_TableFieldsField.isReadOnly = true;
            m_TableFieldsField.multiline = true;
            m_TableFieldsField.style.height = 96;
            m_TableFieldsField.style.marginBottom = 8;
            detail.Add(m_TableFieldsField);

            m_TableDiagnosticsLabel = new Label();
            m_TableDiagnosticsLabel.style.whiteSpace = WhiteSpace.Normal;
            m_TableDiagnosticsLabel.style.marginBottom = 8;
            m_TableDiagnosticsLabel.style.minWidth = 0;
            detail.Add(m_TableDiagnosticsLabel);

            var actions = CreateButtonRow();
            detail.Add(actions);

            m_OpenTableSourceButton = new Button(OpenSelectedTableSource) { text = "打开源表" };
            AddRowButton(actions, m_OpenTableSourceButton);

            m_OpenGeneratedCodeButton = new Button(OpenSelectedGeneratedCode) { text = "打开代码" };
            AddRowButton(actions, m_OpenGeneratedCodeButton);

            m_OpenGeneratedDataButton = new Button(OpenSelectedGeneratedData) { text = "打开数据" };
            AddRowButton(actions, m_OpenGeneratedDataButton);

            m_SaveTableDeclarationButton = new Button(SaveSelectedTableDeclaration) { text = "保存定义" };
            AddRowButton(actions, m_SaveTableDeclarationButton);

            m_SelectSameSourceTablesButton = new Button(SelectSameSourceTables) { text = "选择同源表" };
            AddRowButton(actions, m_SelectSameSourceTablesButton);

            m_ClearSameSourceTablesButton = new Button(ClearSameSourceTables) { text = "清空同源表" };
            AddRowButton(actions, m_ClearSameSourceTablesButton);

            RefreshTablePanel();
            return panel;
        }

        /// <summary>
        /// 刷新 Table Panel。
        /// </summary>
        private void RefreshTablePanel()
        {
            RefreshTableList();
            RefreshTableDetail();
        }

        /// <summary>
        /// 刷新 Table List。
        /// </summary>
        private void RefreshTableList()
        {
            if (m_TableListView == null)
            {
                return;
            }

            m_TableItems.Clear();
            if (m_TableIndex == null || m_TableIndex.Tables.Count == 0)
            {
                m_SelectedTable = null;
                m_TableListView.Rebuild();
                return;
            }

            if (m_SelectedTable == null
                || m_TableIndex.Tables.Any(x => string.Equals(x.TableName, m_SelectedTable.TableName, StringComparison.OrdinalIgnoreCase)) is false)
            {
                m_SelectedTable = m_TableIndex.Tables[0];
            }

            m_TableItems.AddRange(m_TableIndex.Tables);
            m_TableListView.Rebuild();
            var selectedIndex = m_TableItems.FindIndex(x => string.Equals(x.TableName, m_SelectedTable?.TableName, StringComparison.OrdinalIgnoreCase));
            if (selectedIndex >= 0)
            {
                m_TableListView.SetSelectionWithoutNotify(new[] { selectedIndex });
            }
        }

        /// <summary>
        /// 创建 Table Row。
        /// </summary>
        /// <returns>执行结果。</returns>
        private VisualElement MakeTableRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 62;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 8;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.28f, 0.28f, 0.28f);
            row.RegisterCallback<MouseDownEvent>(OnTableRowMouseDown);

            var selectionBar = new VisualElement { name = "selectionBar" };
            selectionBar.style.width = 3;
            selectionBar.style.height = 42;
            selectionBar.style.marginRight = 7;
            selectionBar.style.borderTopLeftRadius = 2;
            selectionBar.style.borderTopRightRadius = 2;
            selectionBar.style.borderBottomLeftRadius = 2;
            selectionBar.style.borderBottomRightRadius = 2;
            selectionBar.style.flexShrink = 0;
            row.Add(selectionBar);

            var toggle = new Toggle { name = "toggle" };
            toggle.style.width = 24;
            toggle.style.marginRight = 6;
            toggle.style.flexShrink = 0;
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.target is not Toggle changedToggle || changedToggle.userData is not LubanTableDefinition table)
                {
                    return;
                }

                SaveProfileEdit(profile => profile.TableSelection.SetSelected(table.TableName, evt.newValue));
            });
            row.Add(toggle);

            var texts = new VisualElement();
            texts.style.flexGrow = 1;
            texts.style.minWidth = 0;
            row.Add(texts);

            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.alignItems = Align.Center;
            top.style.minWidth = 0;
            texts.Add(top);

            var name = new Label { name = "name" };
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.flexGrow = 1;
            name.style.minWidth = 0;
            name.style.whiteSpace = WhiteSpace.Normal;
            top.Add(name);

            var badge = new Label { name = "badge" };
            badge.style.fontSize = 10;
            badge.style.width = 78;
            badge.style.height = 18;
            badge.style.minWidth = 78;
            badge.style.maxWidth = 78;
            badge.style.paddingLeft = 6;
            badge.style.paddingRight = 6;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.borderTopLeftRadius = 4;
            badge.style.borderTopRightRadius = 4;
            badge.style.borderBottomLeftRadius = 4;
            badge.style.borderBottomRightRadius = 4;
            badge.style.flexShrink = 0;
            badge.style.alignSelf = Align.Center;
            top.Add(badge);

            var meta = new Label { name = "meta" };
            meta.style.fontSize = 11;
            meta.style.marginTop = 3;
            meta.style.color = Color.gray;
            meta.style.whiteSpace = WhiteSpace.NoWrap;
            texts.Add(meta);
            return row;
        }

        /// <summary>
        /// 绑定 Table Row。
        /// </summary>
        /// <param name="element">element 参数。</param>
        /// <param name="index">index 参数。</param>
        private void BindTableRow(VisualElement element, int index)
        {
            if (index < 0 || index >= m_TableItems.Count)
            {
                return;
            }

            var table = m_TableItems[index];
            element.userData = index;
            var selected = string.Equals(table.TableName, m_SelectedTable?.TableName, StringComparison.OrdinalIgnoreCase);
            element.style.backgroundColor = selected
                ? (EditorGUIUtility.isProSkin ? new Color(0.22f, 0.24f, 0.27f) : new Color(0.9f, 0.96f, 0.95f))
                : Color.clear;

            var selectionBar = element.Q<VisualElement>("selectionBar");
            selectionBar.style.backgroundColor = selected
                ? (EditorGUIUtility.isProSkin ? new Color(0.22f, 0.74f, 0.66f) : new Color(0.05f, 0.56f, 0.5f))
                : Color.clear;

            var toggle = element.Q<Toggle>("toggle");
            toggle.userData = table;
            toggle.SetValueWithoutNotify(IsTableSelected(table.TableName));
            toggle.SetEnabled(IsSelectedTableScope());

            var name = element.Q<Label>("name");
            name.text = string.IsNullOrWhiteSpace(table.TableName) ? "(未命名)" : table.TableName;
            name.style.color = selected
                ? (EditorGUIUtility.isProSkin ? new Color(0.78f, 1f, 0.94f) : new Color(0.05f, 0.42f, 0.38f))
                : (EditorGUIUtility.isProSkin ? new Color(0.9f, 0.92f, 0.94f) : new Color(0.1f, 0.13f, 0.18f));

            var badge = element.Q<Label>("badge");
            badge.text = table.SourceKind ?? string.Empty;
            badge.style.backgroundColor = table.SourceKind == "ReadOnly"
                ? new Color(0.42f, 0.34f, 0.2f)
                : new Color(0.12f, 0.38f, 0.36f);
            badge.style.color = Color.white;

            var meta = element.Q<Label>("meta");
            meta.text = BuildTableRowMeta(table);
        }

        /// <summary>
        /// 处理 Table Selection Changed。
        /// </summary>
        /// <param name="selection">selection 参数。</param>
        private void OnTableSelectionChanged(System.Collections.Generic.IEnumerable<object> selection)
        {
            var table = selection.OfType<LubanTableDefinition>().FirstOrDefault();
            if (table == null)
            {
                return;
            }

            m_SelectedTable = table;
            RefreshTableList();
            RefreshTableDetail();
        }

        /// <summary>
        /// 处理 Table Row Mouse Down。
        /// </summary>
        /// <param name="evt">evt 参数。</param>
        private void OnTableRowMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || evt.currentTarget is not VisualElement element || element.userData is not int index)
            {
                return;
            }

            if (index < 0 || index >= m_TableItems.Count)
            {
                return;
            }

            m_SelectedTable = m_TableItems[index];
            m_TableListView.SetSelectionWithoutNotify(new[] { index });
            RefreshTableList();
            RefreshTableDetail();
        }

        /// <summary>
        /// 刷新 Table Detail。
        /// </summary>
        private void RefreshTableDetail()
        {
            var hasTable = m_SelectedTable != null;
            SetProfileDropdown(
                m_TableScopeField,
                BuildTableScopeChoices(),
                LabelFromTableScope(GetSelectedGenerationProfile()?.TableSelection.Scope ?? LubanTableScope.AllTables));
            m_OpenTableSourceButton?.SetEnabled(hasTable && HasProjectFile(m_SelectedTable.SourcePath));
            m_OpenGeneratedCodeButton?.SetEnabled(hasTable && HasProjectFile(GetGeneratedCodePath(m_SelectedTable)));
            m_OpenGeneratedDataButton?.SetEnabled(hasTable && HasProjectFile(m_SelectedTable.ConfigPathCandidate));
            m_SaveTableDeclarationButton?.SetEnabled(hasTable);
            var sameSourceTables = hasTable ? GetSameSourceTables(m_SelectedTable) : Array.Empty<LubanTableDefinition>();
            m_SelectSameSourceTablesButton?.SetEnabled(hasTable && sameSourceTables.Count > 1);
            m_ClearSameSourceTablesButton?.SetEnabled(hasTable && sameSourceTables.Count > 1);

            if (m_TableDetailField == null || m_TableFieldsField == null || m_TableDiagnosticsLabel == null)
            {
                return;
            }

            if (hasTable is false)
            {
                m_TableDetailField.SetValueWithoutNotify(string.Empty);
                m_TableFieldsField.SetValueWithoutNotify(string.Empty);
                m_TableDiagnosticsLabel.text = "No table selected.";
                return;
            }

            var table = m_SelectedTable;
            sameSourceTables = GetSameSourceTables(table);
            var sourceInputLabel = string.Equals(table.SourceKind, "ExcelInline", StringComparison.OrdinalIgnoreCase)
                ? "sheet"
                : "input";
            var details =
                $"tableName={table.TableName}\n" +
                $"dataKey={table.DataKey}\n" +
                $"row type={table.RowTypeName}\n" +
                $"selected={IsTableSelected(table.TableName)}\n" +
                $"source={table.SourcePath}\n" +
                $"source kind={table.SourceKind}\n" +
                $"{sourceInputLabel}={table.InputName}\n" +
                $"source tables={sameSourceTables.Count}\n" +
                $"groups={string.Join(", ", table.Groups ?? Array.Empty<string>())}\n" +
                $"key/index={table.KeyOrIndex}\n" +
                $"data path={table.ConfigPathCandidate}";
            m_TableDetailField.SetValueWithoutNotify(details);
            m_TableFieldsField.SetValueWithoutNotify(BuildFieldDetails(table));
            m_TableDiagnosticsLabel.text = BuildTableDiagnostics(table, sameSourceTables.Count);
        }

        /// <summary>
        /// 构建 Field Details。
        /// </summary>
        /// <param name="table">table 参数。</param>
        /// <returns>执行结果。</returns>
        private static string BuildFieldDetails(LubanTableDefinition table)
        {
            if (table.Fields == null || table.Fields.Count == 0)
            {
                return string.Empty;
            }

            var lines = table.Fields.Select(field =>
            {
                var keyText = field.KeyParticipant ? " key" : string.Empty;
                return $"{field.VariableName}: {field.Type} [{string.Join(",", field.Groups ?? Array.Empty<string>())}]{keyText} - {field.Comment}";
            });
            return string.Join("\n", lines);
        }

        /// <summary>
        /// 构建 Table Diagnostics。
        /// </summary>
        /// <param name="table">table 参数。</param>
        /// <returns>执行结果。</returns>
        private static string BuildTableDiagnostics(LubanTableDefinition table, int sameSourceTableCount)
        {
            var source = LubanTableDeclarationSource.Create(table);
            var sourceNote = string.Equals(table.SourceKind, "ExcelInline", StringComparison.OrdinalIgnoreCase) && sameSourceTableCount > 1
                ? $"同一个 Excel 中检测到 {sameSourceTableCount} 张表；全部表模式会一起导出，选中表模式可用同源表按钮一次选择。"
                : string.Empty;
            if (table.SourceKind == "ReadOnly")
            {
                return "Generated table only. Source was not found in current workspace scan.";
            }

            if (source.CanSave is false)
            {
                return JoinDiagnostics(source.ReadOnlyReason, sourceNote);
            }

            if (string.IsNullOrWhiteSpace(table.KeyOrIndex))
            {
                return JoinDiagnostics("Missing key/index metadata.", sourceNote);
            }

            if (table.Fields == null || table.Fields.Count == 0)
            {
                return JoinDiagnostics("No fields detected.", sourceNote);
            }

            return JoinDiagnostics("Ready.", sourceNote);
        }

        /// <summary>
        /// 拼接 Diagnostics。
        /// </summary>
        /// <param name="primary">primary 参数。</param>
        /// <param name="note">note 参数。</param>
        /// <returns>执行结果。</returns>
        private static string JoinDiagnostics(string primary, string note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return primary;
            }

            return $"{primary}\n{note}";
        }

        /// <summary>
        /// 打开 Selected Table Source。
        /// </summary>
        private void OpenSelectedTableSource()
        {
            OpenProjectFile(m_SelectedTable?.SourcePath);
        }

        /// <summary>
        /// 打开 Selected Generated Code。
        /// </summary>
        private void OpenSelectedGeneratedCode()
        {
            OpenProjectFile(GetGeneratedCodePath(m_SelectedTable));
        }

        /// <summary>
        /// 打开 Selected Generated Data。
        /// </summary>
        private void OpenSelectedGeneratedData()
        {
            OpenProjectFile(m_SelectedTable?.ConfigPathCandidate);
        }

        /// <summary>
        /// 保存 Selected Table Declaration。
        /// </summary>
        private void SaveSelectedTableDeclaration()
        {
            if (m_SelectedTable == null)
            {
                return;
            }

            var source = LubanTableDeclarationSource.Create(m_SelectedTable);
            var result = source.Save(m_SelectedTable);
            m_TableDiagnosticsLabel.text = result.Success ? result.Message : $"Save failed: {result.Message}";
        }

        /// <summary>
        /// 选择 Same Source Tables。
        /// </summary>
        private void SelectSameSourceTables()
        {
            SetSameSourceTablesSelected(true);
        }

        /// <summary>
        /// 清空 Same Source Tables。
        /// </summary>
        private void ClearSameSourceTables()
        {
            SetSameSourceTablesSelected(false);
        }

        /// <summary>
        /// 设置 Same Source Tables Selected。
        /// </summary>
        /// <param name="selected">selected 参数。</param>
        private void SetSameSourceTablesSelected(bool selected)
        {
            if (m_SelectedTable == null)
            {
                return;
            }

            var sameSourceTables = GetSameSourceTables(m_SelectedTable);
            if (sameSourceTables.Count == 0)
            {
                return;
            }

            SaveProfileEdit(profile =>
            {
                profile.TableSelection.Scope = LubanTableScope.SelectedTables;
                foreach (var table in sameSourceTables)
                {
                    profile.TableSelection.SetSelected(table.TableName, selected);
                }
            });
        }

        /// <summary>
        /// 打开 Project File。
        /// </summary>
        /// <param name="path">path 参数。</param>
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

        /// <summary>
        /// 是否有 Project File。
        /// </summary>
        /// <param name="path">path 参数。</param>
        /// <returns>执行结果。</returns>
        private static bool HasProjectFile(string path)
        {
            return string.IsNullOrWhiteSpace(path) is false
                && IOFile.Exists(LubanCommandRunner.GetAbsoluteProjectPath(path));
        }

        /// <summary>
        /// 是否 Selected Table Scope。
        /// </summary>
        /// <returns>执行结果。</returns>
        private bool IsSelectedTableScope()
        {
            return GetSelectedGenerationProfile()?.TableSelection.Scope == LubanTableScope.SelectedTables;
        }

        /// <summary>
        /// 尝试获取 Generate Table Selection Ready。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <returns>执行结果。</returns>
        private bool TryGetGenerateTableSelectionReady(out string message)
        {
            message = string.Empty;
            var profile = GetSelectedGenerationProfile();
            if (profile == null || profile.TableSelection.Scope != LubanTableScope.SelectedTables)
            {
                return true;
            }

            var selectedTableNames = profile.TableSelection.SelectedTableNames
                .Where(x => string.IsNullOrWhiteSpace(x) is false)
                .Select(x => x.Trim())
                .ToArray();
            if (selectedTableNames.Length == 0)
            {
                message = "当前生成范围是“选中表”，但没有勾选任何表。请在配置表页勾选表，或切回“全部表”。";
                return false;
            }

            if (m_TableIndex == null)
            {
                message = "当前生成范围是“选中表”，但表索引还没有加载。请先刷新工作区。";
                return false;
            }

            var missingTableNames = selectedTableNames
                .Where(tableName => m_TableIndex.Tables.Any(table => string.Equals(table.TableName, tableName, StringComparison.OrdinalIgnoreCase)) is false)
                .ToArray();
            if (missingTableNames.Length > 0)
            {
                message = $"选中表不存在于当前 Luban 表索引：{string.Join(", ", missingTableNames)}。请刷新表列表或取消这些选择。";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取 Same Source Tables。
        /// </summary>
        /// <param name="table">table 参数。</param>
        /// <returns>执行结果。</returns>
        private System.Collections.Generic.IReadOnlyList<LubanTableDefinition> GetSameSourceTables(LubanTableDefinition table)
        {
            if (table == null || m_TableIndex == null || string.IsNullOrWhiteSpace(table.SourcePath))
            {
                return Array.Empty<LubanTableDefinition>();
            }

            return m_TableIndex.Tables
                .Where(x => string.Equals(x.SourcePath, table.SourcePath, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        /// <summary>
        /// 执行 Label From Table Scope。
        /// </summary>
        /// <param name="scope">scope 参数。</param>
        /// <returns>执行结果。</returns>
        private static string LabelFromTableScope(LubanTableScope scope)
        {
            return scope == LubanTableScope.SelectedTables ? "选中表" : "全部表";
        }

        /// <summary>
        /// 执行 Table Scope From Label。
        /// </summary>
        /// <param name="label">label 参数。</param>
        /// <returns>执行结果。</returns>
        private static LubanTableScope TableScopeFromLabel(string label)
        {
            return string.Equals(label, "选中表", StringComparison.Ordinal) ? LubanTableScope.SelectedTables : LubanTableScope.AllTables;
        }

        /// <summary>
        /// 是否 Table Selected。
        /// </summary>
        /// <param name="tableName">table Name 参数。</param>
        /// <returns>执行结果。</returns>
        private bool IsTableSelected(string tableName)
        {
            var profile = GetSelectedGenerationProfile();
            if (profile == null || string.IsNullOrWhiteSpace(tableName))
            {
                return false;
            }

            if (profile.TableSelection.Scope == LubanTableScope.AllTables)
            {
                return true;
            }

            return profile.TableSelection.SelectedTableNames.Any(x => string.Equals(x, tableName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 构建 Table Row Meta。
        /// </summary>
        /// <param name="table">table 参数。</param>
        /// <returns>执行结果。</returns>
        private static string BuildTableRowMeta(LubanTableDefinition table)
        {
            if (table == null)
            {
                return string.Empty;
            }

            if (string.Equals(table.SourceKind, "ExcelInline", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(table.InputName) is false)
            {
                return $"{table.InputName} · {table.DataKey} · {table.RowTypeName}";
            }

            return $"{table.DataKey} · {table.RowTypeName}";
        }

        /// <summary>
        /// 获取 Generated Code Path。
        /// </summary>
        /// <param name="table">table 参数。</param>
        /// <returns>执行结果。</returns>
        private string GetGeneratedCodePath(LubanTableDefinition table)
        {
            var profile = GetSelectedGenerationProfile();
            if (table == null || profile == null || string.IsNullOrWhiteSpace(profile.OutputCodeDirectory))
            {
                return string.Empty;
            }

            var rowTypeName = table.RowTypeName ?? string.Empty;
            var rowLocalName = rowTypeName.Contains(".")
                ? rowTypeName.Substring(rowTypeName.LastIndexOf(".", StringComparison.Ordinal) + 1)
                : rowTypeName;
            var fileName = string.IsNullOrWhiteSpace(rowLocalName) ? $"{table.TableName}.cs" : $"{rowLocalName}.cs";
            return $"{profile.OutputCodeDirectory.TrimEnd('/', '\\')}/{fileName}";
        }
    }
}
