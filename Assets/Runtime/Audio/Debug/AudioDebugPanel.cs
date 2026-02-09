using System;
using System.Collections.Generic;
using System.Reflection;
using GameDeveloperKit.Log;
using UnityEngine;

namespace GameDeveloperKit.Audio
{
    /// <summary>
    /// 音效模块调试面板 - IMGUI 版本
    /// </summary>
    public class AudioDebugPanel : IDebugPanelIMGUI
    {
        public string Name => "Audio";
        public int Order => 120;

        private Vector2 _scrollPosition;
        private bool _autoRefresh = true;
        private float _lastRefreshTime;
        private const float RefreshInterval = 0.5f;

        private FieldInfo _activeTracksField;
        private FieldInfo _groupsField;
        private FieldInfo _masterVolumeField;
        private FieldInfo _isMutedField;

        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private bool _stylesInitialized;

        public AudioDebugPanel()
        {
            _activeTracksField = typeof(AudioModule).GetField("_activeTracks", BindingFlags.NonPublic | BindingFlags.Instance);
            _groupsField = typeof(AudioModule).GetField("_groups", BindingFlags.NonPublic | BindingFlags.Instance);
            _masterVolumeField = typeof(AudioModule).GetField("_masterVolume", BindingFlags.NonPublic | BindingFlags.Instance);
            _isMutedField = typeof(AudioModule).GetField("_isMuted", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public void OnGUI()
        {
            InitStyles();

            var audioModule = Game.Audio as AudioModule;
            if (audioModule == null)
            {
                GUILayout.Label("AudioModule not available");
                return;
            }

            var activeTracks = _activeTracksField?.GetValue(audioModule) as IList<AudioTrack>;
            var groups = _groupsField?.GetValue(audioModule) as Dictionary<string, AudioGroup>;
            var masterVolume = _masterVolumeField != null ? (float)_masterVolumeField.GetValue(audioModule) : 1f;
            var isMuted = _isMutedField != null ? (bool)_isMutedField.GetValue(audioModule) : false;
            var poolStats = audioModule.GetPoolStats();

            // Toolbar
            GUILayout.BeginHorizontal();
            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto Refresh", GUILayout.Width(100));
            if (GUILayout.Button("Stop All", GUILayout.Width(70)))
                audioModule.StopAll();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Vol: {masterVolume:P0}{(isMuted ? " [MUTED]" : "")}", _labelStyle);
            if (GUILayout.Button(isMuted ? "Unmute" : "Mute", GUILayout.Width(60)))
                audioModule.SetMute(!isMuted);
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.Label($"Tracks: {activeTracks?.Count ?? 0} | Groups: {groups?.Count ?? 0} | Pool: {poolStats.available}/{poolStats.total}", _labelStyle);
            GUILayout.Space(10);

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            // Active Tracks
            if (activeTracks != null && activeTracks.Count > 0)
            {
                GUILayout.Label($"Active Tracks ({activeTracks.Count})", _headerStyle);
                int count = 0;
                foreach (var track in activeTracks)
                {
                    if (count++ >= 20) break;
                    GUILayout.BeginHorizontal("box");
                    var status = track.IsPlaying ? "[>]" : track.IsPaused ? "[||]" : "[x]";
                    GUILayout.Label(status, GUILayout.Width(30));
                    GUILayout.Label(track.ClipName ?? "Unknown", GUILayout.Width(150));
                    if (track.IsPlaying && GUILayout.Button("||", GUILayout.Width(25)))
                        track.Pause();
                    if (track.IsPaused && GUILayout.Button(">", GUILayout.Width(25)))
                        track.Resume();
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                        track.Stop();
                    GUILayout.EndHorizontal();
                }
                if (activeTracks.Count > 20)
                    GUILayout.Label($"... and {activeTracks.Count - 20} more", _labelStyle);
            }

            GUILayout.Space(10);

            // Audio Groups
            if (groups != null && groups.Count > 0)
            {
                GUILayout.Label($"Audio Groups ({groups.Count})", _headerStyle);
                foreach (var kvp in groups)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(kvp.Key, GUILayout.Width(120));
                    GUILayout.Label($"Tracks: {kvp.Value.TrackCount}", _labelStyle);
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();
        }

        public void OnUpdate()
        {
            if (!_autoRefresh) return;
            _lastRefreshTime += Time.deltaTime;
            if (_lastRefreshTime >= RefreshInterval)
                _lastRefreshTime = 0;
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;
            _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.4f, 0.8f, 1f) } };
            _labelStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
        }
    }
}
