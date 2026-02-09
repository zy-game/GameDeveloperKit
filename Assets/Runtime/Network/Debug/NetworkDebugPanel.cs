using System;
using System.Collections.Generic;
using System.Reflection;
using GameDeveloperKit.Log;
using UnityEngine;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络调试面板 - IMGUI 版本
    /// </summary>
    public class NetworkDebugPanel : IDebugPanelIMGUI
    {
        public string Name => "Network";
        public int Order => 80;

        private Vector2 _scrollPosition;
        private bool _autoRefresh = true;
        private float _lastRefreshTime;
        private const float RefreshInterval = 0.5f;

        private readonly Dictionary<string, int> _pingInfos = new();
        private FieldInfo _terminalsField;
        private FieldInfo _activeDownloadsField;

        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private bool _stylesInitialized;

        public NetworkDebugPanel()
        {
            _terminalsField = typeof(NetworkModule).GetField("_terminals", BindingFlags.NonPublic | BindingFlags.Instance);
            _activeDownloadsField = typeof(DownloadModule).GetField("_activeDownloads", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public void OnGUI()
        {
            InitStyles();

            // Toolbar
            GUILayout.BeginHorizontal();
            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto Refresh", GUILayout.Width(100));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            // Socket Connections
            var networkModule = Game.GetModule<NetworkModule>();
            if (networkModule != null)
            {
                var terminals = _terminalsField?.GetValue(networkModule) as Dictionary<string, INetworkTerminal>;
                if (terminals != null && terminals.Count > 0)
                {
                    GUILayout.Label($"Socket Connections ({terminals.Count})", _headerStyle);
                    foreach (var kvp in terminals)
                    {
                        GUILayout.BeginHorizontal("box");
                        var stateColor = GetStateColor(kvp.Value.State);
                        GUI.color = stateColor;
                        GUILayout.Label("●", GUILayout.Width(15));
                        GUI.color = Color.white;
                        GUILayout.Label(kvp.Key, GUILayout.Width(100));
                        GUILayout.Label(kvp.Value.Protocol.ToString(), _labelStyle, GUILayout.Width(60));
                        GUILayout.Label(kvp.Value.State.ToString(), GUILayout.Width(80));
                        if (kvp.Value.IsConnected && GUILayout.Button("Disconnect", GUILayout.Width(80)))
                            kvp.Value.Disconnect();
                        GUILayout.EndHorizontal();
                    }
                }
            }

            GUILayout.Space(10);

            // Downloads
            var downloadModule = Game.Download as DownloadModule;
            if (downloadModule != null)
            {
                var downloads = _activeDownloadsField?.GetValue(downloadModule) as System.Collections.Concurrent.ConcurrentDictionary<string, DownloadHandle>;
                if (downloads != null && downloads.Count > 0)
                {
                    GUILayout.Label($"Downloads ({downloads.Count})", _headerStyle);
                    foreach (var kvp in downloads)
                    {
                        var handle = kvp.Value;
                        GUILayout.BeginVertical("box");
                        GUILayout.Label(TruncateUrl(handle.Url, 50));
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"{handle.Progress * 100:F1}%", GUILayout.Width(50));
                        GUILayout.Label($"{FormatBytes(handle.ReceivedBytes)}/{FormatBytes(handle.TotalBytes)}", _labelStyle);
                        GUILayout.Label($"{FormatBytes(handle.CurrentSpeed)}/s", GUILayout.Width(80));
                        if (handle.Status == DownloadStatus.Running && GUILayout.Button("||", GUILayout.Width(25)))
                            handle.Pause();
                        if (handle.Status == DownloadStatus.Paused && GUILayout.Button(">", GUILayout.Width(25)))
                            handle.Resume();
                        if (GUILayout.Button("X", GUILayout.Width(25)))
                            handle.Cancel();
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                    }
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

        private Color GetStateColor(NetworkState state) => state switch
        {
            NetworkState.Connected => Color.green,
            NetworkState.Connecting => Color.yellow,
            NetworkState.Reconnecting => new Color(1f, 0.6f, 0.2f),
            _ => Color.gray
        };

        private string TruncateUrl(string url, int max) => url?.Length > max ? "..." + url.Substring(url.Length - max + 3) : url;
        private string FormatBytes(long b) => b < 1024 ? $"{b}B" : b < 1048576 ? $"{b / 1024f:F1}KB" : $"{b / 1048576f:F1}MB";

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;
            _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.4f, 0.8f, 1f) } };
            _labelStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
        }
    }
}
