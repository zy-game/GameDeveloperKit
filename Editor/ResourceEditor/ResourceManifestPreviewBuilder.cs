using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;

namespace GameDeveloperKit.ResourceEditor
{
    /// <summary>
    /// 定义 Resource Manifest Preview Builder 类型。
    /// </summary>
    public static class ResourceManifestPreviewBuilder
    {
        /// <summary>
        /// 构建 member。
        /// </summary>
        /// <param name="settings">settings 参数。</param>
        /// <param name="previews">previews 参数。</param>
        /// <returns>执行结果。</returns>
        public static ManifestInfo Build(ResourceEditorSettings settings, IReadOnlyDictionary<ResourceEditorBundle, List<ResourceGroupPreview>> previews)
        {
            return Build(settings, previews, _ => true);
        }

        public static ManifestInfo Build(
            ResourceEditorSettings settings,
            IReadOnlyDictionary<ResourceEditorBundle, List<ResourceGroupPreview>> previews,
            Func<ResourceEditorPackage, bool> packageFilter)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            packageFilter ??= _ => true;
            var manifest = new ManifestInfo
            {
                Version = "preview",
                BuildTime = 0,
                Packages = new List<PackageInfo>()
            };

            foreach (var package in settings.Packages)
            {
                if (package == null)
                {
                    continue;
                }

                if (packageFilter(package) is false)
                {
                    continue;
                }

                var packageInfo = new PackageInfo
                {
                    Name = package.Name,
                    Bundles = new List<BundleInfo>()
                };

                foreach (var bundle in package.Bundles)
                {
                    if (bundle == null)
                    {
                        continue;
                    }

                    var resources = ResourceEditorEntryPreviewBuilder.HasEntries(bundle)
                        ? ResourceEditorEntryPreviewBuilder.Build(bundle)
                        : previews != null && previews.TryGetValue(bundle, out var preview)
                            ? preview
                            : new List<ResourceGroupPreview>();
                    if (ShouldSkipEmptyAssetBundle(bundle, resources))
                    {
                        continue;
                    }

                    packageInfo.Bundles.Add(new BundleInfo
                    {
                        Name = ResourceManifestBuildWriter.NormalizeBundleLogicalName(bundle.Name),
                        ProviderId = bundle.ProviderId,
                        Size = 0,
                        Crc = 0,
                        Dependencies = new List<string>(),
                        Assets = resources.Select(resource => new AssetInfo
                        {
                            Location = resource.Location,
                            AssetPath = resource.AssetPath,
                            TypeName = resource.TypeName,
                            Labels = resource.Labels.Where(x => string.IsNullOrWhiteSpace(x) is false).Distinct().ToList()
                        }).ToList()
                    });
                }

                manifest.Packages.Add(packageInfo);
            }

            return manifest;
        }

        private static bool ShouldSkipEmptyAssetBundle(ResourceEditorBundle bundle, IReadOnlyList<ResourceGroupPreview> resources)
        {
            return bundle != null &&
                   ResourceProviderIds.IsAssetBundle(bundle.ProviderId) &&
                   (resources == null || resources.Count == 0);
        }
    }
}
