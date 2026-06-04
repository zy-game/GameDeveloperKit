using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditorInternal;
using UnityEngine;

namespace GameDeveloperKit.ResourcePublisher
{
    public sealed class ResourcePublisherSettings : ScriptableObject
    {
        private const string SettingsPath = "ProjectSettings/GameDeveloperKitResourcePublisherSettings.asset";
        private static ResourcePublisherSettings s_Instance;

        [SerializeField] private List<PublisherChannel> m_Channels;

        [SerializeField] private int m_SelectedChannelIndex;

        public List<PublisherChannel> Channels => m_Channels;

        public int SelectedChannelIndex
        {
            get => m_SelectedChannelIndex;
            set => m_SelectedChannelIndex = value;
        }

        public static ResourcePublisherSettings LoadOrCreate()
        {
            if (s_Instance != null)
            {
                s_Instance.EnsureDefaults();
                return s_Instance;
            }

            if (System.IO.File.Exists(SettingsPath))
            {
                s_Instance = InternalEditorUtility.LoadSerializedFileAndForget(SettingsPath)
                    .OfType<ResourcePublisherSettings>()
                    .FirstOrDefault();
            }

            if (s_Instance == null)
            {
                s_Instance = CreateInstance<ResourcePublisherSettings>();
            }

            s_Instance.hideFlags = HideFlags.HideAndDontSave;
            s_Instance.EnsureDefaults();
            if (System.IO.File.Exists(SettingsPath) is false)
            {
                s_Instance.SaveSettings();
            }

            return s_Instance;
        }

        public void SaveSettings()
        {
            EnsureDefaults();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingsPath) ?? ".");
            InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { this }, SettingsPath, true);
        }

        public void EnsureDefaults()
        {
            m_Channels ??= new List<PublisherChannel>();
            foreach (var channel in m_Channels)
            {
                channel?.EnsureDefaults();
            }

            if (m_Channels.Count == 0)
            {
                m_SelectedChannelIndex = -1;
            }
            else if (m_SelectedChannelIndex < 0 || m_SelectedChannelIndex >= m_Channels.Count)
            {
                m_SelectedChannelIndex = 0;
            }
        }
    }

    public sealed class StorageCredential
    {
        public string PlatformId { get; set; }

        public string SecretId { get; set; }

        public string SecretKey { get; set; }
    }

    [Serializable]
    public sealed class PublisherChannel
    {
        [SerializeField] private string m_ChannelId;

        [SerializeField] private string m_ChannelName = "dev";

        [SerializeField] private string m_BuildTarget;

        [SerializeField] private string m_PlatformId = "cos";

        [SerializeField] private string m_SecretId;

        [SerializeField] private string m_SecretKey;

        [SerializeField] private string m_RegionId;

        [SerializeField] private string m_BucketName;

        [SerializeField] private bool m_IsPublished;

        public string ChannelId
        {
            get => m_ChannelId;
            set => m_ChannelId = value;
        }

        public string ChannelName
        {
            get => m_ChannelName;
            set => m_ChannelName = value;
        }

        public string BuildTarget
        {
            get => m_BuildTarget;
            set => m_BuildTarget = value;
        }

        public string PlatformId
        {
            get => m_PlatformId;
            set => m_PlatformId = value;
        }

        public string SecretId
        {
            get => m_SecretId;
            set => m_SecretId = value;
        }

        public string SecretKey
        {
            get => m_SecretKey;
            set => m_SecretKey = value;
        }

        public string RegionId
        {
            get => m_RegionId;
            set => m_RegionId = value;
        }

        public string BucketName
        {
            get => m_BucketName;
            set => m_BucketName = value;
        }

        public bool IsPublished
        {
            get => m_IsPublished;
            set => m_IsPublished = value;
        }

        public void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(m_ChannelId))
            {
                m_ChannelId = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(m_ChannelName))
            {
                m_ChannelName = "dev";
            }

            if (string.IsNullOrWhiteSpace(m_PlatformId))
            {
                m_PlatformId = "cos";
            }
        }

        public StorageCredential ToCredential()
        {
            return new StorageCredential
            {
                PlatformId = PlatformId,
                SecretId = SecretId,
                SecretKey = SecretKey
            };
        }
    }
}
