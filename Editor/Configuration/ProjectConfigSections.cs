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

        [SerializeField] private string m_CatalogApiUrl;
        [SerializeField] private string m_CdnBaseUrl;
        [SerializeField] private string m_PreviewLocale = DefaultPreviewLocale;
        [SerializeField] private int m_TimeoutSeconds = DefaultTimeoutSeconds;

        public string CatalogApiUrl
        {
            get => m_CatalogApiUrl;
            set => m_CatalogApiUrl = value;
        }

        public string CdnBaseUrl
        {
            get => m_CdnBaseUrl;
            set => m_CdnBaseUrl = value;
        }

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
            m_CatalogApiUrl = m_CatalogApiUrl?.Trim() ?? string.Empty;
            m_CdnBaseUrl = m_CdnBaseUrl?.Trim() ?? string.Empty;
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
    public sealed class CloudProjectConfig
    {
        [SerializeField] private string m_ProviderId;
        [SerializeField] private string m_CredentialProfileName;
        [SerializeField] private string m_Bucket;
        [SerializeField] private string m_Region;
        [SerializeField] private string m_Endpoint;
        [SerializeField] private string m_RootPrefix;

        public string ProviderId
        {
            get => m_ProviderId;
            set => m_ProviderId = value;
        }

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

        internal void EnsureDefaults()
        {
            m_ProviderId = m_ProviderId?.Trim() ?? string.Empty;
            m_CredentialProfileName = m_CredentialProfileName?.Trim() ?? string.Empty;
            m_Bucket = m_Bucket?.Trim() ?? string.Empty;
            m_Region = m_Region?.Trim() ?? string.Empty;
            m_Endpoint = m_Endpoint?.Trim() ?? string.Empty;
            m_RootPrefix = m_RootPrefix?.Trim() ?? string.Empty;
        }
    }
}
