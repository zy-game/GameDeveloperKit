using System;
using System.Collections.Generic;
using System.Reflection;
using GameDeveloperKit.Log;
using Massive;
using UnityEngine;

namespace GameDeveloperKit.World
{
    /// <summary>
    /// World调试面板 - IMGUI 版本
    /// </summary>
    public class WorldDebugPanel : IDebugPanelIMGUI
    {
        public string Name => "World";
        public int Order => 100;

        private Vector2 _scrollPosition;
        private bool _autoRefresh = true;
        private float _lastRefreshTime;
        private const float RefreshInterval = 0.5f;

        private int? _selectedEntityId;
        private GameWorld _selectedWorld;

        private FieldInfo _worldsField;
        private FieldInfo _contextField;
        private FieldInfo _systemManagerField;
        private FieldInfo _massiveWorldField;

        private float _currentFps;
        private float _fpsAccumulator;
        private int _fpsFrameCount;

        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private bool _stylesInitialized;

        public WorldDebugPanel()
        {
            _worldsField = typeof(WorldModule).GetField("_worlds", BindingFlags.NonPublic | BindingFlags.Instance);
            _contextField = typeof(GameWorld).GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance);
            _systemManagerField = typeof(GameWorld).GetField("_systemManager", BindingFlags.NonPublic | BindingFlags.Instance);
            _massiveWorldField = typeof(WorldContext).GetField("_massiveWorld", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public void OnGUI()
        {
            InitStyles();

            var worldModule = Game.World as WorldModule;
            if (worldModule == null) { GUILayout.Label("WorldModule not available"); return; }

            // Toolbar
            GUILayout.BeginHorizontal();
            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto", GUILayout.Width(50));
            if (_selectedEntityId.HasValue && GUILayout.Button("Back", GUILayout.Width(50)))
            { _selectedEntityId = null; _selectedWorld = null; }
            GUILayout.FlexibleSpace();
            GUILayout.Label($"FPS: {_currentFps:F0}", _labelStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            if (_selectedEntityId.HasValue && _selectedWorld != null)
                ShowEntityDetail();
            else
                ShowWorldList(worldModule);

            GUILayout.EndScrollView();
        }

        private void ShowWorldList(WorldModule worldModule)
        {
            var worlds = _worldsField?.GetValue(worldModule) as Dictionary<string, GameWorld>;
            if (worlds == null || worlds.Count == 0) { GUILayout.Label("No worlds"); return; }

            foreach (var kvp in worlds)
            {
                var world = kvp.Value;
                var context = _contextField?.GetValue(world) as WorldContext;
                var massiveWorld = context != null ? _massiveWorldField?.GetValue(context) as Massive.World : null;
                var systemManager = _systemManagerField?.GetValue(world) as SystemManager;

                GUILayout.BeginVertical("box");
                GUILayout.Label($"World: {kvp.Key}", _headerStyle);
                GUILayout.Label($"Time: {world.Time.TotalTime:F1}s | Frame: {world.Time.FrameCount}", _labelStyle);

                if (systemManager != null)
                {
                    GUILayout.Label($"Systems ({systemManager.Systems.Count}):", _labelStyle);
                    foreach (var sys in systemManager.Systems)
                        GUILayout.Label($"  • {sys.GetType().Name}", _labelStyle);
                }

                if (massiveWorld != null)
                {
                    var entityCount = massiveWorld.Entifiers.Count;
                    GUILayout.Label($"Entities ({entityCount}):", _labelStyle);
                    int count = 0;
                    foreach (var entityId in massiveWorld.Entifiers)
                    {
                        if (count++ >= 20) break;
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"  Entity #{entityId}", GUILayout.Width(150));
                        if (GUILayout.Button("View", GUILayout.Width(50)))
                        { _selectedEntityId = entityId; _selectedWorld = world; }
                        GUILayout.EndHorizontal();
                    }
                    if (entityCount > 20)
                        GUILayout.Label($"  ... and {entityCount - 20} more", _labelStyle);
                }
                GUILayout.EndVertical();
                GUILayout.Space(5);
            }
        }

        private void ShowEntityDetail()
        {
            if (!_selectedEntityId.HasValue || _selectedWorld == null) return;
            var entityId = _selectedEntityId.Value;

            var context = _contextField?.GetValue(_selectedWorld) as WorldContext;
            var massiveWorld = context != null ? _massiveWorldField?.GetValue(context) as Massive.World : null;

            GUILayout.Label($"Entity #{entityId}", _headerStyle);

            if (massiveWorld == null || !massiveWorld.Entifiers.IsAlive(entityId))
            { GUILayout.Label("Entity is not alive", _labelStyle); return; }

            GUILayout.Label("Components:", _labelStyle);
            var allSets = massiveWorld.Sets.AllSets;
            for (int i = 0; i < allSets.Count; i++)
            {
                var set = allSets[i];
                if (!set.Has(entityId)) continue;

                var setType = set.GetType();
                var componentType = setType.IsGenericType ? setType.GenericTypeArguments[0] : null;
                var componentName = componentType?.Name ?? setType.Name;

                GUILayout.BeginVertical("box");
                GUILayout.Label(componentName, _headerStyle);

                if (componentType != null)
                {
                    try
                    {
                        var getMethod = setType.GetMethod("Get");
                        var component = getMethod?.Invoke(set, new object[] { entityId });
                        if (component != null)
                        {
                            foreach (var field in componentType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                            {
                                var value = field.GetValue(component);
                                GUILayout.Label($"  {field.Name}: {FormatValue(value)}", _labelStyle);
                            }
                        }
                    }
                    catch { }
                }
                GUILayout.EndVertical();
            }
        }

        private string FormatValue(object value)
        {
            if (value == null) return "null";
            if (value is Vector3 v3) return $"({v3.x:F2}, {v3.y:F2}, {v3.z:F2})";
            if (value is Vector2 v2) return $"({v2.x:F2}, {v2.y:F2})";
            if (value is float f) return f.ToString("F2");
            return value.ToString();
        }

        public void OnUpdate()
        {
            _fpsAccumulator += Time.unscaledDeltaTime;
            _fpsFrameCount++;
            if (_fpsAccumulator >= 0.5f)
            { _currentFps = _fpsFrameCount / _fpsAccumulator; _fpsAccumulator = 0; _fpsFrameCount = 0; }

            if (!_autoRefresh) return;
            _lastRefreshTime += Time.deltaTime;
            if (_lastRefreshTime >= RefreshInterval) _lastRefreshTime = 0;
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;
            _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.4f, 0.8f, 1f) } };
            _labelStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
        }
    }
}
