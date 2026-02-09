using System;
using System.Collections.Generic;
using System.Reflection;
using GameDeveloperKit.Log;
using UnityEngine;

namespace GameDeveloperKit.Data
{
    /// <summary>
    /// 数据调试面板 - IMGUI 版本
    /// </summary>
    public class DataDebugPanel : IDebugPanelIMGUI
    {
        public string Name => "Data";
        public int Order => 60;

        private Vector2 _scrollPosition;
        private string _searchText = "";
        private bool _showRuntime = true;
        private bool _showPersistent = true;

        private FieldInfo _containerField;
        private FieldInfo _keyIndexField;
        private FieldInfo _runtimeContainersField;
        private FieldInfo _persistentContainersField;

        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private bool _stylesInitialized;

        public DataDebugPanel()
        {
            _containerField = typeof(DataModule).GetField("_container", BindingFlags.NonPublic | BindingFlags.Instance);
            var containerType = typeof(DataContainer);
            _keyIndexField = containerType.GetField("_keyIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            _runtimeContainersField = containerType.GetField("_runtimeContainers", BindingFlags.NonPublic | BindingFlags.Instance);
            _persistentContainersField = containerType.GetField("_persistentContainers", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public void OnGUI()
        {
            InitStyles();

            var dataModule = Game.Data as DataModule;
            if (dataModule == null) { GUILayout.Label("DataModule not available"); return; }

            var container = _containerField?.GetValue(dataModule);
            if (container == null) { GUILayout.Label("DataContainer not available"); return; }

            var keyIndex = _keyIndexField?.GetValue(container) as Dictionary<string, (Type type, bool isPersistent)>;
            var runtimeContainers = _runtimeContainersField?.GetValue(container) as Dictionary<Type, object>;
            var persistentContainers = _persistentContainersField?.GetValue(container) as Dictionary<Type, object>;

            if (keyIndex == null) { GUILayout.Label("No data available"); return; }

            // Toolbar
            GUILayout.BeginHorizontal();
            _showRuntime = GUILayout.Toggle(_showRuntime, "Runtime", GUILayout.Width(70));
            _showPersistent = GUILayout.Toggle(_showPersistent, "Persistent", GUILayout.Width(80));
            GUILayout.Label("Search:", GUILayout.Width(50));
            _searchText = GUILayout.TextField(_searchText, GUILayout.Width(120));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            int runtimeCount = 0, persistentCount = 0;
            foreach (var kvp in keyIndex)
            {
                if (kvp.Value.isPersistent) persistentCount++;
                else runtimeCount++;
            }
            GUILayout.Label($"Runtime: {runtimeCount} | Persistent: {persistentCount} | Total: {keyIndex.Count}", _labelStyle);

            GUILayout.Space(5);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            foreach (var kvp in keyIndex)
            {
                var key = kvp.Key;
                var type = kvp.Value.type;
                var isPersistent = kvp.Value.isPersistent;

                if (!_showRuntime && !isPersistent) continue;
                if (!_showPersistent && isPersistent) continue;
                if (!string.IsNullOrEmpty(_searchText) && !key.Contains(_searchText, StringComparison.OrdinalIgnoreCase)) continue;

                var value = GetValue(key, type, isPersistent ? persistentContainers : runtimeContainers);

                GUILayout.BeginHorizontal("box");
                GUILayout.Label(isPersistent ? "[P]" : "[R]", GUILayout.Width(25));
                GUILayout.Label(key, GUILayout.Width(180));
                GUILayout.Label($"<{type.Name}>", _labelStyle, GUILayout.Width(80));
                GUILayout.Label(FormatValue(value), GUILayout.Width(150));
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private object GetValue(string key, Type type, Dictionary<Type, object> containers)
        {
            if (containers == null || !containers.TryGetValue(type, out var containerObj)) return null;
            var tryGetMethod = containerObj.GetType().GetMethod("TryGet");
            if (tryGetMethod == null) return null;
            var parameters = new object[] { key, null };
            var found = (bool)tryGetMethod.Invoke(containerObj, parameters);
            return found ? parameters[1] : null;
        }

        private string FormatValue(object value)
        {
            if (value == null) return "null";
            if (value is string s) return $"\"{s}\"";
            if (value is Array arr) return $"Array[{arr.Length}]";
            return value.ToString();
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;
            _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.4f, 0.8f, 0.4f) } };
            _labelStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
        }
    }
}
