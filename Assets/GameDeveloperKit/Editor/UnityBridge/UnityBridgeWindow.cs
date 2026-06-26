using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.UnityBridge
{
    public sealed class UnityBridgeWindow : EditorWindow
    {
        private const string WindowTitle = "Unity Bridge";
        private const string StylePath = "Assets/GameDeveloperKit/Editor/UnityBridge/UnityBridgeWindow.uss";

        private UnityBridgeSettings m_Settings;
        private Toggle m_AutoStartToggle;
        private Label m_StatusLabel;
        private Label m_CompileLabel;
        private Label m_PlayLabel;
        private Label m_SceneLabel;

        private ListView m_TaskLogList;
        private readonly List<UnityBridgeTaskQueue.TaskLogEntry> m_TaskLogItems = new();

        private ListView m_ConsoleList;
        private DropdownField m_ConsoleLevelField;
        private int m_ConsoleCount = 50;
        private readonly List<UnityBridgeConsoleCapture.LogEntry> m_ConsoleItems = new();

        private ListView m_RegistrySkillList;
        private readonly List<IUnityBridgeSkill> m_RegistrySkillItems = new();
        private ScrollView m_InstallerContainer;

        [MenuItem("GameDeveloperKit/Unity Bridge")]
        public static void Open()
        {
            var window = GetWindow<UnityBridgeWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(980f, 720f);
            window.Show();
        }

        public void CreateGUI()
        {
            m_Settings = UnityBridgeSettings.LoadOrCreate();
            BuildLayout();
            RefreshAll();
            RegisterEditorUpdate();
        }

        private void OnDisable()
        {
            UnregisterEditorUpdate();
        }

        private void BuildLayout()
        {
            rootVisualElement.Clear();
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            var root = new VisualElement();
            root.AddToClassList("unity-bridge");
            rootVisualElement.Add(root);

            // ---- toolbar ----
            var toolbar = new VisualElement();
            toolbar.AddToClassList("unity-bridge__toolbar");
            toolbar.Add(new Label("Unity Bridge"));
            root.Add(toolbar);

            // ---- settings row ----
            var settingsRow = new VisualElement();
            settingsRow.AddToClassList("unity-bridge__settings-row");
            m_AutoStartToggle = new Toggle("Auto Start") { value = m_Settings.AutoStart };
            settingsRow.Add(m_AutoStartToggle);
            settingsRow.Add(new Button(SaveSettings) { text = "Save" });
            settingsRow.Add(new Button(UnityBridgeTaskQueue.Start) { text = "Start" });
            settingsRow.Add(new Button(UnityBridgeTaskQueue.Stop) { text = "Stop" });
            root.Add(settingsRow);

            // ---- status ----
            var statusPanel = new VisualElement();
            statusPanel.AddToClassList("unity-bridge__panel");
            m_StatusLabel = new Label();
            m_CompileLabel = new Label();
            m_PlayLabel = new Label();
            m_SceneLabel = new Label();
            statusPanel.Add(new Label("Bridge Status"));
            statusPanel.Add(m_StatusLabel);
            statusPanel.Add(m_CompileLabel);
            statusPanel.Add(m_PlayLabel);
            statusPanel.Add(m_SceneLabel);
            root.Add(statusPanel);

            // ---- body: requests + console ----
            var body = new VisualElement();
            body.AddToClassList("unity-bridge__body");
            root.Add(body);

            var left = new VisualElement();
            left.AddToClassList("unity-bridge__panel");
            left.style.flexGrow = 1f;
            body.Add(left);
            left.Add(new Label("Task Log"));
            m_TaskLogList = new ListView(m_TaskLogItems, 22, () => new Label(), (elem, i) =>
            {
                var item = m_TaskLogItems[i];
                ((Label)elem).text = $"{item.Time:HH:mm:ss} {item.Method} {item.Path} -> {item.StatusCode}";
            })
            { selectionType = SelectionType.Single, style = { flexGrow = 1f } };
            left.Add(m_TaskLogList);

            var right = new VisualElement();
            right.AddToClassList("unity-bridge__panel");
            right.style.flexGrow = 1f;
            body.Add(right);
            right.Add(new Label("Console"));
            m_ConsoleLevelField = new DropdownField("Level", new List<string> { "All", "Error", "Warning", "Log" }, 0);
            m_ConsoleLevelField.RegisterValueChangedCallback(_ => RefreshConsole());
            right.Add(m_ConsoleLevelField);
            m_ConsoleList = new ListView(m_ConsoleItems, 22, () => new Label(), (elem, i) =>
            {
                var item = m_ConsoleItems[i];
                ((Label)elem).text = $"[{item.Timestamp:HH:mm:ss}] {item.Type}: {item.Message}";
            })
            { selectionType = SelectionType.Single, style = { flexGrow = 1f } };
            right.Add(m_ConsoleList);

            // ---- registered skills ----
            var skillPanel = new VisualElement();
            skillPanel.AddToClassList("unity-bridge__panel");
            skillPanel.style.marginTop = 8;
            skillPanel.Add(new Label("Registered Skills"));
            skillPanel.Add(new Label("The following capabilities are available via CLI."));
            m_RegistrySkillList = new ListView(m_RegistrySkillItems, 22, () =>
            {
                var row = new VisualElement();
                row.AddToClassList("unity-bridge__skill-row");
                var name = new Label();
                name.AddToClassList("unity-bridge__skill-name");
                row.Add(name);
                var desc = new Label();
                desc.AddToClassList("unity-bridge__skill-desc");
                row.Add(desc);
                return row;
            }, (elem, i) =>
            {
                var skill = m_RegistrySkillItems[i];
                var labels = elem.Query<Label>().ToList();
                labels[0].text = skill.Name;
                labels[1].text = skill.Description;
            })
            { selectionType = SelectionType.None, style = { flexGrow = 0f, height = 160f } };
            skillPanel.Add(m_RegistrySkillList);
            root.Add(skillPanel);

            // ---- CLI installation ----
            var installPanel = new VisualElement();
            installPanel.AddToClassList("unity-bridge__panel");
            installPanel.style.marginTop = 8;
            installPanel.Add(new Label("CLI Installation"));
            installPanel.Add(new Label("Install the generated skill files to your preferred CLI tool."));

            var installRows = new ScrollView();
            installRows.style.maxHeight = 120;
            m_InstallerContainer = installRows;
            foreach (var adapter in SkillInstaller.Adapters)
            {
                var row = new VisualElement();
                row.AddToClassList("unity-bridge__skill-row");

                var nameLabel = new Label(adapter.DisplayName);
                nameLabel.AddToClassList("unity-bridge__skill-name");
                row.Add(nameLabel);

                var statusLabel = new Label(SkillInstaller.GetInstallStatusText(adapter));
                statusLabel.style.width = 80;
                row.Add(statusLabel);

                var installBtn = new Button(() =>
                {
                    SkillInstaller.Install(adapter);
                    RefreshInstallerRows(installRows);
                });
                installBtn.text = "Install";
                installBtn.style.width = 70;
                installBtn.SetEnabled(!SkillInstaller.IsInstalled(adapter));
                row.Add(installBtn);

                var uninstallBtn = new Button(() =>
                {
                    SkillInstaller.Uninstall(adapter);
                    RefreshInstallerRows(installRows);
                });
                uninstallBtn.text = "Uninstall";
                uninstallBtn.style.width = 70;
                uninstallBtn.SetEnabled(SkillInstaller.IsInstalled(adapter));
                row.Add(uninstallBtn);

                installRows.Add(row);
            }
            installPanel.Add(installRows);
            root.Add(installPanel);
            root.Add(installPanel);
        }

        private void RefreshAll()
        {
            RefreshStatus();
            RefreshTaskLog();
            RefreshConsole();
            RefreshRegistrySkills();
            RefreshInstallerRows();
        }

        private void RefreshStatus()
        {
            m_StatusLabel.text = $"Running: {UnityBridgeTaskQueue.IsRunning} | Pending: {UnityBridgeTaskQueue.PendingCount}";
            m_CompileLabel.text = $"Compiling: {EditorApplication.isCompiling}";
            m_PlayLabel.text = $"Playing: {EditorApplication.isPlaying} / Paused: {EditorApplication.isPaused}";
            m_SceneLabel.text = $"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}";
        }

        private void RefreshTaskLog()
        {
            m_TaskLogItems.Clear();
            m_TaskLogItems.AddRange(UnityBridgeTaskQueue.TaskLog);
            m_TaskLogList?.Rebuild();
        }

        private void RefreshConsole()
        {
            var level = GetConsoleLevelFilter();
            m_ConsoleItems.Clear();
            m_ConsoleItems.AddRange(UnityBridgeConsoleCapture.GetLogs(level, m_ConsoleCount));
            m_ConsoleList?.Rebuild();
        }

        private void RefreshRegistrySkills()
        {
            m_RegistrySkillItems.Clear();
            m_RegistrySkillItems.AddRange(UnityBridgeSkillRegistry.Skills);
            m_RegistrySkillList?.Rebuild();
        }

        private void RefreshInstallerRows()
        {
            RefreshInstallerRows(m_InstallerContainer);
        }

        private void RefreshInstallerRows(ScrollView container)
        {
            if (container == null) return;
            foreach (var child in container.Children())
            {
                var labels = child.Query<Label>().ToList();
                if (labels.Count < 2) continue;
                var name = labels[0].text;
                var adapter = SkillInstaller.Adapters.FirstOrDefault(a => a.DisplayName == name);
                if (adapter == null) continue;
                labels[1].text = SkillInstaller.GetInstallStatusText(adapter);
                var buttons = child.Query<Button>().ToList();
                if (buttons.Count >= 2)
                {
                    buttons[0].SetEnabled(!SkillInstaller.IsInstalled(adapter));
                    buttons[1].SetEnabled(SkillInstaller.IsInstalled(adapter));
                }
            }
        }

        private LogType? GetConsoleLevelFilter()
        {
            return m_ConsoleLevelField?.value switch
            {
                "Error" => LogType.Error,
                "Warning" => LogType.Warning,
                "Log" => LogType.Log,
                _ => null
            };
        }

        private void SaveSettings()
        {
            m_Settings.AutoStart = m_AutoStartToggle.value;
            m_Settings.SaveSettings();
        }

        private void RegisterEditorUpdate()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void UnregisterEditorUpdate()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            RefreshStatus();
            m_TaskLogList?.RefreshItems();
            m_ConsoleList?.RefreshItems();
        }
    }
}
