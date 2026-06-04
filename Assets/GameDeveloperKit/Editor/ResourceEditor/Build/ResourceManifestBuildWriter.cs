using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;

namespace GameDeveloperKit.ResourceEditor
{
    public static class ResourceManifestBuildWriter
    {
        public static ManifestInfo Build(ResourceBuildContext context, ResourceBuildPlan plan, ResourceBuildResult result)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var manifest = new ManifestInfo
            {
                Version = ResolveVersion(context),
                BuildTime = result.BuildTime,
                Packages = new List<PackageInfo>()
            };

            var artifactByBundleName = result.Artifacts
                .Where(x => x != null && string.IsNullOrWhiteSpace(x.BundleName) is false)
                .GroupBy(x => x.BundleName, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
            var logicalNameByBundleName = plan.Bundles
                .Where(x => x != null && string.IsNullOrWhiteSpace(x.BundleName) is false)
                .GroupBy(x => x.BundleName, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => NormalizeBundleLogicalName(x.First().Bundle.Name), StringComparer.Ordinal);

            foreach (var package in context.Packages.Where(x => x != null))
            {
                var packageInfo = new PackageInfo
                {
                    Name = package.Name,
                    Bundles = new List<BundleInfo>()
                };

                foreach (var planBundle in plan.Bundles.Where(x => x.Package == package))
                {
                    artifactByBundleName.TryGetValue(planBundle.BundleName, out var artifact);
                    packageInfo.Bundles.Add(new BundleInfo
                    {
                        Name = logicalNameByBundleName[planBundle.BundleName],
                        Hash = artifact?.Hash ?? string.Empty,
                        Size = artifact?.Size ?? 0,
                        Crc = artifact?.Crc ?? 0,
                        Dependencies = ResolveDependencyKeys(artifact, logicalNameByBundleName),
                        Assets = planBundle.Resources
                            .Where(resource => resource != null)
                            .Select(resource => new AssetInfo
                            {
                                Location = resource.Location,
                                TypeName = resource.TypeName,
                                Labels = resource.Labels?
                                    .Where(label => string.IsNullOrWhiteSpace(label) is false)
                                    .Distinct(StringComparer.Ordinal)
                                    .OrderBy(label => label, StringComparer.Ordinal)
                                    .ToList() ?? new List<string>()
                            })
                            .ToList()
                    });
                }

                manifest.Packages.Add(packageInfo);
            }

            return manifest;
        }

        public static string ResolveVersion(ResourceBuildContext context)
        {
            return context.BuildSettings.ManifestVersion?.Trim() ?? string.Empty;
        }

        internal static string NormalizeBundleLogicalName(string bundleName)
        {
            var normalized = (bundleName ?? string.Empty).Replace('\\', '/').Trim();
            const string bundleExtension = ".bundle";
            if (normalized.EndsWith(bundleExtension, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - bundleExtension.Length);
            }

            return normalized;
        }

        private static List<string> ResolveDependencyKeys(ResourceBuildArtifact artifact, IReadOnlyDictionary<string, string> logicalNameByBundleName)
        {
            if (artifact?.Dependencies == null || artifact.Dependencies.Count == 0)
            {
                return new List<string>();
            }

            return artifact.Dependencies
                .Where(x => string.IsNullOrWhiteSpace(x) is false)
                .Select(x => ResolveDependencyLogicalName(x, logicalNameByBundleName))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
        }

        private static string ResolveDependencyLogicalName(string bundleName, IReadOnlyDictionary<string, string> logicalNameByBundleName)
        {
            if (logicalNameByBundleName.TryGetValue(bundleName, out var logicalName))
            {
                return logicalName;
            }

            throw new InvalidOperationException($"Dependency bundle is missing from build plan: {bundleName}");
        }
    }
}
