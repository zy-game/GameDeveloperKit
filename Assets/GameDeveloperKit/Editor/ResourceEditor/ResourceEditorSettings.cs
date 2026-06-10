using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;
using UnityEditorInternal;
using UnityEngine;

namespace GameDeveloperKit.ResourceEditor
{
    /// <summary>
    /// 定义 Resource Editor Settings 类型。
    /// </summary>
    public sealed class ResourceEditorSettings : ScriptableObject
    {
        /// <summary>
        /// 定义 Settings Path 常量。
        /// </summary>
        private const string SettingsPath = "ProjectSettings/GameDeveloperKitResourceEditorSettings.asset";
        /// <summary>
        /// 存储 Instance。
        /// </summary>
        private static ResourceEditorSettings s_Instance;

        [SerializeField] private List<ResourceEditorPackage> m_Packages;

        [SerializeField] private string m_ManifestOutputPath;

        [SerializeField] private ResourceBuildSettings m_BuildSettings;

        [SerializeField] private int m_SelectedPackageIndex;

        /// <summary>
        /// 存储 Packages。
        /// </summary>
        public List<ResourceEditorPackage> Packages => m_Packages;

        public string ManifestOutputPath
        {
            get => m_ManifestOutputPath;
            set => m_ManifestOutputPath = value;
        }

        /// <summary>
        /// 存储 Build Settings。
        /// </summary>
        public ResourceBuildSettings BuildSettings => m_BuildSettings;

        public int SelectedPackageIndex
        {
            get => m_SelectedPackageIndex;
            set => m_SelectedPackageIndex = value;
        }

