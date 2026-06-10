using System;
using System.Collections.Generic;
using UnityEditor;

namespace GameDeveloperKit.ResourceEditor
{
    /// <summary>
    /// 定义 Resource Build Context 类型。
    /// </summary>
    public sealed class ResourceBuildContext
    {
        /// <summary>
        /// 初始化 Resource Build Context。
        /// </summary>
        /// <param name="settings">settings 参数。</param>
        /// <param name="registry">registry 参数。</param>
        /// <param name="packages">packages 参数。</param>
        /// <param name="previews">previews 参数。</param>
        /// <param name="buildSettings">build Settings 参数。</param>
        /// <param name="buildTime">build Time 参数。</param>
        public ResourceBuildContext(
            ResourceEditorSettings settings,
            ResourceEditorRegistry registry,
            IReadOnlyList<ResourceEditorPackage> packages,
            IReadOnlyDictionary<ResourceEditorBundle, List<ResourceGroupPreview>> previews,
            ResourceBuildSettings buildSettings,
            DateTime buildTime)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            Packages = packages ?? Array.Empty<ResourceEditorPackage>();
            Previews = previews;
            BuildSettings = buildSettings ?? throw new ArgumentNullException(nameof(buildSettings));
            BuildTime = buildTime;
        }

        public ResourceEditorSettings Settings { get; }

        public ResourceEditorRegistry Registry { get; }

        public IReadOnlyList<ResourceEditorPackage> Packages { get; }

        public IReadOnlyDictionary<ResourceEditorBundle, List<ResourceGroupPreview>> Previews { get; }

        public ResourceBuildSettings BuildSettings { get; }

        public DateTime BuildTime { get; }
    }

    /// <summary>
    /// 定义 Resource Build Plan 类型。
    /// </summary>
    public sealed class ResourceBuildPlan
    {
        /// <summary>         /// 存储 Bundles。         /// </summary>
        private readonly List<ResourceBuildPlanBundle> m_Bundles = new List<ResourceBuildPlanBundle>();

        /// <summary>
        /// 存储 Bundles。
        /// </summary>
        public IReadOnlyList<ResourceBuildPlanBundle> Bundles => m_Bundles;

        /// <summary>
        /// 添加 Bundle。
        /// </summary>
        /// <param name="bundle">bundle 参数。</param>
        public void AddBundle(ResourceBuildPlanBundle bundle)
        {
            if (bundle == null)
            {
                throw new ArgumentNullException(nameof(bundle));
            }

            m_Bundles.Add(bundle);
        }
    }

    /// <summary>
    /// 定义 Resource Build Plan Bundle 类型。
    /// </summary>
    public sealed class ResourceBuildPlanBundle
    {
        /// <summary>
        /// 初始化 Resource Build Plan Bundle。
        /// </summary>
        /// <param name="package">package 参数。</param>
        /// <param name="bundle">bundle 参数。</param>
        /// <param name="bundleName">bundle Name 参数。</param>
        /// <param name="resources">resources 参数。</param>
        public ResourceBuildPlanBundle(
            ResourceEditorPackage package,
            ResourceEditorBundle bundle,
            string bundleName,
            IReadOnlyList<ResourceGroupPreview> resources)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            Bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            BundleName = bundleName;
            Resources = resources ?? Array.Empty<ResourceGroupPreview>();
        }

        public ResourceEditorPackage Package { get; }

        public ResourceEditorBundle Bundle { get; }

        public string BundleName { get; }

        public IReadOnlyList<ResourceGroupPreview> Resources { get; }

        /// <summary>
        /// 执行 To Asset Bundle Build。
        /// </summary>
        /// <returns>执行结果。</returns>
        public AssetBundleBuild ToAssetBundleBuild()
        {
            var assetNames = new List<string>();
            var addressableNames = new List<string>();
            foreach (var resource in Resources)
            {
                if (resource == null || string.IsNullOrWhiteSpace(resource.AssetPath))
                {
                    continue;
                }

                assetNames.Add(resource.AssetPath);
                addressableNames.Add(resource.Location);
            }

            return new AssetBundleBuild
            {
                assetBundleName = BundleName,
                assetNames = assetNames.ToArray(),
                addressableNames = addressableNames.ToArray()
            };
        }
    }

    /// <summary>
    /// 定义 Resource Build Artifact 类型。
    /// </summary>
    [Serializable]
    public sealed class ResourceBuildArtifact
    {
        /// <summary>
        /// 存储 Package Name。
        /// </summary>
        public string PackageName;
        /// <summary>
        /// 存储 Bundle Name。
        /// </summary>
        public string BundleName;
        /// <summary>
        /// 存储 Local Path。
        /// </summary>
        public string LocalPath;
        /// <summary>
        /// 存储 Remote Key。
        /// </summary>
        public string RemoteKey;
        /// <summary>
        /// 记录 Hash 状态。
        /// </summary>
        public string Hash;
        /// <summary>
        /// 存储 Size。
        /// </summary>
        public long Size;
        /// <summary>
        /// 存储 Crc。
        /// </summary>
        public uint Crc;
        /// <summary>         /// 存储 Dependencies。         /// </summary>
        public List<string> Dependencies = new List<string>();
    }

    /// <summary>
    /// 定义 Resource Build Result 类型。
    /// </summary>
    [Serializable]
    public sealed class ResourceBuildResult
    {
        /// <summary>
        /// 记录 Succeeded 状态。
        /// </summary>
        public bool Succeeded;
        /// <summary>
        /// 存储 Output Root。
        /// </summary>
        public string OutputRoot;
        /// <summary>
        /// 存储 Manifest Path。
        /// </summary>
        public string ManifestPath;
        /// <summary>
        /// 存储 Error Message。
        /// </summary>
        public string ErrorMessage;
        /// <summary>
        /// 存储 Build Time。
        /// </summary>
        public long BuildTime;
        /// <summary>         /// 存储 Artifacts。         /// </summary>
        public List<ResourceBuildArtifact> Artifacts = new List<ResourceBuildArtifact>();

        /// <summary>
        /// 执行 Failure。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <returns>执行结果。</returns>
        public static ResourceBuildResult Failure(string message)
        {
            return new ResourceBuildResult
            {
                Succeeded = false,
                ErrorMessage = message,
                BuildTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }
    }
}
