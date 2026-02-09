using System.Collections.Generic;
using UnityEngine;

namespace GameDeveloperKit.Log
{
    /// <summary>
    /// 日志面板 - IMGUI 版本
    /// </summary>
    public class LogPanel : IDebugPanelIMGUI
    {
        public string Name => "Log";
        public int Order => 0;

        private readonly LogCache _logCache;
        private Vector2 _scrollPosition;
        private Vector2 _stackScrollPosition;
        private bool _autoScroll = true;
        private LogLevel _filterLevel = LogLevel.Debug;
        private string _searchText = "";
        private readonly List<LogEntry> _filteredLogs = new();
        private bool _needsRefresh = true;
        private int _selectedLogIndex = -1;

        private GUIStyle _logStyle;
        private GUIStyle _warningStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _selectedStyle;
        private GUIStyle _stackTraceStyle;
        private bool _stylesInitialized;

        public LogPanel(LogCache logCache)
        {
            _logCache = logCache;
        }

        public void OnGUI()
        {
            InitStyles();

            // Toolbar
            GUILayout.BeginHorizontal();
            
            GUILayout.Label("Filter:", GUILayout.Width(40));
            var newFilter = (LogLevel)GUILayout.SelectionGrid((int)_filterLevel, 
                new[] { "All", "Info", "Warn", "Error" }, 4, GUILayout.Width(200));
            if (newFilter != _filterLevel)
            {
                _filterLevel = newFilter;
                _needsRefresh = true;
                _selectedLogIndex = -1;
            }

            GUILayout.Space(10);
            GUILayout.Label("Search:", GUILayout.Width(50));
            var newSearch = GUILayout.TextField(_searchText, GUILayout.Width(150));
            if (newSearch != _searchText)
            {
                _searchText = newSearch;
                _needsRefresh = true;
                _selectedLogIndex = -1;
            }

            GUILayout.FlexibleSpace();
            _autoScroll = GUILayout.Toggle(_autoScroll, "Auto Scroll", GUILayout.Width(100));
            
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                _logCache.Clear();
                _needsRefresh = true;
                _selectedLogIndex = -1;
            }
            
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Refresh filtered logs
            if (_needsRefresh)
            {
                RefreshFilteredLogs();
                _needsRefresh = false;
            }

            // Calculate heights based on whether stack trace is shown
            float logListHeight = _selectedLogIndex >= 0 ? Screen.height * 0.5f : Screen.height - 150;
            
            // Log list
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(logListHeight));
            
            for (int i = 0; i < _filteredLogs.Count; i++)
            {
                var log = _filteredLogs[i];
                var isSelected = i == _selectedLogIndex;
                
                var style = isSelected ? _selectedStyle : log.Level switch
                {
                    LogLevel.Warning => _warningStyle,
                    LogLevel.Error or LogLevel.Fatal => _errorStyle,
                    _ => _logStyle
                };

                GUILayout.BeginHorizontal(isSelected ? "box" : GUIStyle.none);
                
                if (GUILayout.Button($"[{log.Timestamp:HH:mm:ss}]", GUI.skin.label, GUILayout.Width(70)))
                    _selectedLogIndex = isSelected ? -1 : i;
                if (GUILayout.Button($"[{log.Level}]", GUI.skin.label, GUILayout.Width(60)))
                    _selectedLogIndex = isSelected ? -1 : i;
                if (GUILayout.Button(log.Message, style))
                    _selectedLogIndex = isSelected ? -1 : i;
                    
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            // Auto scroll
            if (_autoScroll && _logCache.Count > 0 && _selectedLogIndex < 0)
            {
                _scrollPosition.y = float.MaxValue;
            }

            // Stack trace panel
            if (_selectedLogIndex >= 0 && _selectedLogIndex < _filteredLogs.Count)
            {
                var selectedLog = _filteredLogs[_selectedLogIndex];
                
                GUILayout.Space(5);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Stack Trace:", GUILayout.Width(80));
                if (GUILayout.Button("Copy", GUILayout.Width(50)))
                    GUIUtility.systemCopyBuffer = selectedLog.StackTrace ?? "";
                if (GUILayout.Button("Close", GUILayout.Width(50)))
                    _selectedLogIndex = -1;
                GUILayout.EndHorizontal();
                
                GUILayout.Space(5);
                _stackScrollPosition = GUILayout.BeginScrollView(_stackScrollPosition, GUILayout.Height(Screen.height * 0.3f));
                
                var stackTrace = selectedLog.StackTrace;
                if (string.IsNullOrEmpty(stackTrace))
                    GUILayout.Label("No stack trace available", _stackTraceStyle);
                else
                    GUILayout.Label(stackTrace, _stackTraceStyle);
                    
                GUILayout.EndScrollView();
            }
        }

        public void OnUpdate()
        {
            if (_logCache.Count != _filteredLogs.Count)
                _needsRefresh = true;
        }

        private void RefreshFilteredLogs()
        {
            _filteredLogs.Clear();
            foreach (var log in _logCache.GetAll())
            {
                if (log.Level < _filterLevel) continue;
                if (!string.IsNullOrEmpty(_searchText) && 
                    !log.Message.Contains(_searchText, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                _filteredLogs.Add(log);
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _logStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white },
                wordWrap = false
            };

            _warningStyle = new GUIStyle(_logStyle)
            {
                normal = { textColor = new Color(1f, 0.8f, 0.2f) }
            };

            _errorStyle = new GUIStyle(_logStyle)
            {
                normal = { textColor = new Color(1f, 0.3f, 0.3f) }
            };

            _selectedStyle = new GUIStyle(_logStyle)
            {
                normal = { textColor = new Color(0.4f, 0.8f, 1f) },
                fontStyle = FontStyle.Bold
            };

            _stackTraceStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) },
                wordWrap = true,
                fontSize = 11
            };
        }
    }
}