        /// <summary>
        /// 加载 Or Create。
        /// </summary>
        /// <returns>执行结果。</returns>
        public static ResourceEditorSettings LoadOrCreate()
        {
            if (s_Instance != null)
            {
                s_Instance.EnsureDefaults();
                return s_Instance;
            }

            if (System.IO.File.Exists(SettingsPath))
            {
                s_Instance = InternalEditorUtility.LoadSerializedFileAndForget(SettingsPath)
                    .OfType<ResourceEditorSettings>()
                    .FirstOrDefault();
            }

            if (s_Instance == null)
            {
                s_Instance = CreateInstance<ResourceEditorSettings>();
            }

            s_Instance.hideFlags = HideFlags.HideAndDontSave;
            s_Instance.EnsureDefaults();
            if (System.IO.File.Exists(SettingsPath) is false)
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
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingsPath) ?? ".");
            InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { this }, SettingsPath, true);
        }

        /// <summary>
        /// 确保 Defaults。
        /// </summary>
        public void EnsureDefaults()
        {
            m_Packages ??= new List<ResourceEditorPackage>();

            if (string.IsNullOrWhiteSpace(m_ManifestOutputPath))
            {
                m_ManifestOutputPath = $"Assets/StreamingAssets/{ResourceSettings.MANIFEST_NAME}";
            }

            m_BuildSettings ??= new ResourceBuildSettings();

            foreach (var package in m_Packages)
            {
                package?.EnsureDefaults();
            }

            m_BuildSettings.EnsureDefaults(GetLegacyPackageVersion());

            if (m_Packages.Count == 0)
            {
                m_SelectedPackageIndex = -1;
                return;
            }

            if (m_SelectedPackageIndex < 0 || m_SelectedPackageIndex >= m_Packages.Count)
            {
                m_SelectedPackageIndex = 0;
            }
        }

        /// <summary>
        /// 获取 Legacy Package Version。
        /// </summary>
        /// <returns>执行结果。</returns>
        private string GetLegacyPackageVersion()
        {
            return m_Packages
                .Where(package => package != null && string.IsNullOrWhiteSpace(package.Version) is false)
                .Select(package => package.Version)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// 定义 Resource Build Scope 枚举。
    /// </summary>
    public enum ResourceBuildScope
    {
        SelectedPackage,
        AllPackages,
        HotUpdatePackages
    }

    /// <summary>
    /// 定义 Resource Build Compression 枚举。
    /// </summary>
    public enum ResourceBuildCompression
    {
        Default,
        Lz4,
        Uncompressed
    }

    /// <summary>
    /// 定义 Resource Build Settings 类型。
    /// </summary>
    [Serializable]
    public sealed class ResourceBuildSettings
    {
        /// <summary>
        /// 定义 OUTPUT ROOT 常量。
        /// </summary>
        public const string OUTPUT_ROOT = "Build/ResourceBundles";

        [SerializeField] private string m_OutputRoot = OUTPUT_ROOT;

        [SerializeField] private string m_Target;

        [SerializeField] private string m_Channel = "dev";

        [SerializeField] private bool m_CleanOutput = true;

        [SerializeField] private ResourceBuildCompression m_Compression = ResourceBuildCompression.Lz4;

        [SerializeField] private string m_ManifestFileName = ResourceSettings.MANIFEST_NAME;

        [SerializeField] private string m_Version;

        [SerializeField] private ResourceBuildScope m_Scope = ResourceBuildScope.SelectedPackage;

        public string OutputRoot
        {
            get => OUTPUT_ROOT;
            set => m_OutputRoot = value;
        }

        public string Target
        {
            get => string.Empty;
            set => m_Target = value;
        }

        public string Channel
        {
            get => m_Channel;
            set => m_Channel = value;
        }

        public bool CleanOutput
        {
            get => m_CleanOutput;
            set => m_CleanOutput = value;
        }

        public ResourceBuildCompression Compression
        {
            get => m_Compression;
            set => m_Compression = value;
        }

        public string ManifestFileName
        {
            get => ResourceSettings.MANIFEST_NAME;
            set => m_ManifestFileName = value;
        }

        public string Version
        {
            get => m_Version;
            set => m_Version = value;
        }

        public string ManifestVersion
        {
            get => m_Version;
            set => m_Version = value;
        }

        public ResourceBuildScope Scope
        {
            get => m_Scope;
            set => m_Scope = value;
        }

        /// <summary>
        /// 确保 Defaults。
        /// </summary>
        public void EnsureDefaults()
        {
            EnsureDefaults(null);
        }

        /// <summary>
        /// 确保 Defaults。
        /// </summary>
        /// <param name="versionFallback">version Fallback 参数。</param>
        public void EnsureDefaults(string versionFallback)
        {
            if (string.IsNullOrWhiteSpace(m_OutputRoot))
            {
                m_OutputRoot = OUTPUT_ROOT;
            }

            m_OutputRoot = OUTPUT_ROOT;
            m_Target = string.Empty;
            if (string.IsNullOrWhiteSpace(m_Channel))
            {
                m_Channel = "dev";
            }

            m_CleanOutput = true;
            m_ManifestFileName = ResourceSettings.MANIFEST_NAME;
            if (string.IsNullOrWhiteSpace(m_Version))
            {
                m_Version = string.IsNullOrWhiteSpace(versionFallback) ? "1.0.0" : versionFallback.Trim();
            }
        }
    }

    /// <summary>
    /// 定义 Resource Editor Package 类型。
    /// </summary>
    [Serializable]
    public sealed class ResourceEditorPackage
    {
        [SerializeField] private string m_Name = "NewPackage";

        [SerializeField] private string m_Version = "1.0.0";

        [SerializeField] private bool m_IsHotUpdate;

        [SerializeField] private string m_CollectorId;

        [SerializeField] private string m_BuildStrategyId;

        [SerializeField] private List<ResourceEditorBundle> m_Bundles;

        public string Name
        {
            get => m_Name;
            set => m_Name = value;
        }

        public string Version
        {
            get => m_Version;
            set => m_Version = value;
        }

        public bool IsHotUpdate
        {
            get => m_IsHotUpdate;
            set => m_IsHotUpdate = value;
        }

        public string CollectorId
        {
            get => m_CollectorId;
            set => m_CollectorId = value;
        }

        public string BuildStrategyId
        {
            get => m_BuildStrategyId;
            set => m_BuildStrategyId = value;
        }

        /// <summary>
        /// 存储 Bundles。
        /// </summary>
        public List<ResourceEditorBundle> Bundles => m_Bundles;

        /// <summary>
        /// 确保 Defaults。
        /// </summary>
        public void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(m_Name))
            {
                m_Name = "NewPackage";
            }

            if (string.IsNullOrWhiteSpace(m_Version))
            {
                m_Version = "1.0.0";
            }

            m_Bundles ??= new List<ResourceEditorBundle>();
            foreach (var bundle in m_Bundles)
            {
                bundle?.EnsureDefaults();
            }
        }
    }

    /// <summary>
    /// 定义 Resource Editor Bundle 类型。
    /// </summary>
    [Serializable]
    public sealed class ResourceEditorBundle
    {
        [SerializeField] private string m_Name = "NewBundle";

        [SerializeField] private string m_Group = "Default";

        [SerializeField] private List<string> m_Dependencies;

        [SerializeField] private List<string> m_Labels;

        [SerializeField] private List<string> m_AssetPaths;

        [SerializeField] private string m_CollectorId;

        [SerializeField] private string m_SourceFolder;

        [SerializeField] private string m_CollectorParameter;

        public string Name
        {
            get => m_Name;
            set => m_Name = value;
        }

        public string Group
        {
            get => m_Group;
            set => m_Group = value;
        }

        /// <summary>
        /// 存储 Dependencies。
        /// </summary>
        public List<string> Dependencies => m_Dependencies;

        /// <summary>
        /// 存储 Labels。
        /// </summary>
        public List<string> Labels => m_Labels;

        /// <summary>
        /// 存储 Asset Paths。
        /// </summary>
        public List<string> AssetPaths => m_AssetPaths;

        public string CollectorId
        {
            get => m_CollectorId;
            set => m_CollectorId = value;
        }

        public string SourceFolder
        {
            get => m_SourceFolder;
            set => m_SourceFolder = value;
        }

        public string CollectorParameter
        {
            get => m_CollectorParameter;
            set => m_CollectorParameter = value;
        }

        /// <summary>
        /// 确保 Defaults。
        /// </summary>
        public void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(m_Name))
            {
                m_Name = "NewBundle";
            }

            if (string.IsNullOrWhiteSpace(m_Group))
            {
                m_Group = "Default";
            }

            m_Dependencies ??= new List<string>();
            m_Labels ??= new List<string>();
            m_AssetPaths ??= new List<string>();
        }
    }
}
