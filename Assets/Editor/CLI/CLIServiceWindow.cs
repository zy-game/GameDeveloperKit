using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor.CLI
{
    public class CLIServiceWindow : EditorWindow
    {
        private Label _statusLabel;
        private Label _installStatusLabel;
        private Label _commandsDirLabel;
        private Button _installBtn;
        private ScrollView _taskScroll;
        private VisualElement _taskContainer;
        private List<CLICommand> _recentTasks = new();
        private double _lastRefreshTime;
        private const double RefreshInterval = 1.0;
        private const int MaxRecentTasks = 50;
        
        private static string CLIDirectory => Path.Combine(Application.dataPath, "..", "Library", "CLI");
        private static string CommandsDirectory => Path.Combine(CLIDirectory, "commands");
        private static string InstalledScriptPath => Path.Combine(CLIDirectory, "unity-command.ps1");
        private static string SkillDirectory => Path.Combine(Application.dataPath, "..", ".factory", "skills", "unity-editor");
        private static string InstalledSkillPath => Path.Combine(SkillDirectory, "SKILL.md");
        
        private static string GetSourceScriptPath()
        {
            // 查找 CLIServiceWindow.cs 的路径，脚本文件应该在同一目录下
            var guids = AssetDatabase.FindAssets("t:Script CLIServiceWindow");
            if (guids.Length > 0)
            {
                var scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                var dir = Path.GetDirectoryName(scriptPath);
                return Path.Combine(dir, "unity-command.ps1");
            }
            
            // 回退到默认路径
            return Path.Combine(Application.dataPath, "Editor", "CLI", "unity-command.ps1");
        }
        
        private static string GetSourceSkillPath()
        {
            // 查找 CLIServiceWindow.cs 的路径，SKILL.md 应该在同一目录下
            var guids = AssetDatabase.FindAssets("t:Script CLIServiceWindow");
            if (guids.Length > 0)
            {
                var scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                var dir = Path.GetDirectoryName(scriptPath);
                return Path.Combine(dir, "SKILL.md");
            }
            
            return null;
        }

        [MenuItem("GameDeveloperKit/CLI Service")]
        public static void ShowWindow()
        {
            var window = GetWindow<CLIServiceWindow>("CLI Service");
            window.minSize = new Vector2(450, 500);
        }

        private void OnEnable()
        {
            EditorApplication.update += OnUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnUpdate;
        }

        private void OnUpdate()
        {
            if (EditorApplication.timeSinceStartup - _lastRefreshTime < RefreshInterval)
                return;

            _lastRefreshTime = EditorApplication.timeSinceStartup;
            RefreshTaskList();
            UpdateInstallStatus();
        }
        
        private bool IsInstalled()
        {
            return File.Exists(InstalledScriptPath);
        }
        
        private void UpdateInstallStatus()
        {
            if (_installStatusLabel == null || _installBtn == null) return;
            
            var installed = IsInstalled();
            _installStatusLabel.text = installed ? "Installed" : "Not Installed";
            _installStatusLabel.style.color = installed ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.8f, 0.6f, 0.2f);
            _installBtn.text = installed ? "Reinstall" : "Install";
        }
        
        private void InstallScript()
        {
            try
            {
                // 创建 CLI 目录
                if (!Directory.Exists(CLIDirectory))
                {
                    Directory.CreateDirectory(CLIDirectory);
                }
                
                if (!Directory.Exists(CommandsDirectory))
                {
                    Directory.CreateDirectory(CommandsDirectory);
                }
                
                // 安装 PowerShell 脚本
                var sourceScriptPath = GetSourceScriptPath();
                if (!File.Exists(sourceScriptPath))
                {
                    Debug.LogError($"[CLI] Source script not found: {sourceScriptPath}");
                    return;
                }
                
                File.Copy(sourceScriptPath, InstalledScriptPath, true);
                Debug.Log($"[CLI] Script installed to: {InstalledScriptPath}");
                
                // 安装 SKILL.md 到 .factory/skills/unity-editor/
                var sourceSkillPath = GetSourceSkillPath();
                if (!string.IsNullOrEmpty(sourceSkillPath) && File.Exists(sourceSkillPath))
                {
                    if (!Directory.Exists(SkillDirectory))
                    {
                        Directory.CreateDirectory(SkillDirectory);
                    }
                    
                    File.Copy(sourceSkillPath, InstalledSkillPath, true);
                    Debug.Log($"[CLI] SKILL.md installed to: {InstalledSkillPath}");
                }
                else
                {
                    Debug.LogWarning($"[CLI] SKILL.md not found, skipping skill installation");
                }
                
                UpdateInstallStatus();
            }
            catch (Exception e)
            {
                Debug.LogError($"[CLI] Failed to install: {e.Message}");
            }
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;

            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;

            // 标题
            var title = new Label("CLI Service");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 10;
            root.Add(title);

            // 状态卡片
            var statusCard = CreateCard();
            root.Add(statusCard);

            var statusRow = CreateRow();
            statusCard.Add(statusRow);
            statusRow.Add(new Label("Status:") { style = { width = 100 } });
            _statusLabel = new Label("Active");
            _statusLabel.style.color = new Color(0.4f, 0.8f, 0.4f);
            statusRow.Add(_statusLabel);

            var installRow = CreateRow();
            statusCard.Add(installRow);
            installRow.Add(new Label("Script:") { style = { width = 100 } });
            _installStatusLabel = new Label("Checking...");
            _installStatusLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            installRow.Add(_installStatusLabel);

            var dirRow = CreateRow();
            statusCard.Add(dirRow);
            dirRow.Add(new Label("CLI Dir:") { style = { width = 100 } });
            _commandsDirLabel = new Label(CLIDirectory);
            _commandsDirLabel.style.fontSize = 10;
            _commandsDirLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            _commandsDirLabel.style.overflow = Overflow.Hidden;
            _commandsDirLabel.style.textOverflow = TextOverflow.Ellipsis;
            dirRow.Add(_commandsDirLabel);

            // 按钮行
            var btnRow = CreateRow();
            btnRow.style.marginTop = 10;
            statusCard.Add(btnRow);

            _installBtn = new Button(() => InstallScript()) 
            { 
                text = "Install", 
                style = { flexGrow = 1, height = 28 } 
            };
            btnRow.Add(_installBtn);

            var clearBtn = new Button(() => ClearCompletedTasks()) 
            { 
                text = "Clear Completed", 
                style = { flexGrow = 1, height = 28, marginLeft = 5 } 
            };
            btnRow.Add(clearBtn);

            var refreshBtn = new Button(() => RefreshTaskList()) 
            { 
                text = "Refresh", 
                style = { flexGrow = 1, height = 28, marginLeft = 5 } 
            };
            btnRow.Add(refreshBtn);

            var openDirBtn = new Button(() => OpenCommandsDirectory()) 
            { 
                text = "Open Dir", 
                style = { flexGrow = 1, height = 28, marginLeft = 5 } 
            };
            btnRow.Add(openDirBtn);

            // 使用说明
            var helpCard = CreateCard();
            helpCard.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            root.Add(helpCard);

            helpCard.Add(new Label("Usage") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 5 } });
            helpCard.Add(new Label(
                "PowerShell:\n" +
                "powershell -ExecutionPolicy Bypass -File\n" +
                "  \"Library/CLI/unity-command.ps1\"\n" +
                "  -Command \"<command>\" -Arguments '<json>'\n" +
                "  -WorkingDirectory \"<project_path>\"")
            { style = { whiteSpace = WhiteSpace.Normal, fontSize = 10 } });
            
            UpdateInstallStatus();

            // 任务列表标题
            var taskHeader = CreateRow();
            taskHeader.style.marginTop = 10;
            taskHeader.style.marginBottom = 5;
            root.Add(taskHeader);

            taskHeader.Add(new Label("Recent Tasks") { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } });

            var countLabel = new Label("0 tasks");
            countLabel.name = "task-count";
            countLabel.style.fontSize = 10;
            countLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            taskHeader.Add(countLabel);

            // 任务列表
            _taskScroll = new ScrollView(ScrollViewMode.Vertical);
            _taskScroll.style.flexGrow = 1;
            _taskScroll.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            _taskScroll.style.borderTopLeftRadius = 5;
            _taskScroll.style.borderTopRightRadius = 5;
            _taskScroll.style.borderBottomLeftRadius = 5;
            _taskScroll.style.borderBottomRightRadius = 5;
            root.Add(_taskScroll);

            _taskContainer = new VisualElement();
            _taskContainer.style.paddingTop = 5;
            _taskContainer.style.paddingBottom = 5;
            _taskContainer.style.paddingLeft = 5;
            _taskContainer.style.paddingRight = 5;
            _taskScroll.Add(_taskContainer);

            RefreshTaskList();
        }

        private VisualElement CreateCard()
        {
            var card = new VisualElement();
            card.style.marginBottom = 10;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            card.style.borderTopLeftRadius = 5;
            card.style.borderTopRightRadius = 5;
            card.style.borderBottomLeftRadius = 5;
            card.style.borderBottomRightRadius = 5;
            return card;
        }

        private VisualElement CreateRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 5;
            return row;
        }

        private void OpenCommandsDirectory()
        {
            if (!Directory.Exists(CommandsDirectory))
            {
                Directory.CreateDirectory(CommandsDirectory);
            }
            EditorUtility.RevealInFinder(CommandsDirectory);
        }

        private void ClearCompletedTasks()
        {
            if (!Directory.Exists(CommandsDirectory))
                return;

            var files = Directory.GetFiles(CommandsDirectory, "*.json");
            var clearedCount = 0;

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var cmd = JsonConvert.DeserializeObject<CLICommand>(json);
                    if (cmd != null && (cmd.status == "completed" || cmd.status == "failed"))
                    {
                        File.Delete(file);
                        clearedCount++;
                    }
                }
                catch { }
            }

            Debug.Log($"[CLI] Cleared {clearedCount} completed/failed tasks");
            RefreshTaskList();
        }

        private void RefreshTaskList()
        {
            if (_taskContainer == null) return;

            _taskContainer.Clear();
            _recentTasks.Clear();
            
            if (!Directory.Exists(CommandsDirectory))
            {
                ShowEmptyState();
                UpdateTaskCount();
                return;
            }

            var files = Directory.GetFiles(CommandsDirectory, "*.json");

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var cmd = JsonConvert.DeserializeObject<CLICommand>(json);
                    if (cmd != null)
                    {
                        _recentTasks.Add(cmd);
                    }
                }
                catch { }
            }

            // 按创建时间排序（最新的在前）
            _recentTasks.Sort((a, b) => b.created_at.CompareTo(a.created_at));

            // 限制数量
            if (_recentTasks.Count > MaxRecentTasks)
            {
                _recentTasks = _recentTasks.GetRange(0, MaxRecentTasks);
            }

            if (_recentTasks.Count == 0)
            {
                ShowEmptyState();
                UpdateTaskCount();
                return;
            }

            foreach (var task in _recentTasks)
            {
                _taskContainer.Add(CreateTaskItem(task));
            }

            UpdateTaskCount();
        }

        private void ShowEmptyState()
        {
            _taskContainer.Add(new Label("No tasks yet")
            {
                style =
                {
                    fontSize = 11,
                    color = new Color(0.5f, 0.5f, 0.5f),
                    unityTextAlign = TextAnchor.MiddleCenter,
                    paddingTop = 20
                }
            });
        }

        private void UpdateTaskCount()
        {
            var countLabel = rootVisualElement.Q<Label>("task-count");
            if (countLabel != null)
            {
                var pendingCount = _recentTasks.FindAll(t => t.status == "pending").Count;
                var text = $"{_recentTasks.Count} tasks";
                if (pendingCount > 0)
                {
                    text += $" ({pendingCount} pending)";
                }
                countLabel.text = text;
            }
        }

        private VisualElement CreateTaskItem(CLICommand task)
        {
            var taskItem = new VisualElement();
            taskItem.style.marginBottom = 4;
            taskItem.style.paddingTop = 6;
            taskItem.style.paddingBottom = 6;
            taskItem.style.paddingLeft = 8;
            taskItem.style.paddingRight = 8;
            taskItem.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            taskItem.style.borderTopLeftRadius = 4;
            taskItem.style.borderTopRightRadius = 4;
            taskItem.style.borderBottomLeftRadius = 4;
            taskItem.style.borderBottomRightRadius = 4;

            // 第一行：状态 + 命令名 + 时间
            var row1 = CreateRow();
            row1.style.marginBottom = 2;
            taskItem.Add(row1);

            // 状态标签
            var statusBadge = new Label(task.status ?? "unknown");
            statusBadge.style.fontSize = 9;
            statusBadge.style.paddingTop = 2;
            statusBadge.style.paddingBottom = 2;
            statusBadge.style.paddingLeft = 6;
            statusBadge.style.paddingRight = 6;
            statusBadge.style.borderTopLeftRadius = 3;
            statusBadge.style.borderTopRightRadius = 3;
            statusBadge.style.borderBottomLeftRadius = 3;
            statusBadge.style.borderBottomRightRadius = 3;
            statusBadge.style.marginRight = 8;
            statusBadge.style.unityTextAlign = TextAnchor.MiddleCenter;

            switch (task.status)
            {
                case "pending":
                    statusBadge.style.backgroundColor = new Color(0.7f, 0.6f, 0.1f);
                    statusBadge.style.color = Color.black;
                    break;
                case "running":
                    statusBadge.style.backgroundColor = new Color(0.2f, 0.5f, 0.8f);
                    statusBadge.style.color = Color.white;
                    break;
                case "completed":
                    statusBadge.style.backgroundColor = new Color(0.2f, 0.6f, 0.2f);
                    statusBadge.style.color = Color.white;
                    break;
                case "failed":
                    statusBadge.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
                    statusBadge.style.color = Color.white;
                    break;
                default:
                    statusBadge.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
                    statusBadge.style.color = Color.white;
                    break;
            }
            row1.Add(statusBadge);

            // 命令名
            var commandLabel = new Label(task.command ?? "unknown");
            commandLabel.style.fontSize = 11;
            commandLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            commandLabel.style.flexGrow = 1;
            row1.Add(commandLabel);

            // 时间
            var timeLabel = new Label(FormatTime(task.created_at));
            timeLabel.style.fontSize = 9;
            timeLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            row1.Add(timeLabel);

            // 第二行：ID
            var row2 = CreateRow();
            row2.style.marginBottom = 0;
            taskItem.Add(row2);

            var idLabel = new Label($"ID: {task.id ?? "unknown"}");
            idLabel.style.fontSize = 9;
            idLabel.style.color = new Color(0.4f, 0.4f, 0.4f);
            row2.Add(idLabel);

            // 错误信息（如果有）
            if (!string.IsNullOrEmpty(task.error))
            {
                var errorRow = CreateRow();
                errorRow.style.marginTop = 4;
                taskItem.Add(errorRow);

                var errorLabel = new Label($"Error: {task.error}");
                errorLabel.style.fontSize = 10;
                errorLabel.style.color = new Color(1f, 0.5f, 0.5f);
                errorLabel.style.whiteSpace = WhiteSpace.Normal;
                errorRow.Add(errorLabel);
            }

            // 参数信息（折叠显示）
            if (task.arguments != null && task.arguments.Count > 0)
            {
                var argsRow = CreateRow();
                argsRow.style.marginTop = 2;
                taskItem.Add(argsRow);

                var argsJson = JsonConvert.SerializeObject(task.arguments);
                if (argsJson.Length > 80)
                {
                    argsJson = argsJson.Substring(0, 77) + "...";
                }
                var argsLabel = new Label($"Args: {argsJson}");
                argsLabel.style.fontSize = 9;
                argsLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                argsLabel.style.overflow = Overflow.Hidden;
                argsLabel.style.textOverflow = TextOverflow.Ellipsis;
                argsRow.Add(argsLabel);
            }

            return taskItem;
        }

        private string FormatTime(long unixTimestamp)
        {
            if (unixTimestamp == 0) return "";
            
            var dateTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).LocalDateTime;
            var now = DateTime.Now;
            
            if (dateTime.Date == now.Date)
            {
                return dateTime.ToString("HH:mm:ss");
            }
            return dateTime.ToString("MM-dd HH:mm");
        }
    }
}
