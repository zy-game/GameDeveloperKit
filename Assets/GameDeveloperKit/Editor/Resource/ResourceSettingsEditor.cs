using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;
using GameDeveloperKit.ResourcePublisher;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.ResourceEditor
{
    [CustomEditor(typeof(ResourceSettings))]
    public sealed class ResourceSettingsEditor : Editor
    {
        private SerializedProperty m_Mode;
        private SerializedProperty m_DefaultPackages;
        private SerializedProperty m_ChannelId;
        private SerializedProperty m_ChannelName;
        private SerializedProperty m_ServerUrl;
        private SerializedProperty m_ManifestName;
        private SerializedProperty m_CachePath;
        private List<PublisherChannel> m_Channels = new List<PublisherChannel>();
        private int m_SelectedChannelIndex = -1;
        private string m_Status;
        private string m_MissingChannelId;

        private void OnEnable()
        {
            m_Mode = serializedObject.FindProperty("Mode");
            m_DefaultPackages = serializedObject.FindProperty("DefaultPackages");
            m_ChannelId = serializedObject.FindProperty("ChannelId");
            m_ChannelName = serializedObject.FindProperty("ChannelName");
            m_ServerUrl = serializedObject.FindProperty("ServerUrl");
            m_ManifestName = serializedObject.FindProperty("ManifestName");
            m_CachePath = serializedObject.FindProperty("CachePath");
            RefreshChannels();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_Mode);
            EditorGUILayout.PropertyField(m_DefaultPackages);
            EditorGUILayout.PropertyField(m_ManifestName);
            EditorGUILayout.PropertyField(m_CachePath);

            EditorGUILayout.Space(8f);
            DrawChannelSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawChannelSection()
        {
            EditorGUILayout.LabelField("Publisher Channel", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(m_Channels.Count == 0))
            {
                var selected = m_SelectedChannelIndex;
                var labels = m_Channels.Count == 0
                    ? new[] { "No channels" }
                    : m_Channels.Select(ChannelLabel).ToArray();
                var next = EditorGUILayout.Popup(Mathf.Clamp(selected, 0, Math.Max(0, labels.Length - 1)), labels);
                if (m_Channels.Count > 0 && next != selected)
                {
                    m_SelectedChannelIndex = next;
                    m_Status = string.Empty;
                }
            }

            if (GUILayout.Button("Refresh", GUILayout.Width(72)))
            {
                RefreshChannels();
            }
            EditorGUILayout.EndHorizontal();

            if (m_Channels.Count == 0)
            {
                EditorGUILayout.HelpBox("Resource Publisher has no channels.", MessageType.Info);
                return;
            }

            if (string.IsNullOrWhiteSpace(m_MissingChannelId) is false)
            {
                EditorGUILayout.HelpBox($"Saved channel is missing: {m_MissingChannelId}", MessageType.Warning);
            }

            var channel = m_Channels[Mathf.Clamp(m_SelectedChannelIndex, 0, m_Channels.Count - 1)];
            var root = ResolveChannelRoot(channel, out var error);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Channel Id", channel.ChannelId ?? string.Empty);
                EditorGUILayout.TextField("Server Url", string.IsNullOrWhiteSpace(error) ? root : error);
                EditorGUILayout.TextField("Publish Url", string.IsNullOrWhiteSpace(error) ? CombineAddress(root, RuntimePlatformPreview(), "publish.json") : string.Empty);
                EditorGUILayout.TextField("Manifest Preview", string.IsNullOrWhiteSpace(error) ? CombineAddress(root, RuntimePlatformPreview(), "{version}", ManifestNamePreview()) : string.Empty);
            }

            if (string.IsNullOrWhiteSpace(error) is false)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(error) is false))
            {
                if (GUILayout.Button("Apply Channel"))
                {
                    ApplyChannel(channel, root);
                }
            }

            if (string.IsNullOrWhiteSpace(m_Status) is false)
            {
                EditorGUILayout.HelpBox(m_Status, MessageType.Info);
            }
        }

        private void RefreshChannels()
        {
            var settings = ResourcePublisherSettings.LoadOrCreate();
            m_Channels = settings.Channels
                .Where(x => x != null)
                .ToList();

            var selectedId = m_ChannelId?.stringValue;
            m_MissingChannelId = string.Empty;
            m_SelectedChannelIndex = string.IsNullOrWhiteSpace(selectedId)
                ? settings.SelectedChannelIndex
                : m_Channels.FindIndex(x => x.ChannelId == selectedId);

            if (string.IsNullOrWhiteSpace(selectedId) is false && m_SelectedChannelIndex < 0)
            {
                m_MissingChannelId = selectedId;
            }

            if (m_SelectedChannelIndex < 0 || m_SelectedChannelIndex >= m_Channels.Count)
            {
                m_SelectedChannelIndex = m_Channels.Count == 0 ? -1 : 0;
            }
        }

        private void ApplyChannel(PublisherChannel channel, string root)
        {
            Undo.RecordObject(target, "Apply Resource Channel");
            m_ChannelId.stringValue = channel.ChannelId ?? string.Empty;
            m_ChannelName.stringValue = channel.ChannelName ?? string.Empty;
            m_ServerUrl.stringValue = root;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            m_Status = $"Applied channel: {ChannelLabel(channel)}";
        }

        private static string ResolveChannelRoot(PublisherChannel channel, out string error)
        {
            error = null;
            if (channel == null)
            {
                error = "Channel is missing.";
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(channel.ChannelName))
            {
                error = "Channel name is empty.";
                return string.Empty;
            }

            if (string.Equals(channel.PlatformId, "cos", StringComparison.OrdinalIgnoreCase) is false)
            {
                error = $"Unsupported storage platform: {channel.PlatformId}";
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(channel.RegionId))
            {
                error = "Region is empty.";
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(channel.BucketName))
            {
                error = "Bucket is empty.";
                return string.Empty;
            }

            return CombineAddress($"https://{channel.BucketName}.cos.{channel.RegionId}.myqcloud.com", SanitizeSegment(channel.ChannelName, "dev"));
        }

        private string ManifestNamePreview()
        {
            return string.IsNullOrWhiteSpace(m_ManifestName.stringValue)
                ? ResourceSettings.MANIFEST_NAME
                : m_ManifestName.stringValue;
        }

        private static string RuntimePlatformPreview()
        {
            return EditorUserBuildSettings.activeBuildTarget.ToString();
        }

        private static string ChannelLabel(PublisherChannel channel)
        {
            if (channel == null)
            {
                return "(missing)";
            }

            return $"{EmptyAsDash(channel.ChannelName)} · {EmptyAsDash(channel.BuildTarget)} · {EmptyAsDash(channel.BucketName)}";
        }

        private static string EmptyAsDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static string SanitizeSegment(string value, string fallback)
        {
            var segment = string.IsNullOrWhiteSpace(value) ? fallback : value;
            return segment.Replace('\\', '/').Trim('/');
        }

        private static string CombineAddress(params string[] segments)
        {
            return string.Join("/", segments
                .Select(x => (x ?? string.Empty).Replace('\\', '/').Trim('/'))
                .Where(x => string.IsNullOrWhiteSpace(x) is false));
        }
    }
}
