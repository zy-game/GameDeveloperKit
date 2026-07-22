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
}
