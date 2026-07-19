using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace GameDeveloperKit.ResourceEditor.Authoring
{
    internal static class BuiltinConstants
    {
        public const string ExplicitCollectorId = "explicit-assets";

        public const string FolderCollectorId = "folder-assets";

        public const string ResourcesCollectorId = "unity-resources";

        public static string PackageName => ResourceConstants.BUILTIN_PACKAGE_NAME;

        public static string ResourcesGroupName => "Resources";

        public static string LocalPackageName => "LOCAL";

        public static string LocalBundleName => "LOCAL";

        public static bool IsBuiltinPackage(GameDeveloperKit.ResourceEditor.Authoring.Package package)
        {
            return package != null && package.Name == PackageName;
        }

        public static bool IsResourcesGroup(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            return bundle != null && bundle.ProviderId == ResourceProviderIds.Resources;
        }

        public static bool IsLocalPackage(GameDeveloperKit.ResourceEditor.Authoring.Package package)
        {
            return package != null && string.Equals(package.Name, LocalPackageName, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// 定义 Resource Editor Settings 类型。
    /// </summary>
    [MovedFrom(true, sourceNamespace: "GameDeveloperKit.ResourceEditor", sourceAssembly: "GameDeveloperKit.Editor", sourceClassName: "ResourceEditorSettings")]
    public sealed class Settings : ScriptableObject
    {
        /// <summary>
        /// 定义 Settings Path 常量。
        /// </summary>
        internal const string SettingsPath = "ProjectSettings/GameDeveloperKitResourceEditorSettings.asset";
        /// <summary>
        /// 存储 Instance。
        /// </summary>
        private static Settings s_Instance;

        [SerializeField] private List<GameDeveloperKit.ResourceEditor.Authoring.Package> m_Packages;

        [SerializeField] private string m_ManifestOutputPath;

        [SerializeField] private GameDeveloperKit.ResourceEditor.Build.Settings m_BuildSettings;

        [SerializeField] private int m_SelectedPackageIndex;

        /// <summary>
        /// 存储 Packages。
        /// </summary>
        public List<GameDeveloperKit.ResourceEditor.Authoring.Package> Packages => m_Packages;

        public string ManifestOutputPath
        {
            get => m_ManifestOutputPath;
            set => m_ManifestOutputPath = value;
        }

        /// <summary>
        /// 存储 Build Settings。
        /// </summary>
        public GameDeveloperKit.ResourceEditor.Build.Settings BuildSettings => m_BuildSettings;

        public int SelectedPackageIndex
        {
            get => m_SelectedPackageIndex;
            set => m_SelectedPackageIndex = value;
        }

        /// <summary>
        /// 加载 Or Create。
        /// </summary>
        /// <returns>执行结果。</returns>
        public static Settings LoadOrCreate()
        {
            if (TryLoadExisting(out var settings))
            {
                settings.EnsureDefaults();
                return settings;
            }

            s_Instance = CreateInstance<Settings>();
            s_Instance.hideFlags = HideFlags.HideAndDontSave;
            s_Instance.EnsureDefaults();
            s_Instance.SaveSettings();

            return s_Instance;
        }

        internal static bool TryLoadExisting(out Settings settings)
        {
            if (s_Instance != null)
            {
                settings = s_Instance;
                return true;
            }

            if (System.IO.File.Exists(SettingsPath) is false)
            {
                settings = null;
                return false;
            }

            s_Instance = InternalEditorUtility.LoadSerializedFileAndForget(SettingsPath)
                .OfType<Settings>()
                .FirstOrDefault();
            if (s_Instance == null)
            {
                settings = null;
                return false;
            }

            s_Instance.hideFlags = HideFlags.HideAndDontSave;
            settings = s_Instance;
            return true;
        }

        internal static bool IsLoadedInstance(Settings settings)
        {
            return settings != null && ReferenceEquals(s_Instance, settings);
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
            m_Packages ??= new List<GameDeveloperKit.ResourceEditor.Authoring.Package>();

            if (string.IsNullOrWhiteSpace(m_ManifestOutputPath))
            {
                m_ManifestOutputPath = $"Assets/StreamingAssets/{ResourceSettings.MANIFEST_NAME}";
            }

            m_BuildSettings ??= new GameDeveloperKit.ResourceEditor.Build.Settings();

            foreach (var package in m_Packages)
            {
                package?.EnsureDefaults();
                EnsureUniqueBundleNames(package);
            }

            EnsureBuiltinPackage();
            EnsureLocalPackage();
            m_BuildSettings.EnsureDefaults();

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

        private void EnsureBuiltinPackage()
        {
            var builtinPackages = m_Packages
                .Where(GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsBuiltinPackage)
                .ToList();
            var builtinPackage = builtinPackages.FirstOrDefault();
            if (builtinPackage == null)
            {
                builtinPackage = new GameDeveloperKit.ResourceEditor.Authoring.Package();
                m_Packages.Insert(0, builtinPackage);
            }

            foreach (var duplicate in builtinPackages.Skip(1))
            {
                m_Packages.Remove(duplicate);
            }

            builtinPackage.Name = GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.PackageName;
            builtinPackage.IsHotUpdate = false;
            if (string.IsNullOrWhiteSpace(builtinPackage.BuildStrategyId))
            {
                builtinPackage.BuildStrategyId = "single-bundle";
            }
            builtinPackage.EnsureDefaults();

            var resourcesGroups = builtinPackage.Bundles
                .Where(GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsResourcesGroup)
                .ToList();
            var resourcesGroup = resourcesGroups.FirstOrDefault();
            if (resourcesGroup == null)
            {
                resourcesGroup = new GameDeveloperKit.ResourceEditor.Authoring.Bundle();
                builtinPackage.Bundles.Insert(0, resourcesGroup);
            }

            foreach (var duplicate in resourcesGroups.Skip(1))
            {
                builtinPackage.Bundles.Remove(duplicate);
            }

            resourcesGroup.Name = GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.ResourcesGroupName;
            resourcesGroup.Group = GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.ResourcesGroupName;
            resourcesGroup.ProviderId = ResourceProviderIds.Resources;
            resourcesGroup.CollectorId = GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.ResourcesCollectorId;
            resourcesGroup.SourceFolder = string.Empty;
            resourcesGroup.EnsureDefaults();

            foreach (var bundle in builtinPackage.Bundles.Where(bundle => bundle != null && ReferenceEquals(bundle, resourcesGroup) is false))
            {
                bundle.ProviderId = ResourceProviderIds.AssetBundle;
                foreach (var entry in bundle.Entries.Where(entry => entry != null))
                {
                    entry.ProviderId = ResourceProviderIds.AssetBundle;
                }
            }
        }

        private void EnsureLocalPackage()
        {
            var localPackages = m_Packages
                .Where(GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsLocalPackage)
                .ToList();
            var localPackage = localPackages.FirstOrDefault();
            if (localPackage == null)
            {
                localPackage = new GameDeveloperKit.ResourceEditor.Authoring.Package
                {
                    Name = GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.LocalPackageName
                };
                var insertIndex = Math.Min(1, m_Packages.Count);
                m_Packages.Insert(insertIndex, localPackage);
            }

            foreach (var duplicate in localPackages.Skip(1))
            {
                m_Packages.Remove(duplicate);
            }

            localPackage.Name = GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.LocalPackageName;
            localPackage.IsHotUpdate = false;
            if (string.IsNullOrWhiteSpace(localPackage.BuildStrategyId))
            {
                localPackage.BuildStrategyId = "single-bundle";
            }

            localPackage.EnsureDefaults();
            if (localPackage.Bundles.Count == 0)
            {
                var bundle = new GameDeveloperKit.ResourceEditor.Authoring.Bundle
                {
                    Name = GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.LocalBundleName,
                    Group = GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.LocalBundleName,
                    ProviderId = ResourceProviderIds.AssetBundle
                };
                bundle.EnsureDefaults();
                localPackage.Bundles.Add(bundle);
            }

            foreach (var bundle in localPackage.Bundles.Where(bundle => bundle != null))
            {
                bundle.ProviderId = ResourceProviderIds.AssetBundle;
                if (string.IsNullOrWhiteSpace(bundle.CollectorId))
                {
                    bundle.CollectorId = GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.ExplicitCollectorId;
                }
                foreach (var entry in bundle.Entries.Where(entry => entry != null))
                {
                    entry.ProviderId = ResourceProviderIds.AssetBundle;
                }
            }

            EnsureUniqueBundleNames(localPackage);
        }

        private static void EnsureUniqueBundleNames(GameDeveloperKit.ResourceEditor.Authoring.Package package)
        {
            if (package?.Bundles == null)
            {
                return;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            var index = 1;
            foreach (var bundle in package.Bundles.Where(bundle => bundle != null))
            {
                var name = string.IsNullOrWhiteSpace(bundle.Group) ? bundle.Name : bundle.Group;
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = NextBundleName(names, ref index);
                }

                if (names.Add(name) is false)
                {
                    name = NextBundleName(names, ref index);
                    names.Add(name);
                }

                bundle.Name = name;
                bundle.Group = name;
            }
        }

        private static string NextBundleName(ICollection<string> existingNames, ref int index)
        {
            while (true)
            {
                var name = index == 1 ? "Default" : $"Group{index}";
                index++;
                if (existingNames.Contains(name) is false)
                {
                    return name;
                }
            }
        }

    }

    /// <summary>
    /// 定义 Resource Build Scope 枚举。
    /// </summary>
}

namespace GameDeveloperKit.ResourceEditor.Build
{
    [MovedFrom(true, sourceNamespace: "GameDeveloperKit.ResourceEditor", sourceAssembly: "GameDeveloperKit.Editor", sourceClassName: "ResourceBuildScope")]
    public enum Scope
    {
        SelectedPackage,
        AllPackages,
        HotUpdatePackages
    }

    /// <summary>
    /// 定义 Resource Build Compression 枚举。
    /// </summary>
    [MovedFrom(true, sourceNamespace: "GameDeveloperKit.ResourceEditor", sourceAssembly: "GameDeveloperKit.Editor", sourceClassName: "ResourceBuildCompression")]
    public enum Compression
    {
        Default,
        Lz4,
        Uncompressed
    }

    /// <summary>
    /// 定义 Resource Build Settings 类型。
    /// </summary>
    [Serializable]
    [MovedFrom(true, sourceNamespace: "GameDeveloperKit.ResourceEditor", sourceAssembly: "GameDeveloperKit.Editor", sourceClassName: "ResourceBuildSettings")]
    public sealed class Settings
    {
        /// <summary>
        /// 定义 OUTPUT ROOT 常量。
        /// </summary>
        public const string OUTPUT_ROOT = "Build/ResourceBundles";

        public const string NoChannelSelection = "__none__";

        [SerializeField] private string m_OutputRoot = OUTPUT_ROOT;

        [SerializeField] private string m_Target;

        [SerializeField] private string m_Channel = ResourceSettings.DEFAULT_CHANNEL_NAME;

        [SerializeField] private bool m_CleanOutput = true;

        [SerializeField] private Compression m_Compression = Compression.Lz4;

        [SerializeField] private string m_ManifestFileName = ResourceSettings.MANIFEST_NAME;

        [SerializeField] private string m_Version;

        [SerializeField] private Scope m_Scope = Scope.SelectedPackage;

        public string OutputRoot
        {
            get => m_OutputRoot;
            set => m_OutputRoot = value;
        }

        public string Target
        {
            get => m_Target;
            set => m_Target = value;
        }

        public string Channel
        {
            get => m_Channel;
            set => m_Channel = value;
        }

        public IReadOnlyList<string> Channels
        {
            get
            {
                return (m_Channel ?? string.Empty)
                    .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(channel => channel.Trim())
                    .Where(channel => string.IsNullOrWhiteSpace(channel) is false && IsNoChannelSelection(channel) is false)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }
        }

        public bool CleanOutput
        {
            get => m_CleanOutput;
            set => m_CleanOutput = value;
        }

        public Compression Compression
        {
            get => m_Compression;
            set => m_Compression = value;
        }

        public string ManifestFileName
        {
            get => m_ManifestFileName;
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

        public Scope Scope
        {
            get => m_Scope;
            set => m_Scope = value;
        }

        /// <summary>
        /// 确保 Defaults。
        /// </summary>
        public void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(m_OutputRoot))
            {
                m_OutputRoot = OUTPUT_ROOT;
            }

            if (string.IsNullOrWhiteSpace(m_Target))
            {
                m_Target = EditorUserBuildSettings.activeBuildTarget.ToString();
            }

            if (string.IsNullOrWhiteSpace(m_Channel))
            {
                m_Channel = ResourceSettings.DEFAULT_CHANNEL_NAME;
            }

            if (string.IsNullOrWhiteSpace(m_ManifestFileName))
            {
                m_ManifestFileName = ResourceSettings.MANIFEST_NAME;
            }

            if (string.IsNullOrWhiteSpace(m_Version))
            {
                m_Version = "1.0.0";
            }
        }

        internal Settings Copy()
        {
            return new Settings
            {
                OutputRoot = OutputRoot,
                Target = Target,
                Channel = Channel,
                CleanOutput = CleanOutput,
                Compression = Compression,
                ManifestFileName = ManifestFileName,
                ManifestVersion = ManifestVersion,
                Scope = Scope
            };
        }

        public static bool IsNoChannelSelection(string value)
        {
            return string.Equals(value?.Trim(), NoChannelSelection, StringComparison.Ordinal);
        }
    }

}

namespace GameDeveloperKit.ResourceEditor.Authoring
{
    /// <summary>
    /// 定义 Resource Editor Package 类型。
    /// </summary>
    [Serializable]
    [MovedFrom(true, sourceNamespace: "GameDeveloperKit.ResourceEditor", sourceAssembly: "GameDeveloperKit.Editor", sourceClassName: "ResourceEditorPackage")]
    public sealed class Package
    {
        [SerializeField] private string m_Name = "NewPackage";

        [SerializeField] private string m_Version = "1.0.0";

        [SerializeField] private bool m_IsHotUpdate;

        [SerializeField] private string m_BuildStrategyId;

        [SerializeField] private List<GameDeveloperKit.ResourceEditor.Authoring.Bundle> m_Bundles;

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

        public string BuildStrategyId
        {
            get => m_BuildStrategyId;
            set => m_BuildStrategyId = value;
        }

        /// <summary>
        /// 存储 Bundles。
        /// </summary>
        public List<GameDeveloperKit.ResourceEditor.Authoring.Bundle> Bundles => m_Bundles;

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

            m_Bundles ??= new List<GameDeveloperKit.ResourceEditor.Authoring.Bundle>();
            foreach (var bundle in m_Bundles)
            {
                bundle?.EnsureDefaults();
            }
        }
    }

    /// <summary>
    /// 资源条目的剔除方式。
    /// </summary>
    [MovedFrom(true, sourceNamespace: "GameDeveloperKit.ResourceEditor", sourceAssembly: "GameDeveloperKit.Editor", sourceClassName: "ResourceEntryExcludeKind")]
    public enum EntryExcludeKind
    {
        /// <summary>
        /// 正常参与打包。
        /// </summary>
        None,

        /// <summary>
        /// 手动排除，不参与打包，可恢复。
        /// </summary>
        Excluded,

        /// <summary>
        /// 标记删除，不参与打包，可恢复。
        /// </summary>
        Deleted
    }

    /// <summary>
    /// 定义 Resource Editor Asset Entry 类型。
    /// </summary>
    [Serializable]
    [MovedFrom(true, sourceNamespace: "GameDeveloperKit.ResourceEditor", sourceAssembly: "GameDeveloperKit.Editor", sourceClassName: "ResourceEditorAssetEntry")]
    public sealed class AssetEntry
    {
        [SerializeField] private string m_Guid;

        [SerializeField] private string m_AssetPath;

        [SerializeField] private string m_Location;

        [SerializeField] private string m_TypeName;

        [SerializeField] private List<string> m_Labels;

        [SerializeField] private string m_ProviderId;

        [SerializeField] private EntryExcludeKind m_ExcludeKind;

        public string Guid
        {
            get => m_Guid;
            set => m_Guid = NormalizeGuid(value);
        }

        public string AssetPath
        {
            get => m_AssetPath;
            set => m_AssetPath = NormalizePath(value);
        }

        /// <summary>
        /// 条目的排除方式：正常参与打包、被排除或被标记删除。
        /// 被排除或标记删除的条目不参与预览、清单与构建，仅在忽略列表中展示，可恢复。
        /// </summary>
        public EntryExcludeKind ExcludeKind
        {
            get => m_ExcludeKind;
            set => m_ExcludeKind = value;
        }

        /// <summary>
        /// 是否被剔除出打包（排除或标记删除）。
        /// </summary>
        public bool Excluded => m_ExcludeKind != GameDeveloperKit.ResourceEditor.Authoring.EntryExcludeKind.None;

        public string Location
        {
            get => m_Location;
            set => m_Location = value;
        }

        public string TypeName
        {
            get => m_TypeName;
            set => m_TypeName = value;
        }

        public List<string> Labels => m_Labels;

        public string ProviderId
        {
            get => ResourceProviderIds.Normalize(m_ProviderId);
            set => m_ProviderId = ResourceProviderIds.Normalize(value);
        }

        public void EnsureDefaults(string providerId)
        {
            m_Guid = NormalizeGuid(m_Guid);
            m_AssetPath = NormalizePath(m_AssetPath);
            m_Labels ??= new List<string>();
            m_ProviderId = ResourceProviderIds.Normalize(string.IsNullOrWhiteSpace(m_ProviderId) ? providerId : m_ProviderId);
        }

        private static string NormalizePath(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace('\\', '/').Trim();
        }

        private static string NormalizeGuid(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }

    /// <summary>
    /// 定义 Resource Editor Bundle 类型。
    /// </summary>
    [Serializable]
    [MovedFrom(true, sourceNamespace: "GameDeveloperKit.ResourceEditor", sourceAssembly: "GameDeveloperKit.Editor", sourceClassName: "ResourceEditorBundle")]
    public sealed class Bundle
    {
        [SerializeField] private string m_Name = "NewBundle";

        [SerializeField] private string m_Group = "Default";

        [SerializeField] private List<string> m_Dependencies;

        [SerializeField] private List<string> m_Labels;

        [SerializeField] private string m_ProviderId = ResourceProviderIds.AssetBundle;

        [SerializeField] private List<GameDeveloperKit.ResourceEditor.Authoring.AssetEntry> m_Entries;

        [SerializeField] private string m_CollectorId;

        [SerializeField] private string m_SourceFolder;

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

        public string ProviderId
        {
            get => ResourceProviderIds.Normalize(m_ProviderId);
            set => m_ProviderId = ResourceProviderIds.Normalize(value);
        }

        public List<GameDeveloperKit.ResourceEditor.Authoring.AssetEntry> Entries => m_Entries;

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
            m_Entries ??= new List<GameDeveloperKit.ResourceEditor.Authoring.AssetEntry>();
            m_ProviderId = ResourceProviderIds.Normalize(m_ProviderId);
            m_SourceFolder = NormalizePath(m_SourceFolder);
            if (ResourceProviderIds.IsResources(m_ProviderId))
            {
                m_CollectorId = GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.ResourcesCollectorId;
                m_SourceFolder = string.Empty;
            }
            else if (string.IsNullOrWhiteSpace(m_CollectorId))
            {
                m_CollectorId = GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.ExplicitCollectorId;
                m_SourceFolder = string.Empty;
            }
            else if (string.Equals(m_CollectorId, GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.FolderCollectorId, StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(m_SourceFolder))
                {
                    m_CollectorId = GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.ExplicitCollectorId;
                }
            }
            else if (string.Equals(m_CollectorId, GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.ExplicitCollectorId, StringComparison.Ordinal))
            {
                m_SourceFolder = string.Empty;
            }
            foreach (var entry in m_Entries.Where(entry => entry != null))
            {
                entry.EnsureDefaults(m_ProviderId);
            }
            RemoveDuplicateEntries();
        }

        private static string NormalizePath(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace('\\', '/').Trim();
        }

        private void RemoveDuplicateEntries()
        {
            var knownPaths = new HashSet<string>(StringComparer.Ordinal);
            for (var i = m_Entries.Count - 1; i >= 0; i--)
            {
                var entry = m_Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.AssetPath) || knownPaths.Add(entry.AssetPath) is false)
                {
                    m_Entries.RemoveAt(i);
                }
            }
        }
    }
}
