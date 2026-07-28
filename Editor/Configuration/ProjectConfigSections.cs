using System;
using UnityEngine;

namespace GameDeveloperKit.EditorConfiguration
{
    [Serializable]
    public sealed class LubanProjectConfig
    {
        public const string DefaultTableDirectory = "DataTables";
        public const string DefaultGeneratedCodeDirectory = "Assets/Generated/Luban/Code";
        public const string DefaultGeneratedDataDirectory = "Assets/Generated/Luban/Data";
        public const string DefaultCodeNamespace = "cfg";

        [SerializeField] private string m_TableDirectory = DefaultTableDirectory;
        [SerializeField] private string m_GeneratedCodeDirectory = DefaultGeneratedCodeDirectory;
        [SerializeField] private string m_GeneratedDataDirectory = DefaultGeneratedDataDirectory;
        [SerializeField] private string m_CodeNamespace = DefaultCodeNamespace;

        public string TableDirectory
        {
            get => m_TableDirectory;
            set => m_TableDirectory = value;
        }

        public string GeneratedCodeDirectory
        {
            get => m_GeneratedCodeDirectory;
            set => m_GeneratedCodeDirectory = value;
        }

        public string GeneratedDataDirectory
        {
            get => m_GeneratedDataDirectory;
            set => m_GeneratedDataDirectory = value;
        }

        public string CodeNamespace
        {
            get => m_CodeNamespace;
            set => m_CodeNamespace = value;
        }

        internal void EnsureDefaults()
        {
            m_TableDirectory = DefaultIfBlank(m_TableDirectory, DefaultTableDirectory);
            m_GeneratedCodeDirectory = DefaultIfBlank(m_GeneratedCodeDirectory, DefaultGeneratedCodeDirectory);
            m_GeneratedDataDirectory = DefaultIfBlank(m_GeneratedDataDirectory, DefaultGeneratedDataDirectory);
            m_CodeNamespace = DefaultIfBlank(m_CodeNamespace, DefaultCodeNamespace);
        }

        private static string DefaultIfBlank(string value, string defaultValue)
        {
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }
    }

    [Serializable]
    public sealed class LocalizationProjectConfig
    {
        [SerializeField] private string m_CatalogAssetGuid;
        [SerializeField] private string m_PreviewLocale;

        public string CatalogAssetGuid
        {
            get => m_CatalogAssetGuid;
            set => m_CatalogAssetGuid = value;
        }

        public string PreviewLocale
        {
            get => m_PreviewLocale;
            set => m_PreviewLocale = value;
        }

