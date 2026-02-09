using System;
using System.Collections.Generic;
using System.Reflection;
using GameDeveloperKit.Log;
using UnityEngine;

namespace GameDeveloperKit.Config
{
    /// <summary>
    /// 配置调试面板 - IMGUI 版本
    /// </summary>
    public class ConfigDebugPanel : IDebugPanelIMGUI
    {
        public string Name => "Config";
        public int Order => 50;

        private Vector2 _scrollPosition;
        private string _searchText = "";
        private string _selectedConfigType;
        private FieldInfo _configsField;

        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private bool _stylesInitialized;

        public ConfigDebugPanel()
        {
            _configsField = typeof(ConfigModule).GetField("_configs", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public void OnGUI()
        {
            InitStyles();

            var configModule = Game.Config as ConfigModule;
            if (configModule == null)
            {
                GUILayout.Label("ConfigModule not available");
                return;
            }

            var configs = _configsField?.GetValue(configModule) as Dictionary<Type, object>;
            if (configs == null || configs.Count == 0)
            {
                GUILayout.Label("No configs loaded");
                return;
            }

            // Toolbar
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(50));
            _searchText = GUILayout.TextField(_searchText, GUILayout.Width(150));
            GUILayout.FlexibleSpace();
            
            if (!string.IsNullOrEmpty(_selectedConfigType))
            {
                if (GUILayout.Button("Back", GUILayout.Width(60)))
                    _selectedConfigType = null;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            if (string.IsNullOrEmpty(_selectedConfigType))
                ShowConfigList(configs);
            else
                ShowConfigDetail(configs);

            GUILayout.EndScrollView();
        }

        private void ShowConfigList(Dictionary<Type, object> configs)
        {
            int totalItems = 0;
            foreach (var kvp in configs)
            {
                var configType = kvp.Key;
                var configName = configType.Name;

                if (!string.IsNullOrEmpty(_searchText) &&
                    !configName.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                    continue;

                var count = GetConfigCount(kvp.Value);
                totalItems += count;

                GUILayout.BeginHorizontal("box");
                GUILayout.Label(configName, _headerStyle, GUILayout.Width(200));
                GUILayout.Label($"{count} items", _labelStyle, GUILayout.Width(80));
                if (GUILayout.Button("View", GUILayout.Width(60)))
                    _selectedConfigType = configType.FullName;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            GUILayout.Label($"Total: {configs.Count} tables, {totalItems} items", _labelStyle);
        }

        private void ShowConfigDetail(Dictionary<Type, object> configs)
        {
            Type targetType = null;
            object targetContainer = null;

            foreach (var kvp in configs)
            {
                if (kvp.Key.FullName == _selectedConfigType)
                {
                    targetType = kvp.Key;
                    targetContainer = kvp.Value;
                    break;
                }
            }

            if (targetType == null) { _selectedConfigType = null; return; }

            GUILayout.Label($"{targetType.Name}", _headerStyle);
            GUILayout.Space(5);

            var datasProp = targetContainer.GetType().GetProperty("Datas");
            if (datasProp == null) return;

            var datas = datasProp.GetValue(targetContainer) as Array;
            if (datas == null) return;

            var properties = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // Header
            GUILayout.BeginHorizontal("box");
            foreach (var prop in properties)
                GUILayout.Label(prop.Name, _headerStyle, GUILayout.Width(100));
            GUILayout.EndHorizontal();

            // Data rows
            int displayCount = 0;
            foreach (var data in datas)
            {
                if (displayCount >= 50) break;

                if (!string.IsNullOrEmpty(_searchText))
                {
                    bool match = false;
                    foreach (var prop in properties)
                    {
                        var value = prop.GetValue(data)?.ToString() ?? "";
                        if (value.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                        { match = true; break; }
                    }
                    if (!match) continue;
                }

                GUILayout.BeginHorizontal();
                foreach (var prop in properties)
                {
                    var value = prop.GetValue(data);
                    GUILayout.Label(FormatValue(value), _valueStyle, GUILayout.Width(100));
                }
                GUILayout.EndHorizontal();
                displayCount++;
            }

            if (datas.Length > 50)
                GUILayout.Label($"... and {datas.Length - 50} more items", _labelStyle);
        }

        private int GetConfigCount(object container)
        {
            var countProp = container.GetType().GetProperty("Count");
            return countProp != null ? (int)countProp.GetValue(container) : 0;
        }

        private string FormatValue(object value)
        {
            if (value == null) return "null";
            if (value is Array arr) return $"[{arr.Length}]";
            return value.ToString();
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.4f, 0.9f, 0.6f) }
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
            _valueStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white }
            };
        }
    }
}
