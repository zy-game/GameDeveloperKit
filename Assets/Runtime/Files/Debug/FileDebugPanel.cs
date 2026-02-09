using System;
using System.Collections.Generic;
using System.Reflection;
using GameDeveloperKit.Log;
using UnityEngine;

namespace GameDeveloperKit.Files
{
    /// <summary>
    /// 文件模块调试面板 - IMGUI 版本
    /// </summary>
    public class FileDebugPanel : IDebugPanelIMGUI
    {
        public string Name => "File";
        public int Order => 110;

        private Vector2 _scrollPosition;
        private bool _autoRefresh = true;
        private float _lastRefreshTime;
        private const float RefreshInterval = 1f;

        private FieldInfo _systemsField;
        private FieldInfo _cachedHandlesField;
        private FieldInfo _vfsRootPathField;

        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private bool _stylesInitialized;

        public FileDebugPanel()
        {
            _systemsField = typeof(VFSModule).GetField("_systems", BindingFlags.NonPublic | BindingFlags.Instance);
            _cachedHandlesField = typeof(VFSModule).GetField("_cachedHandles", BindingFlags.NonPublic | BindingFlags.Instance);
            _vfsRootPathField = typeof(VFSModule).GetField("_vfsRootPath", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public void OnGUI()
        {
            InitStyles();

            var vfsModule = Game.File as VFSModule;
            if (vfsModule == null) { GUILayout.Label("VFSModule not available"); return; }

            var systems = _systemsField?.GetValue(vfsModule) as IList<VFSystem>;
            var rootPath = _vfsRootPathField?.GetValue(vfsModule) as string;
            var cachedHandles = _cachedHandlesField?.GetValue(vfsModule);
            int cacheCount = GetCacheCount(cachedHandles);

            // Toolbar
            GUILayout.BeginHorizontal();
            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto Refresh", GUILayout.Width(100));
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.Label($"VFS Systems: {systems?.Count ?? 0} | Cached Handles: {cacheCount}", _labelStyle);
            GUILayout.Space(10);

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            // Root Path
            GUILayout.Label("VFS Root Path", _headerStyle);
            GUILayout.Label(rootPath ?? "N/A", _labelStyle);
            GUILayout.Space(10);

            // VFS Systems
            if (systems != null && systems.Count > 0)
            {
                GUILayout.Label($"VFS Systems ({systems.Count})", _headerStyle);
                foreach (var system in systems)
                {
                    var fileCountField = typeof(VFSystem).GetField("_fileCount", BindingFlags.NonPublic | BindingFlags.Instance);
                    var fileCount = fileCountField != null ? (int)fileCountField.GetValue(system) : 0;
                    GUILayout.BeginHorizontal("box");
                    GUILayout.Label(system.SystemId.Substring(0, Math.Min(8, system.SystemId.Length)) + "...", GUILayout.Width(80));
                    GUILayout.Label($"Files: {fileCount}", _labelStyle);
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

        private int GetCacheCount(object cache)
        {
            if (cache == null) return 0;
            var countProp = cache.GetType().GetProperty("Count");
            return countProp != null ? (int)countProp.GetValue(cache) : 0;
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;
            _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.4f, 0.8f, 0.6f) } };
            _labelStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
        }
    }
}
