using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Log
{
    /// <summary>
    /// 命令面板 - IMGUI 版本
    /// </summary>
    public class CommandPanel : IDebugPanelIMGUI
    {
        public string Name => "Command";
        public int Order => 10;

        private readonly CommandManager _commandManager;
        private string _inputText = "";
        private Vector2 _outputScrollPosition;
        private Vector2 _historyScrollPosition;
        private int _historyIndex = -1;

        private GUIStyle _outputStyle;
        private GUIStyle _inputStyle;
        private bool _stylesInitialized;

        public CommandPanel(CommandManager commandManager)
        {
            _commandManager = commandManager;
        }

        public void OnGUI()
        {
            InitStyles();

            // Output area
            GUILayout.Label("Output:", GUILayout.Height(20));
            _outputScrollPosition = GUILayout.BeginScrollView(_outputScrollPosition, GUILayout.Height(200));
            
            foreach (var line in _commandManager.Output)
            {
                GUILayout.Label(line, _outputStyle);
            }
            
            GUILayout.EndScrollView();

            GUILayout.Space(10);

            // Input area
            GUILayout.BeginHorizontal();
            GUILayout.Label(">", GUILayout.Width(15));
            
            GUI.SetNextControlName("CommandInput");
            _inputText = GUILayout.TextField(_inputText, _inputStyle);
            
            if (GUILayout.Button("Execute", GUILayout.Width(70)))
            {
                ExecuteCommand();
            }
            
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                _commandManager.ClearOutput();
            }
            
            GUILayout.EndHorizontal();

            // Handle keyboard
            HandleKeyboard();

            GUILayout.Space(10);

            // Command history
            GUILayout.Label("History:", GUILayout.Height(20));
            _historyScrollPosition = GUILayout.BeginScrollView(_historyScrollPosition, GUILayout.Height(100));
            
            var history = _commandManager.History;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (GUILayout.Button(history[i], GUI.skin.label))
                {
                    _inputText = history[i];
                }
            }
            
            GUILayout.EndScrollView();

            GUILayout.Space(10);

            // Available commands
            GUILayout.Label("Available Commands:", GUILayout.Height(20));
            GUILayout.BeginHorizontal();
            foreach (var cmd in _commandManager.Commands)
            {
                if (GUILayout.Button(cmd.Name, GUILayout.Width(80)))
                {
                    _inputText = cmd.Name + " ";
                    GUI.FocusControl("CommandInput");
                }
            }
            GUILayout.EndHorizontal();
        }

        private void HandleKeyboard()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;

            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                ExecuteCommand();
                e.Use();
            }
            else if (e.keyCode == KeyCode.UpArrow)
            {
                NavigateHistory(-1);
                e.Use();
            }
            else if (e.keyCode == KeyCode.DownArrow)
            {
                NavigateHistory(1);
                e.Use();
            }
        }

        private void ExecuteCommand()
        {
            if (string.IsNullOrWhiteSpace(_inputText)) return;
            _commandManager.Execute(_inputText);
            _inputText = "";
            _historyIndex = -1;
            _outputScrollPosition.y = float.MaxValue;
        }

        private void NavigateHistory(int direction)
        {
            var history = _commandManager.History;
            if (history.Count == 0) return;

            _historyIndex += direction;
            _historyIndex = Mathf.Clamp(_historyIndex, 0, history.Count - 1);
            _inputText = history[history.Count - 1 - _historyIndex];
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _outputStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) },
                fontSize = 12,
                wordWrap = true
            };

            _inputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 14
            };
        }
    }
}
