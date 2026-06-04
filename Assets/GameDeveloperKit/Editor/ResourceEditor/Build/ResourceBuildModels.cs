using System;
using System.Collections.Generic;
using UnityEditor;

namespace GameDeveloperKit.ResourceEditor
{
    public sealed class ResourceBuildContext
    {
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

    public sealed class ResourceBuildPlan
    {
        private readonly List<ResourceBuildPlanBundle> m_Bundles = new List<ResourceBuildPlanBundle>();

        public IReadOnlyList<ResourceBuildPlanBundle> Bundles => m_Bundles;

        public void AddBundle(ResourceBuildPlanBundle bundle)
        {
            if (bundle == null)
            {
                throw new ArgumentNullException(nameof(bundle));
            }

            m_Bundles.Add(bundle);
        }
    }

    public sealed class ResourceBuildPlanBundle
    {
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

    [Serializable]
    public sealed class ResourceBuildArtifact
    {
        public string PackageName;
        public string BundleName;
        public string LocalPath;
        public string RemoteKey;
        public string Hash;
        public long Size;
        public uint Crc;
        public List<string> Dependencies = new List<string>();
    }

    [Serializable]
    public sealed class ResourceBuildResult
    {
        public bool Succeeded;
        public string OutputRoot;
        public string ManifestPath;
        public string ErrorMessage;
        public long BuildTime;
        public List<ResourceBuildArtifact> Artifacts = new List<ResourceBuildArtifact>();

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
