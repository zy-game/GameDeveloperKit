using System;
using System.Collections.Generic;
using System.Reflection;
using GameDeveloperKit.Log;
using UnityEngine;

namespace GameDeveloperKit.Events
{
    /// <summary>
    /// 事件调试面板 - IMGUI 版本
    /// </summary>
    public class EventDebugPanel : IDebugPanelIMGUI
    {
        public string Name => "Event";
        public int Order => 70;

        private Vector2 _scrollPosition;
        private string _searchText = "";
        private bool _autoRefresh = true;
        private float _lastRefreshTime;
        private const float RefreshInterval = 0.5f;

        private readonly List<EventRecord> _eventHistory = new();
        private const int MaxHistoryCount = 100;

        private FieldInfo _handlersField;
        private FieldInfo _eventsField;

        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private bool _stylesInitialized;

        public EventDebugPanel()
        {
            var eventModuleType = typeof(EventModule);
            _handlersField = eventModuleType.GetField("_handlers", BindingFlags.NonPublic | BindingFlags.Instance);
            _eventsField = eventModuleType.GetField("_events", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public void OnGUI()
        {
            InitStyles();

            var eventModule = Game.Event as EventModule;
            if (eventModule == null) { GUILayout.Label("EventModule not available"); return; }

            var handlers = _handlersField?.GetValue(eventModule) as Dictionary<int, List<EventHandlerInfo>>;
            var pendingEvents = _eventsField?.GetValue(eventModule) as Queue<GameEventArgs>;

            int totalSubscriptions = 0;
            if (handlers != null)
                foreach (var kvp in handlers) totalSubscriptions += kvp.Value.Count;

            // Toolbar
            GUILayout.BeginHorizontal();
            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto", GUILayout.Width(50));
            GUILayout.Label("Search:", GUILayout.Width(50));
            _searchText = GUILayout.TextField(_searchText, GUILayout.Width(120));
            if (GUILayout.Button("Clear History", GUILayout.Width(90)))
                _eventHistory.Clear();
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.Label($"Types: {handlers?.Count ?? 0} | Subs: {totalSubscriptions} | Pending: {pendingEvents?.Count ?? 0} | History: {_eventHistory.Count}", _labelStyle);
            GUILayout.Space(10);

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            // Subscriptions
            if (handlers != null && handlers.Count > 0)
            {
                GUILayout.Label("Subscriptions", _headerStyle);
                foreach (var kvp in handlers)
                {
                    var eventName = GetEventNameById(kvp.Key);
                    if (!string.IsNullOrEmpty(_searchText) && !eventName.Contains(_searchText, StringComparison.OrdinalIgnoreCase)) continue;

                    GUILayout.BeginHorizontal("box");
                    GUILayout.Label($"[{kvp.Value.Count}]", GUILayout.Width(30));
                    GUILayout.Label(eventName, GUILayout.Width(200));
                    GUILayout.Label($"ID: {kvp.Key}", _labelStyle);
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(10);

            // Event History
            if (_eventHistory.Count > 0)
            {
                GUILayout.Label("Event History", _headerStyle);
                foreach (var record in _eventHistory)
                {
                    if (!string.IsNullOrEmpty(_searchText) && !record.EventName.Contains(_searchText, StringComparison.OrdinalIgnoreCase)) continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(record.Time.ToString("HH:mm:ss"), _labelStyle, GUILayout.Width(70));
                    GUILayout.Label(record.EventName, GUILayout.Width(150));
                    GUILayout.Label(record.Sender, _labelStyle);
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

        public void RecordEvent(string eventName, int eventId, object sender)
        {
            _eventHistory.Insert(0, new EventRecord { Time = DateTime.Now, EventName = eventName, EventId = eventId, Sender = sender?.GetType().Name ?? "null" });
            while (_eventHistory.Count > MaxHistoryCount) _eventHistory.RemoveAt(_eventHistory.Count - 1);
        }

        private string GetEventNameById(int eventId)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                        if (typeof(GameEventArgs).IsAssignableFrom(type) && !type.IsAbstract && type.GetHashCode() == eventId)
                            return type.Name;
                }
                catch { }
            }
            return $"Event_{eventId}";
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;
            _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.4f, 0.8f, 1f) } };
            _labelStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
        }

        private class EventRecord { public DateTime Time; public string EventName; public int EventId; public string Sender; }
    }
}
