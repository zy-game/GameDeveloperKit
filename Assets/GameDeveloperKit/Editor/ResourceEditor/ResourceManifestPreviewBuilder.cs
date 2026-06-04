using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;

namespace GameDeveloperKit.ResourceEditor
{
    public static class ResourceManifestPreviewBuilder
    {
        public static ManifestInfo Build(ResourceEditorSettings settings, IReadOnlyDictionary<ResourceEditorBundle, List<ResourceGroupPreview>> previews)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

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

                    var resources = previews != null && previews.TryGetValue(bundle, out var preview)
                        ? preview
                        : new List<ResourceGroupPreview>();

                    packageInfo.Bundles.Add(new BundleInfo
                    {
                        Name = ResourceManifestBuildWriter.NormalizeBundleLogicalName(bundle.Name),
                        Size = 0,
                        Crc = 0,
                        Dependencies = new List<string>(),
                        Assets = resources.Select(resource => new AssetInfo
                        {
                            Location = resource.Location,
                            TypeName = resource.TypeName,
                            Labels = resource.Labels.Where(x => string.IsNullOrWhiteSpace(x) is false).Distinct().ToList()
                        }).ToList()
                    });
                }

                manifest.Packages.Add(packageInfo);
            }

            return manifest;
        }
    }
}