        internal void EnsureDefaults()
        {
            m_CatalogAssetGuid = m_CatalogAssetGuid?.Trim() ?? string.Empty;
            m_PreviewLocale = m_PreviewLocale?.Trim() ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class StoryMediaProjectConfig
    {
        public const string DefaultPreviewLocale = "zh-CN";
        public const int DefaultTimeoutSeconds = 15;

        [SerializeField] private string m_PreviewLocale = DefaultPreviewLocale;
        [SerializeField] private int m_TimeoutSeconds = DefaultTimeoutSeconds;

        public string PreviewLocale
        {
            get => m_PreviewLocale;
            set => m_PreviewLocale = value;
        }

        public int TimeoutSeconds
        {
            get => m_TimeoutSeconds;
            set => m_TimeoutSeconds = value;
        }

        internal void EnsureDefaults()
        {
            m_PreviewLocale = string.IsNullOrWhiteSpace(m_PreviewLocale)
                ? DefaultPreviewLocale
                : m_PreviewLocale.Trim();
            if (m_TimeoutSeconds <= 0)
            {
                m_TimeoutSeconds = DefaultTimeoutSeconds;
            }
        }
    }

    [Serializable]
    public sealed class CloudConnectionConfig
    {
        [SerializeField] private string m_CredentialProfileName;
        [SerializeField] private string m_Bucket;
        [SerializeField] private string m_Region;
        [SerializeField] private string m_Endpoint;
        [SerializeField] private string m_RootPrefix;
        [SerializeField] private string m_PublicBaseUrl;

        public string CredentialProfileName
        {
            get => m_CredentialProfileName;
            set => m_CredentialProfileName = value;
        }

        public string Bucket
        {
            get => m_Bucket;
            set => m_Bucket = value;
        }

        public string Region
        {
            get => m_Region;
            set => m_Region = value;
        }

        public string Endpoint
        {
            get => m_Endpoint;
            set => m_Endpoint = value;
        }

        public string RootPrefix
        {
            get => m_RootPrefix;
            set => m_RootPrefix = value;
        }

        public string PublicBaseUrl
        {
            get => m_PublicBaseUrl;
            set => m_PublicBaseUrl = value;
        }

        internal void EnsureDefaults()
        {
            m_CredentialProfileName = m_CredentialProfileName?.Trim() ?? string.Empty;
            m_Bucket = m_Bucket?.Trim() ?? string.Empty;
            m_Region = m_Region?.Trim() ?? string.Empty;
            m_Endpoint = m_Endpoint?.Trim() ?? string.Empty;
            m_RootPrefix = m_RootPrefix?.Trim() ?? string.Empty;
            m_PublicBaseUrl = m_PublicBaseUrl?.Trim() ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class CloudProjectConfig
    {
        public const string DefaultProviderId = "tencent-cos";
        public const string TencentCosProviderId = "tencent-cos";
        public const string AliyunOssProviderId = "aliyun-oss";

        [SerializeField] private string m_ProviderId = DefaultProviderId;
        [SerializeField] private CloudConnectionConfig m_TencentCos = new CloudConnectionConfig();
        [SerializeField] private CloudConnectionConfig m_AliyunOss = new CloudConnectionConfig();

        public string ProviderId
        {
            get => m_ProviderId;
            set => m_ProviderId = value;
        }

        public CloudConnectionConfig TencentCos => m_TencentCos;

        public CloudConnectionConfig AliyunOss => m_AliyunOss;

        public string CredentialProfileName
        {
            get => ActiveConnection?.CredentialProfileName ?? string.Empty;
            set => SetActiveConnectionValue(connection => connection.CredentialProfileName = value);
        }

        public string Bucket
        {
            get => ActiveConnection?.Bucket ?? string.Empty;
            set => SetActiveConnectionValue(connection => connection.Bucket = value);
        }

        public string Region
        {
            get => ActiveConnection?.Region ?? string.Empty;
            set => SetActiveConnectionValue(connection => connection.Region = value);
        }

        public string Endpoint
        {
            get => ActiveConnection?.Endpoint ?? string.Empty;
            set => SetActiveConnectionValue(connection => connection.Endpoint = value);
        }

        public string RootPrefix
        {
            get => ActiveConnection?.RootPrefix ?? string.Empty;
            set => SetActiveConnectionValue(connection => connection.RootPrefix = value);
        }

        public string PublicBaseUrl
        {
            get => ActiveConnection?.PublicBaseUrl ?? string.Empty;
            set => SetActiveConnectionValue(connection => connection.PublicBaseUrl = value);
        }

        internal void EnsureDefaults()
        {
            m_ProviderId = string.IsNullOrWhiteSpace(m_ProviderId)
                ? DefaultProviderId
                : m_ProviderId.Trim();
            m_TencentCos ??= new CloudConnectionConfig();
            m_AliyunOss ??= new CloudConnectionConfig();
            m_TencentCos.EnsureDefaults();
            m_AliyunOss.EnsureDefaults();
        }

        private CloudConnectionConfig ActiveConnection
        {
            get
            {
                if (string.Equals(m_ProviderId, TencentCosProviderId, StringComparison.Ordinal))
                {
                    return m_TencentCos;
                }

                return string.Equals(m_ProviderId, AliyunOssProviderId, StringComparison.Ordinal)
                    ? m_AliyunOss
                    : null;
            }
        }

        private void SetActiveConnectionValue(Action<CloudConnectionConfig> setter)
        {
            var connection = ActiveConnection;
            if (connection != null)
            {
                setter(connection);
            }
        }
    }
}
