using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace GameDeveloperKit.ResourceEditor
{
    internal static class ResourceEditorBuiltinConstants
    {
        public const string ResourcesCollectorId = "unity-resources";

        public static string PackageName => ResourceConstants.BUILTIN_PACKAGE_NAME;

        public static string ResourcesGroupName => "Resources";

        public static string LocalPackageName => "LOCAL";

        public static bool IsBuiltinPackage(ResourceEditorPackage package)
        {
            return package != null && package.Name == PackageName;
        }

        public static bool IsResourcesGroup(ResourceEditorBundle bundle)
        {
            return bundle != null && bundle.ProviderId == ResourceProviderIds.Resources;
        }

        public static bool IsLocalPackage(ResourceEditorPackage package)
        {
            return package != null && string.Equals(package.Name, LocalPackageName, StringComparison.Ordinal);
        }
    }

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
                EnsureUniqueBundleNames(package);
            }

            EnsureBuiltinPackage();
            EnsureLocalPackage();
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

        private void EnsureBuiltinPackage()
        {
            var builtinPackages = m_Packages
                .Where(ResourceEditorBuiltinConstants.IsBuiltinPackage)
                .ToList();
            var builtinPackage = builtinPackages.FirstOrDefault();
            if (builtinPackage == null)
            {
                builtinPackage = new ResourceEditorPackage();
                m_Packages.Insert(0, builtinPackage);
            }

            foreach (var duplicate in builtinPackages.Skip(1))
            {
                m_Packages.Remove(duplicate);
            }

            builtinPackage.Name = ResourceEditorBuiltinConstants.PackageName;
            builtinPackage.IsHotUpdate = false;
            if (string.Equals(builtinPackage.CollectorId, ResourceEditorBuiltinConstants.ResourcesCollectorId, StringComparison.Ordinal))
            {
                builtinPackage.CollectorId = ResourceEditorBuiltinConstants.ResourcesCollectorId;
            }
            if (string.IsNullOrWhiteSpace(builtinPackage.BuildStrategyId))
            {
                builtinPackage.BuildStrategyId = "single-bundle";
            }
            builtinPackage.EnsureDefaults();

            var resourcesGroups = builtinPackage.Bundles
                .Where(ResourceEditorBuiltinConstants.IsResourcesGroup)
                .ToList();
            var resourcesGroup = resourcesGroups.FirstOrDefault();
            if (resourcesGroup == null)
            {
                resourcesGroup = new ResourceEditorBundle();
                builtinPackage.Bundles.Insert(0, resourcesGroup);
            }

            foreach (var duplicate in resourcesGroups.Skip(1))
            {
                builtinPackage.Bundles.Remove(duplicate);
            }

            resourcesGroup.Name = ResourceEditorBuiltinConstants.ResourcesGroupName;
            resourcesGroup.Group = ResourceEditorBuiltinConstants.ResourcesGroupName;
            resourcesGroup.ProviderId = ResourceProviderIds.Resources;
            resourcesGroup.SourceFolder = string.Empty;
            resourcesGroup.CollectorParameter = string.Empty;
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
                .Where(ResourceEditorBuiltinConstants.IsLocalPackage)
                .ToList();
            var localPackage = localPackages.FirstOrDefault();
            if (localPackage == null)
            {
                localPackage = new ResourceEditorPackage
                {
                    Name = ResourceEditorBuiltinConstants.LocalPackageName
                };
                var insertIndex = Math.Min(1, m_Packages.Count);
                m_Packages.Insert(insertIndex, localPackage);
            }

            foreach (var duplicate in localPackages.Skip(1))
            {
                m_Packages.Remove(duplicate);
            }

            localPackage.Name = ResourceEditorBuiltinConstants.LocalPackageName;
            localPackage.IsHotUpdate = false;
            if (string.IsNullOrWhiteSpace(localPackage.BuildStrategyId))
            {
                localPackage.BuildStrategyId = "single-bundle";
            }

            localPackage.EnsureDefaults();
            if (localPackage.Bundles.Count == 0)
            {
                var bundle = new ResourceEditorBundle
                {
                    Name = "Default",
                    Group = "Default",
                    ProviderId = ResourceProviderIds.AssetBundle
                };
                bundle.EnsureDefaults();
                localPackage.Bundles.Add(bundle);
            }

            foreach (var bundle in localPackage.Bundles.Where(bundle => bundle != null))
            {
                bundle.ProviderId = ResourceProviderIds.AssetBundle;
                foreach (var entry in bundle.Entries.Where(entry => entry != null))
                {
                    entry.ProviderId = ResourceProviderIds.AssetBundle;
                }
            }

            EnsureUniqueBundleNames(localPackage);
        }

        private static void EnsureUniqueBundleNames(ResourceEditorPackage package)
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

        public const string NoChannelSelection = "__none__";

        [SerializeField] private string m_OutputRoot = OUTPUT_ROOT;

        [SerializeField] private string m_Target;

        [SerializeField] private string m_Channel = GameDeveloperKit.ResourcePublisher.ResourcePublisherSettings.DeveloperChannelName;

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
                m_Channel = GameDeveloperKit.ResourcePublisher.ResourcePublisherSettings.DeveloperChannelName;
            }

            m_CleanOutput = true;
            m_ManifestFileName = ResourceSettings.MANIFEST_NAME;
            if (string.IsNullOrWhiteSpace(m_Version))
            {
                m_Version = string.IsNullOrWhiteSpace(versionFallback) ? "1.0.0" : versionFallback.Trim();
            }
        }

        public static bool IsNoChannelSelection(string value)
        {
            return string.Equals(value?.Trim(), NoChannelSelection, StringComparison.Ordinal);
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
                bundle?.EnsureDefaults(m_CollectorId);
            }
        }
    }

    /// <summary>
    /// 资源条目的剔除方式。
    /// </summary>
    public enum ResourceEntryExcludeKind
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
    public sealed class ResourceEditorAssetEntry
    {
        [SerializeField] private string m_AssetPath;

        [SerializeField] private string m_Location;

        [SerializeField] private string m_TypeName;

        [SerializeField] private List<string> m_Labels;

        [SerializeField] private string m_ProviderId;

        [SerializeField] private ResourceEntryExcludeKind m_ExcludeKind;

        public string AssetPath
        {
            get => m_AssetPath;
            set => m_AssetPath = NormalizePath(value);
        }

        /// <summary>
        /// 条目的排除方式：正常参与打包、被排除或被标记删除。
        /// 被排除或标记删除的条目不参与预览、清单与构建，仅在忽略列表中展示，可恢复。
        /// </summary>
        public ResourceEntryExcludeKind ExcludeKind
        {
            get => m_ExcludeKind;
            set => m_ExcludeKind = value;
        }

        /// <summary>
        /// 是否被剔除出打包（排除或标记删除）。
        /// </summary>
        public bool Excluded => m_ExcludeKind != ResourceEntryExcludeKind.None;

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
            m_AssetPath = NormalizePath(m_AssetPath);
            m_Labels ??= new List<string>();
            m_ProviderId = ResourceProviderIds.Normalize(string.IsNullOrWhiteSpace(m_ProviderId) ? providerId : m_ProviderId);
        }

        private static string NormalizePath(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace('\\', '/').Trim();
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

        [SerializeField] private string m_ProviderId;

        [SerializeField] private List<ResourceEditorAssetEntry> m_Entries;

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

        public string ProviderId
        {
            get => ResourceProviderIds.Normalize(m_ProviderId);
            set => m_ProviderId = ResourceProviderIds.Normalize(value);
        }

        public List<ResourceEditorAssetEntry> Entries => m_Entries;

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
            EnsureDefaults(null);
        }

        public void EnsureDefaults(string packageCollectorId)
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
            m_Entries ??= new List<ResourceEditorAssetEntry>();
            var legacyCollectorId = ResolveLegacyCollectorId(packageCollectorId);
            m_ProviderId = ResourceProviderIds.Normalize(string.IsNullOrWhiteSpace(m_ProviderId) ? InferLegacyProviderId(legacyCollectorId) : m_ProviderId);
            MigrateLegacyInputsToEntries(legacyCollectorId);
            foreach (var entry in m_Entries.Where(entry => entry != null))
            {
                entry.EnsureDefaults(m_ProviderId);
            }
            RemoveDuplicateEntries();
        }

        private string ResolveLegacyCollectorId(string packageCollectorId)
        {
            return string.IsNullOrWhiteSpace(m_CollectorId) ? packageCollectorId : m_CollectorId;
        }

        private string InferLegacyProviderId(string legacyCollectorId)
        {
            return string.Equals(legacyCollectorId, ResourceEditorBuiltinConstants.ResourcesCollectorId, StringComparison.Ordinal)
                ? ResourceProviderIds.Resources
                : ResourceProviderIds.AssetBundle;
        }

        private void MigrateLegacyInputsToEntries(string legacyCollectorId)
        {
            foreach (var assetPath in ResolveLegacyAssetPaths(legacyCollectorId))
            {
                AddEntryIfMissing(assetPath);
            }
        }

        private IEnumerable<string> ResolveLegacyAssetPaths(string legacyCollectorId)
        {
            if (string.Equals(legacyCollectorId, ResourceEditorBuiltinConstants.ResourcesCollectorId, StringComparison.Ordinal))
            {
                foreach (var resource in new UnityResourcesCollector().Collect(null, this))
                {
                    if (resource != null && string.IsNullOrWhiteSpace(resource.AssetPath) is false)
                    {
                        yield return resource.AssetPath;
                    }
                }

                yield break;
            }

            foreach (var assetPath in m_AssetPaths.Where(path => string.IsNullOrWhiteSpace(path) is false))
            {
                foreach (var expandedPath in ExpandAssetPath(assetPath))
                {
                    yield return expandedPath;
                }
            }

            foreach (var folder in ResolveLegacyFolders())
            {
                foreach (var expandedPath in ExpandAssetPath(folder))
                {
                    yield return expandedPath;
                }
            }
        }

        private IEnumerable<string> ResolveLegacyFolders()
        {
            if (AssetDatabase.IsValidFolder(m_SourceFolder))
            {
                yield return m_SourceFolder;
            }

            foreach (var path in (m_CollectorParameter ?? string.Empty).Split(new[] { '\r', '\n', ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var folder = path.Trim();
                if (AssetDatabase.IsValidFolder(folder))
                {
                    yield return folder;
                }
            }
        }

        private static IEnumerable<string> ExpandAssetPath(string assetPath)
        {
            var normalized = assetPath.Replace('\\', '/').Trim();
            if (AssetDatabase.IsValidFolder(normalized) is false)
            {
                yield return normalized;
                yield break;
            }

            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { normalized }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                yield return path.Replace('\\', '/');
            }
        }

        private void AddEntryIfMissing(string assetPath)
        {
            var normalizedPath = string.IsNullOrWhiteSpace(assetPath) ? string.Empty : assetPath.Replace('\\', '/').Trim();
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return;
            }

            if (m_Entries.Any(entry => entry != null && string.Equals(entry.AssetPath, normalizedPath, StringComparison.Ordinal)))
            {
                return;
            }

            m_Entries.Add(CreateEntry(normalizedPath));
        }

        private ResourceEditorAssetEntry CreateEntry(string assetPath)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var labels = asset == null ? Array.Empty<string>() : AssetDatabase.GetLabels(asset);
            var location = ResourceProviderIds.IsResources(m_ProviderId)
                ? UnityResourcesCollector.ToResourcesLocation(assetPath)
                : ExplicitAssetResourceCollector.NormalizeLocation(assetPath);

            var entry = new ResourceEditorAssetEntry
            {
                AssetPath = assetPath,
                Location = location,
                TypeName = type?.Name ?? string.Empty,
                ProviderId = m_ProviderId
            };
            entry.EnsureDefaults(m_ProviderId);
            entry.Labels.AddRange(labels.Where(label => string.IsNullOrWhiteSpace(label) is false).Distinct(StringComparer.Ordinal));
            return entry;
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
