using System;
using System.Collections.Generic;
using UnityEditor;

namespace GameDeveloperKit.ResourceEditor.Build
{
    /// <summary>
    /// 定义 Resource Build Context 类型。
    /// </summary>
    public sealed class Context
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
        public Context(
            GameDeveloperKit.ResourceEditor.Authoring.Settings settings,
            GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry registry,
            IReadOnlyList<GameDeveloperKit.ResourceEditor.Authoring.Package> packages,
            IReadOnlyDictionary<GameDeveloperKit.ResourceEditor.Authoring.Bundle, IReadOnlyList<ResourceGroupPreview>> previews,
            Settings buildSettings,
            DateTime buildTime,
            BuildTarget target)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            Packages = packages ?? Array.Empty<GameDeveloperKit.ResourceEditor.Authoring.Package>();
            Previews = previews;
            BuildSettings = buildSettings ?? throw new ArgumentNullException(nameof(buildSettings));
            BuildTime = buildTime;
            Target = target;
        }

        public GameDeveloperKit.ResourceEditor.Authoring.Settings Settings { get; }

        public GameDeveloperKit.ResourceEditor.Registry.ExtensionRegistry Registry { get; }

        public IReadOnlyList<GameDeveloperKit.ResourceEditor.Authoring.Package> Packages { get; }

        public IReadOnlyDictionary<GameDeveloperKit.ResourceEditor.Authoring.Bundle, IReadOnlyList<ResourceGroupPreview>> Previews { get; }

        public IReadOnlyList<ResourceGroupPreview> GetResources(GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle)
        {
            return Previews != null && Previews.TryGetValue(bundle, out var resources)
                ? resources
                : Array.Empty<ResourceGroupPreview>();
        }

        public Settings BuildSettings { get; }

        public DateTime BuildTime { get; }

        public BuildTarget Target { get; }
    }

    /// <summary>
    /// 定义 Resource Build Plan 类型。
    /// </summary>
    public sealed class Plan
    {
        /// <summary>         /// 存储 Bundles。         /// </summary>
        private readonly List<PlanBundle> m_Bundles = new List<PlanBundle>();

        /// <summary>
        /// 存储 Bundles。
        /// </summary>
        public IReadOnlyList<PlanBundle> Bundles => m_Bundles;

        /// <summary>
        /// 添加 Bundle。
        /// </summary>
        /// <param name="bundle">bundle 参数。</param>
        public void AddBundle(PlanBundle bundle)
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
    public sealed class PlanBundle
    {
        /// <summary>
        /// 初始化 Resource Build Plan Bundle。
        /// </summary>
        /// <param name="package">package 参数。</param>
        /// <param name="bundle">bundle 参数。</param>
        /// <param name="bundleName">bundle Name 参数。</param>
        /// <param name="resources">resources 参数。</param>
        public PlanBundle(
            GameDeveloperKit.ResourceEditor.Authoring.Package package,
            GameDeveloperKit.ResourceEditor.Authoring.Bundle bundle,
            string bundleName,
            IReadOnlyList<ResourceGroupPreview> resources)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            Bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            BundleName = bundleName;
            Resources = resources ?? Array.Empty<ResourceGroupPreview>();
        }

        public GameDeveloperKit.ResourceEditor.Authoring.Package Package { get; }

        public GameDeveloperKit.ResourceEditor.Authoring.Bundle Bundle { get; }

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
    public sealed class Artifact
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
    public sealed class Result
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
        public List<Artifact> Artifacts = new List<Artifact>();

        /// <summary>
        /// 执行 Failure。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <returns>执行结果。</returns>
        public static Result Failure(string message)
        {
            return new Result
            {
                Succeeded = false,
                ErrorMessage = message,
                BuildTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }
    }
}
