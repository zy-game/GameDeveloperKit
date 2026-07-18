using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace GameDeveloperKit.LubanConfigEditor.UI
{
    public sealed partial class MainWindow
    {
        /// <summary>
        /// 创建 Workspace Panel。
        /// </summary>
        /// <returns>执行结果。</returns>
        private VisualElement CreateWorkspacePanel()
        {
            var panel = CreatePanel();
            panel.style.minWidth = 0;

            panel.Add(CreateSectionHeader("工作区"));

            m_WorkspaceSelectorField = CreateDropdownField("当前工作区");
            m_WorkspaceSelectorField.RegisterValueChangedCallback(evt =>
            {
                SelectWorkspaceByLabel(evt.newValue);
            });
            panel.Add(m_WorkspaceSelectorField);

            m_WorkspaceRootField = CreateTextField("Root");
            m_WorkspaceRootField.isDelayed = true;
            m_WorkspaceRootField.value = GetInitialWorkspaceRoot();
            var rootRow = CreateFolderSelectRow(m_WorkspaceRootField, new Button(BrowseWorkspaceRoot) { text = "选择" });
            panel.Add(rootRow);

            var actions = CreateButtonRow();
            panel.Add(actions);

            AddRowButton(actions, new Button(InitializeWorkspace) { text = "初始化" });
            AddRowButton(actions, new Button(SelectWorkspaceConf) { text = "选择 luban.conf" });
            AddRowButton(actions, new Button(LoadSelectedWorkspace) { text = "刷新" });

            m_WorkspaceStatusLabel = new Label();
            m_WorkspaceStatusLabel.style.marginTop = 8;
            m_WorkspaceStatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(m_WorkspaceStatusLabel);

            m_WorkspaceDetailField = CreateTextField("Config");
            m_WorkspaceDetailField.isReadOnly = true;
            m_WorkspaceDetailField.multiline = true;
            m_WorkspaceDetailField.style.marginTop = 6;
            m_WorkspaceDetailField.style.minHeight = 80;
            m_WorkspaceDetailField.style.height = 140;
            panel.Add(m_WorkspaceDetailField);

            RefreshWorkspaceSelector();
            LoadSelectedWorkspace();
            return panel;
        }

        /// <summary>
        /// 浏览 Workspace Root。
        /// </summary>
        private void BrowseWorkspaceRoot()
        {
            var startDirectory = LubanCommandRunner.GetAbsoluteProjectPath(GetInitialWorkspaceRoot());
            if (IODirectory.Exists(startDirectory) is false)
            {
                startDirectory = LubanCommandRunner.GetProjectRoot();
            }

            var selectedPath = EditorUtility.OpenFolderPanel("选择配置工作区", startDirectory, string.Empty);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            m_WorkspaceRootField.SetValueWithoutNotify(LubanCommandRunner.ToProjectRelativePath(selectedPath));
        }

        /// <summary>
        /// 初始化 Workspace。
        /// </summary>
        private void InitializeWorkspace()
        {
            try
            {
                var workspaceRoot = string.IsNullOrWhiteSpace(m_WorkspaceRootField.value)
                    ? "DataTables"
                    : m_WorkspaceRootField.value.Trim();
                var absoluteWorkspaceRoot = LubanCommandRunner.GetAbsoluteProjectPath(workspaceRoot);
                var confPath = IOPath.Combine(absoluteWorkspaceRoot, "luban.conf");
                var existed = IOFile.Exists(confPath);
                m_ConfModel = LubanConfModel.InitializeDefault(absoluteWorkspaceRoot);
                UpsertWorkspace(m_ConfModel);
                RefreshWorkspaceStatus(true, existed ? "Loaded existing workspace." : "Workspace initialized.");
            }
            catch (Exception exception)
            {
                RefreshWorkspaceStatus(false, $"Workspace init failed: {exception.Message}");
            }
        }

        /// <summary>
        /// 选择 Workspace Conf。
        /// </summary>
        private void SelectWorkspaceConf()
        {
            var selectedPath = EditorUtility.OpenFilePanel("Select luban.conf", LubanCommandRunner.GetProjectRoot(), "conf,json");
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            LoadWorkspaceConf(selectedPath, "Workspace loaded.");
        }

        /// <summary>
        /// 加载 Selected Workspace。
        /// </summary>
        private void LoadSelectedWorkspace()
        {
            var workspace = GetSelectedWorkspace();
            if (workspace == null || string.IsNullOrWhiteSpace(workspace.ConfPath))
            {
                RefreshWorkspaceStatus(false, "No workspace selected.");
                return;
            }

            var confPath = LubanCommandRunner.GetAbsoluteProjectPath(workspace.ConfPath);
            if (IOFile.Exists(confPath) is false)
            {
                RefreshWorkspaceStatus(false, $"Missing luban.conf: {workspace.ConfPath}");
                return;
            }

            LoadWorkspaceConf(confPath, "Workspace loaded.");
        }

        /// <summary>
        /// 加载 Workspace Conf。
        /// </summary>
        /// <param name="confPath">conf Path 参数。</param>
        /// <param name="statusText">status Text 参数。</param>
        private void LoadWorkspaceConf(string confPath, string statusText)
        {
            try
            {
                m_ConfModel = LubanConfModel.Load(confPath);
                UpsertWorkspace(m_ConfModel);
                RefreshWorkspaceStatus(true, statusText);
            }
            catch (Exception exception)
            {
                RefreshWorkspaceStatus(false, $"Workspace load failed: {exception.Message}");
            }
        }

        /// <summary>
        /// 执行 Upsert Workspace。
        /// </summary>
        /// <param name="model">model 参数。</param>
        private void UpsertWorkspace(LubanConfModel model)
        {
            var confPath = LubanCommandRunner.ToProjectRelativePath(model.ConfPath);
            var workspaceRoot = LubanCommandRunner.ToProjectRelativePath(model.WorkspaceRoot);
            var workspace = m_Settings.Workspaces.FirstOrDefault(x => x != null
                && string.Equals(x.ConfPath, confPath, StringComparison.OrdinalIgnoreCase));
            if (workspace == null)
            {
                workspace = new LubanWorkspaceProfile();
                m_Settings.Workspaces.Add(workspace);
            }

            workspace.Name = string.IsNullOrWhiteSpace(workspace.Name) ? "Default" : workspace.Name;
            workspace.WorkspaceRoot = workspaceRoot;
            workspace.ConfPath = confPath;
            workspace.SchemaDirectory = MakeProjectRelativeChildPath(model.WorkspaceRoot, model.SchemaFiles.FirstOrDefault() ?? "Defines");
            workspace.DataDirectory = MakeProjectRelativeChildPath(model.WorkspaceRoot, model.DataDirectory);
            workspace.DefaultTarget = model.Targets.FirstOrDefault(x => string.Equals(x, "client", StringComparison.OrdinalIgnoreCase))
                ?? model.Targets.FirstOrDefault()
                ?? "client";
            m_Settings.SelectedWorkspaceIndex = m_Settings.Workspaces.IndexOf(workspace);
            m_Settings.SaveSettings();
            m_WorkspaceRootField?.SetValueWithoutNotify(workspace.WorkspaceRoot);
            RefreshWorkspaceSelector();
            EnsureGenerationProfile();
            RefreshProfileFields();
            RefreshCommandPreview();
            RefreshActionState();
        }

        /// <summary>
        /// 刷新 Workspace Selector。
        /// </summary>
        private void RefreshWorkspaceSelector()
        {
            if (m_WorkspaceSelectorField == null)
            {
                return;
            }

            var choices = BuildWorkspaceChoices();
            m_WorkspaceSelectorField.choices = choices;
            if (choices.Count == 0)
            {
                m_WorkspaceSelectorField.SetValueWithoutNotify(string.Empty);
                return;
            }

            var selected = GetSelectedWorkspace();
            m_WorkspaceSelectorField.SetValueWithoutNotify(GetWorkspaceLabel(selected));
        }

        /// <summary>
        /// 构建 Workspace Choices。
        /// </summary>
        /// <returns>执行结果。</returns>
        private List<string> BuildWorkspaceChoices()
        {
            m_Settings.EnsureDefaults();
            return m_Settings.Workspaces
                .Where(x => x != null)
                .Select(GetWorkspaceLabel)
                .Where(x => string.IsNullOrWhiteSpace(x) is false)
                .ToList();
        }

        /// <summary>
        /// 选择 Workspace By Label。
        /// </summary>
        /// <param name="label">label 参数。</param>
        private void SelectWorkspaceByLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            for (var i = 0; i < m_Settings.Workspaces.Count; i++)
            {
                if (string.Equals(GetWorkspaceLabel(m_Settings.Workspaces[i]), label, StringComparison.Ordinal))
                {
                    m_Settings.SelectedWorkspaceIndex = i;
                    m_Settings.SaveSettings();
                    LoadSelectedWorkspace();
                    return;
                }
            }
        }

        /// <summary>
        /// 获取 Workspace Label。
        /// </summary>
        /// <param name="workspace">workspace 参数。</param>
        /// <returns>执行结果。</returns>
        private static string GetWorkspaceLabel(LubanWorkspaceProfile workspace)
        {
            if (workspace == null)
            {
                return string.Empty;
            }

            return $"{workspace.Name} · {workspace.ConfPath}";
        }

        /// <summary>
        /// 获取 Selected Workspace。
        /// </summary>
        /// <returns>执行结果。</returns>
        private LubanWorkspaceProfile GetSelectedWorkspace()
        {
            if (m_Settings.Workspaces.Count == 0
                || m_Settings.SelectedWorkspaceIndex < 0
                || m_Settings.SelectedWorkspaceIndex >= m_Settings.Workspaces.Count)
            {
                return null;
            }

            return m_Settings.Workspaces[m_Settings.SelectedWorkspaceIndex];
        }

        /// <summary>
        /// 获取 Initial Workspace Root。
        /// </summary>
        /// <returns>执行结果。</returns>
        private string GetInitialWorkspaceRoot()
        {
            return GetSelectedWorkspace()?.WorkspaceRoot ?? "DataTables";
        }

        /// <summary>
        /// 创建 Project Relative Child Path。
        /// </summary>
        /// <param name="workspaceRoot">workspace Root 参数。</param>
        /// <param name="childPath">child Path 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 刷新 Workspace Status。
        /// </summary>
        /// <param name="success">success 参数。</param>
        /// <param name="message">message 参数。</param>
        private void RefreshWorkspaceStatus(bool success, string message)
        {
            if (m_WorkspaceStatusLabel != null)
            {
                m_WorkspaceStatusLabel.text = message;
                m_WorkspaceStatusLabel.style.color = success ? new Color(0.35f, 0.8f, 0.45f) : new Color(0.95f, 0.35f, 0.3f);
            }

            if (m_WorkspaceDetailField == null)
            {
                return;
            }

            if (m_ConfModel == null)
            {
                m_WorkspaceDetailField.SetValueWithoutNotify(string.Empty);
                return;
            }

            RefreshTableIndexSummary();
            var details =
                $"Root: {LubanCommandRunner.ToProjectRelativePath(m_ConfModel.WorkspaceRoot)}\n" +
                $"Conf: {LubanCommandRunner.ToProjectRelativePath(m_ConfModel.ConfPath)}\n" +
                $"Data: {m_ConfModel.DataDirectory}\n" +
                $"Schemas: {string.Join(", ", m_ConfModel.SchemaFiles)}\n" +
                $"Targets: {string.Join(", ", m_ConfModel.Targets)}";
            m_WorkspaceDetailField.SetValueWithoutNotify(details);
            RefreshTablePanel();
            RefreshCommandPreview();
            RefreshActionState();
        }

        /// <summary>
        /// 刷新 Table Index Summary。
        /// </summary>
        /// <returns>执行结果。</returns>
        private string RefreshTableIndexSummary()
        {
            try
            {
                m_TableIndex = LubanTableIndex.Scan(m_ConfModel, GetSelectedWorkspace(), GetSelectedGenerationProfile());
                if (m_TableIndex.Tables.Count == 0)
                {
                    return "Tables: none";
                }

                var lines = m_TableIndex.Tables
                    .Select(table => $"- tableName={table.TableName} | dataKey={table.DataKey} | row type={table.RowTypeName} | source={table.SourcePath}");
                return "Tables:\n" + string.Join("\n", lines);
            }
            catch (Exception exception)
            {
                m_TableIndex = null;
                return $"Tables: scan failed: {exception.Message}";
            }
        }
    }
}
