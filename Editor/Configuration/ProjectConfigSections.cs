using System;
using UnityEngine;

namespace GameDeveloperKit.EditorConfiguration
{
    [Serializable]
    public sealed class UiPrefabStudioProjectConfig
    {
        public const string FigmaSource = "figma";
        public const string LanhuSource = "lanhu";
        public const string ManifestSource = "manifest";
        public const string DefaultSource = LanhuSource;

        public const string FitScaleMode = "fit";
        public const string FillScaleMode = "fill";
        public const string StretchScaleMode = "stretch";
        public const string DefaultScaleMode = FitScaleMode;

        public const string DefaultOutputRoot = "Assets/UI/Generated";
        public const string DefaultGeneratedCodeRoot = "Assets/UI/Generated/Code";
        public const string DefaultCodeNamespace = "GameDeveloperKit.UI.Generated";
        public const int DefaultTargetWidth = 1920;
        public const int DefaultTargetHeight = 1080;
        public const int DefaultMaxTextureSize = 2048;
        public const int DefaultLayerOrder = 200;

        [SerializeField] private string m_Source = DefaultSource;
        [SerializeField] private string m_LanhuProjectUrl = string.Empty;
        [SerializeField] private string m_FigmaFile = string.Empty;
        [SerializeField] private string m_OutputRoot = DefaultOutputRoot;
        [SerializeField] private int m_TargetWidth = DefaultTargetWidth;
        [SerializeField] private int m_TargetHeight = DefaultTargetHeight;
        [SerializeField] private string m_ScaleMode = DefaultScaleMode;
        [SerializeField] private int m_MaxTextureSize = DefaultMaxTextureSize;
        [SerializeField] private bool m_IncludeCanvas = true;
        [SerializeField] private bool m_ExtractSharedAssets = true;
        [SerializeField] private bool m_GenerateWindowCode = true;
        [SerializeField] private string m_GeneratedCodeRoot = DefaultGeneratedCodeRoot;
        [SerializeField] private string m_CodeNamespace = DefaultCodeNamespace;
        [SerializeField] private int m_LayerOrder = DefaultLayerOrder;
        [SerializeField] private bool m_CacheEnabled = true;

        public string Source
        {
            get => m_Source;
            set => m_Source = value;
        }

        public string LanhuProjectUrl
        {
            get => m_LanhuProjectUrl;
            set => m_LanhuProjectUrl = value;
        }

        public string FigmaFile
        {
            get => m_FigmaFile;
            set => m_FigmaFile = value;
        }

        public string OutputRoot
        {
            get => m_OutputRoot;
            set => m_OutputRoot = value;
        }

        public int TargetWidth
        {
            get => m_TargetWidth;
            set => m_TargetWidth = value;
        }

        public int TargetHeight
        {
            get => m_TargetHeight;
            set => m_TargetHeight = value;
        }

        public string ScaleMode
        {
            get => m_ScaleMode;
            set => m_ScaleMode = value;
        }

        public int MaxTextureSize
        {
            get => m_MaxTextureSize;
            set => m_MaxTextureSize = value;
        }

        public bool IncludeCanvas
        {
            get => m_IncludeCanvas;
            set => m_IncludeCanvas = value;
        }

        public bool ExtractSharedAssets
        {
            get => m_ExtractSharedAssets;
            set => m_ExtractSharedAssets = value;
        }

        public bool GenerateWindowCode
        {
            get => m_GenerateWindowCode;
            set => m_GenerateWindowCode = value;
        }

        public string GeneratedCodeRoot
        {
            get => m_GeneratedCodeRoot;
            set => m_GeneratedCodeRoot = value;
        }

        public string CodeNamespace
        {
            get => m_CodeNamespace;
            set => m_CodeNamespace = value;
        }

        public int LayerOrder
        {
            get => m_LayerOrder;
            set => m_LayerOrder = value;
        }

        public bool CacheEnabled
        {
            get => m_CacheEnabled;
            set => m_CacheEnabled = value;
        }

        internal void EnsureDefaults()
        {
            m_Source = IsSupportedSource(m_Source) ? m_Source : DefaultSource;
            m_LanhuProjectUrl = m_LanhuProjectUrl?.Trim() ?? string.Empty;
            m_FigmaFile = m_FigmaFile?.Trim() ?? string.Empty;
            m_OutputRoot = string.IsNullOrWhiteSpace(m_OutputRoot)
                ? DefaultOutputRoot
                : m_OutputRoot.Trim().Replace('\\', '/').TrimEnd('/');
            m_GeneratedCodeRoot = string.IsNullOrWhiteSpace(m_GeneratedCodeRoot)
                ? DefaultGeneratedCodeRoot
                : m_GeneratedCodeRoot.Trim().Replace('\\', '/').TrimEnd('/');
            m_CodeNamespace = string.IsNullOrWhiteSpace(m_CodeNamespace)
                ? DefaultCodeNamespace
                : m_CodeNamespace.Trim();
            m_TargetWidth = m_TargetWidth > 0 ? m_TargetWidth : DefaultTargetWidth;
            m_TargetHeight = m_TargetHeight > 0 ? m_TargetHeight : DefaultTargetHeight;
            m_ScaleMode = IsSupportedScaleMode(m_ScaleMode) ? m_ScaleMode : DefaultScaleMode;
            m_MaxTextureSize = IsSupportedTextureSize(m_MaxTextureSize)
                ? m_MaxTextureSize
                : DefaultMaxTextureSize;
        }

        internal static bool IsSupportedSource(string value)
        {
            return string.Equals(value, FigmaSource, StringComparison.Ordinal) ||
                   string.Equals(value, LanhuSource, StringComparison.Ordinal) ||
                   string.Equals(value, ManifestSource, StringComparison.Ordinal);
        }

        internal static bool IsSupportedScaleMode(string value)
        {
            return string.Equals(value, FitScaleMode, StringComparison.Ordinal) ||
                   string.Equals(value, FillScaleMode, StringComparison.Ordinal) ||
                   string.Equals(value, StretchScaleMode, StringComparison.Ordinal);
        }

        internal static bool IsSupportedTextureSize(int value)
        {
            return value == 1024 || value == 2048 || value == 4096 || value == 8192;
        }
    }

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
        [SerializeField] private string m_CdnBaseUrl;

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

        public string CdnBaseUrl
        {
            get => m_CdnBaseUrl;
            set => m_CdnBaseUrl = value;
        }

        internal void EnsureDefaults()
        {
            m_CredentialProfileName = m_CredentialProfileName?.Trim() ?? string.Empty;
            m_Bucket = m_Bucket?.Trim() ?? string.Empty;
            m_Region = m_Region?.Trim() ?? string.Empty;
            m_Endpoint = m_Endpoint?.Trim() ?? string.Empty;
            m_RootPrefix = m_RootPrefix?.Trim() ?? string.Empty;
            m_CdnBaseUrl = m_CdnBaseUrl?.Trim() ?? string.Empty;
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

        public string CdnBaseUrl
        {
            get => ActiveConnection?.CdnBaseUrl ?? string.Empty;
            set => SetActiveConnectionValue(connection => connection.CdnBaseUrl = value);
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
