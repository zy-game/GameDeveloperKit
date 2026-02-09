using System;
using System.Collections.Generic;
using System.Reflection;
using GameDeveloperKit.Log;
using UnityEngine;

namespace GameDeveloperKit.Procedure
{
    /// <summary>
    /// 流程调试面板 - IMGUI 版本
    /// </summary>
    public class ProcedureDebugPanel : IDebugPanelIMGUI
    {
        public string Name => "Procedure";
        public int Order => 90;

        private Vector2 _scrollPosition;
        private bool _autoRefresh = true;
        private float _lastRefreshTime;
        private const float RefreshInterval = 0.5f;

        private readonly List<ProcedureRecord> _history = new();
        private const int MaxHistory = 20;
        private Type _lastProcedureType;

        private FieldInfo _procedureManagerField;
        private FieldInfo _proceduresField;

        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _activeStyle;
        private bool _stylesInitialized;

        public ProcedureDebugPanel()
        {
            _procedureManagerField = typeof(ProcedureManager).GetField("_procedureManager", BindingFlags.NonPublic | BindingFlags.Instance);
            _proceduresField = typeof(StateManager).GetField("_procedures", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public void OnGUI()
        {
            InitStyles();

            var procedureManager = Game.Procedure as ProcedureManager;
            if (procedureManager == null) { GUILayout.Label("ProcedureManager not available"); return; }

            var current = procedureManager.CurrentProcedure;
            var currentType = current?.GetType();

            // Record history
            if (currentType != null && currentType != _lastProcedureType)
            {
                _history.Insert(0, new ProcedureRecord { Time = DateTime.Now, ProcedureType = currentType });
                while (_history.Count > MaxHistory) _history.RemoveAt(_history.Count - 1);
                _lastProcedureType = currentType;
            }

            // Toolbar
            GUILayout.BeginHorizontal();
            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto", GUILayout.Width(50));
            if (GUILayout.Button("Clear History", GUILayout.Width(90)))
                _history.Clear();
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.Label($"Current: {currentType?.Name ?? "None"} | History: {_history.Count}", _labelStyle);
            GUILayout.Space(10);

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            // Current Procedure
            GUILayout.Label("Current Procedure", _headerStyle);
            if (current != null)
            {
                GUILayout.BeginHorizontal("box");
                GUI.color = Color.green;
                GUILayout.Label("●", GUILayout.Width(15));
                GUI.color = Color.white;
                GUILayout.Label(currentType.Name, _activeStyle);
                GUILayout.EndHorizontal();
            }
            else
                GUILayout.Label("No active procedure", _labelStyle);

            GUILayout.Space(10);

            // Registered Procedures
            var stateManager = _procedureManagerField?.GetValue(procedureManager) as StateManager;
            var procedures = stateManager != null ? _proceduresField?.GetValue(stateManager) as Dictionary<Type, StateBase> : null;
            if (procedures != null && procedures.Count > 0)
            {
                GUILayout.Label($"Registered ({procedures.Count})", _headerStyle);
                foreach (var kvp in procedures)
                {
                    var isCurrent = currentType == kvp.Key;
                    GUILayout.Label(isCurrent ? $"► {kvp.Key.Name}" : $"  {kvp.Key.Name}", isCurrent ? _activeStyle : _labelStyle);
                }
            }

            GUILayout.Space(10);

            // History
            if (_history.Count > 0)
            {
                GUILayout.Label("History", _headerStyle);
                foreach (var record in _history)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(record.Time.ToString("HH:mm:ss"), _labelStyle, GUILayout.Width(70));
                    GUILayout.Label(record.ProcedureType.Name);
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();
        }

        public void OnUpdate()
        {
            if (!_autoRefresh) return;
            _lastRefreshTime += Time.deltaTime;
            if (_lastRefreshTime >= RefreshInterval) _lastRefreshTime = 0;
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;
            _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.3f, 0.9f, 0.5f) } };
            _labelStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
            _activeStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.3f, 0.9f, 0.5f) } };
        }

        private class ProcedureRecord { public DateTime Time; public Type ProcedureType; }
    }
}
