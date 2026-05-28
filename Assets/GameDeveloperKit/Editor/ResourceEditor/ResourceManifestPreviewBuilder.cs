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
                    Version = package.Version,
                    Hash = string.Empty,
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
                        Name = bundle.Name,
                        Hash = string.Empty,
                        Size = 0,
                        Crc = 0,
                        Version = package.Version,
                        Dependencies = bundle.Dependencies.Where(x => string.IsNullOrWhiteSpace(x) is false).ToList(),
                        Assets = resources.Select(resource => new AssetInfo
                        {
                            Location = resource.Location,
                            TypeName = resource.TypeName,
                            Labels = MergeLabels(resource.Labels, bundle.Labels)
                        }).ToList()
                    });
                }

                manifest.Packages.Add(packageInfo);
            }

            return manifest;
        }

        private static List<string> MergeLabels(IReadOnlyList<string> resourceLabels, IReadOnlyList<string> bundleLabels)
        {
            var labels = new List<string>();
            AddLabels(labels, resourceLabels);
            AddLabels(labels, bundleLabels);
            return labels;
        }

        private static void AddLabels(List<string> labels, IReadOnlyList<string> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (var label in source)
            {
                if (string.IsNullOrWhiteSpace(label) || labels.Contains(label))
                {
                    continue;
                }

                labels.Add(label);
            }
        }
    }
}
