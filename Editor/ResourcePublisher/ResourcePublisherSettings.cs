using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditorInternal;
using UnityEngine;

namespace GameDeveloperKit.ResourcePublisher
{
    /// <summary>
    /// 定义 Resource Publisher Settings 类型。
    /// </summary>
    public sealed class ResourcePublisherSettings : ScriptableObject
    {
        public const string DeveloperChannelName = "developer";

        /// <summary>
        /// 定义 Settings Path 常量。
        /// </summary>
        private const string SettingsPath = "ProjectSettings/GameDeveloperKitResourcePublisherSettings.asset";
        /// <summary>
        /// 存储 Instance。
        /// </summary>
        private static ResourcePublisherSettings s_Instance;

        [SerializeField] private List<PublisherChannel> m_Channels;

        [SerializeField] private int m_SelectedChannelIndex;

        /// <summary>
        /// 存储 Channels。
        /// </summary>
        public List<PublisherChannel> Channels => m_Channels;

        public int SelectedChannelIndex
        {
            get => m_SelectedChannelIndex;
            set => m_SelectedChannelIndex = value;
        }

        /// <summary>
        /// 加载 Or Create。
        /// </summary>
        /// <returns>执行结果。</returns>
        public static ResourcePublisherSettings LoadOrCreate()
        {
            if (s_Instance != null)
            {
                if (s_Instance.EnsureDefaults())
                {
                    s_Instance.SaveSettings();
                }

                return s_Instance;
            }

            var settingsFileExists = System.IO.File.Exists(SettingsPath);
            if (settingsFileExists)
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
            var changed = s_Instance.EnsureDefaults();
            if (settingsFileExists is false || changed)
            {
                s_Instance.SaveSettings();
            }

            return s_Instance;
        }

        /// <summary>
        /// 保存 Settings。
        /// </summary>
        public void SaveSettings()
        {
            EnsureDefaults();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingsPath) ?? ".");
            InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { this }, SettingsPath, true);
        }

        /// <summary>
        /// 确保 Defaults。
        /// </summary>
        public bool EnsureDefaults()
        {
            var changed = false;
            if (m_Channels == null)
            {
                m_Channels = new List<PublisherChannel>();
                changed = true;
            }

            for (var i = m_Channels.Count - 1; i >= 0; i--)
            {
                if (m_Channels[i] != null)
                {
                    continue;
                }

                m_Channels.RemoveAt(i);
                changed = true;
            }

            var hasDeveloper = false;
            foreach (var channel in m_Channels)
            {
                if (channel == null)
                {
                    continue;
                }

                changed |= channel.EnsureDefaults();
                hasDeveloper |= string.Equals(channel.ChannelName, DeveloperChannelName, StringComparison.Ordinal);
            }

            if (hasDeveloper is false)
            {
                var developer = new PublisherChannel
                {
                    ChannelName = DeveloperChannelName
                };
                developer.EnsureDefaults();
                m_Channels.Insert(0, developer);
                changed = true;
            }

            if (m_Channels.Count == 0)
            {
                if (m_SelectedChannelIndex != -1)
                {
                    m_SelectedChannelIndex = -1;
                    changed = true;
                }
            }
            else if (m_SelectedChannelIndex < 0 || m_SelectedChannelIndex >= m_Channels.Count)
            {
                m_SelectedChannelIndex = 0;
                changed = true;
            }

            return changed;
        }
    }

    /// <summary>
    /// 定义 Storage Credential 类型。
    /// </summary>
    public sealed class StorageCredential
    {
        public string PlatformId { get; set; }

        public string SecretId { get; set; }

        public string SecretKey { get; set; }
    }

    /// <summary>
    /// 定义 Publisher Channel 类型。
    /// </summary>
    [Serializable]
    public sealed class PublisherChannel
    {
        [SerializeField] private string m_ChannelId;

        [SerializeField] private string m_ChannelName = ResourcePublisherSettings.DeveloperChannelName;

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

        /// <summary>
        /// 确保 Defaults。
        /// </summary>
        public bool EnsureDefaults()
        {
            var changed = false;
            if (string.IsNullOrWhiteSpace(m_ChannelId))
            {
                m_ChannelId = Guid.NewGuid().ToString("N");
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(m_ChannelName))
            {
                m_ChannelName = ResourcePublisherSettings.DeveloperChannelName;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(m_PlatformId))
            {
                m_PlatformId = "cos";
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// 执行 To Credential。
        /// </summary>
        /// <returns>执行结果。</returns>
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
