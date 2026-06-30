using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;

namespace GameDeveloperKit.ResourceEditor
{
    /// <summary>
    /// 定义 Resource Manifest Build Writer 类型。
    /// </summary>
    public static class ResourceManifestBuildWriter
    {
        /// <summary>
        /// 构建 member。
        /// </summary>
        /// <param name="context">context 参数。</param>
        /// <param name="plan">plan 参数。</param>
        /// <param name="result">result 参数。</param>
        /// <returns>执行结果。</returns>
        public static ManifestInfo Build(ResourceBuildContext context, ResourceBuildPlan plan, ResourceBuildResult result)
        {
            return Build(context, plan, result, _ => true);
        }

        public static ManifestInfo Build(ResourceBuildContext context, ResourceBuildPlan plan, ResourceBuildResult result, Func<ResourceEditorPackage, bool> packageFilter)
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

            packageFilter ??= _ => true;

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
            var filteredPlanBundles = plan.Bundles
                .Where(x => x != null && x.Package != null && packageFilter(x.Package))
                .ToList();
            var logicalNameByBundleName = plan.Bundles
                .Where(x => x != null && string.IsNullOrWhiteSpace(x.BundleName) is false)
                .GroupBy(x => x.BundleName, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => NormalizeBundleLogicalName(x.First().Bundle.Name), StringComparer.Ordinal);

            foreach (var package in context.Packages.Where(x => x != null && packageFilter(x)))
            {
                var packageInfo = new PackageInfo
                {
                    Name = package.Name,
                    Bundles = new List<BundleInfo>()
                };

                foreach (var planBundle in filteredPlanBundles.Where(x => x.Package == package))
                {
                    artifactByBundleName.TryGetValue(planBundle.BundleName, out var artifact);
                    packageInfo.Bundles.Add(new BundleInfo
                    {
                        Name = logicalNameByBundleName[planBundle.BundleName],
                        Hash = artifact?.Hash ?? string.Empty,
                        Size = artifact?.Size ?? 0,
                        Crc = artifact?.Crc ?? 0,
                        ProviderId = planBundle.Bundle.ProviderId,
                        Dependencies = ResolveDependencyKeys(artifact, logicalNameByBundleName),
                        Assets = planBundle.Resources
                            .Where(resource => resource != null)
                            .Select(resource => new AssetInfo
                            {
                                Location = resource.Location,
                                AssetPath = resource.AssetPath,
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

        /// <summary>
        /// 解析 Version。
        /// </summary>
        /// <param name="context">context 参数。</param>
        /// <returns>执行结果。</returns>
        public static string ResolveVersion(ResourceBuildContext context)
        {
            return context.BuildSettings.ManifestVersion?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// 执行 Normalize Bundle Logical Name。
        /// </summary>
        /// <param name="bundleName">bundle Name 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 解析 Dependency Keys。
        /// </summary>
        /// <param name="artifact">artifact 参数。</param>
        /// <param name="logicalNameByBundleName">logical Name By Bundle Name 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 解析 Dependency Logical Name。
        /// </summary>
        /// <param name="bundleName">bundle Name 参数。</param>
        /// <param name="logicalNameByBundleName">logical Name By Bundle Name 参数。</param>
        /// <returns>执行结果。</returns>
        private static string ResolveDependencyLogicalName(string bundleName, IReadOnlyDictionary<string, string> logicalNameByBundleName)
        {
            if (logicalNameByBundleName.TryGetValue(bundleName, out var logicalName))
            {
                return logicalName;
            }

            throw new InvalidOperationException($"Dependency bundle is missing from build plan: {bundleName}");
        }
    }

    public static class ResourceManifestPartitioner
    {
        public static ManifestInfo BuildLocalBaseManifest(ResourceBuildContext context, ResourceBuildPlan plan, ResourceBuildResult result)
        {
            return ResourceManifestBuildWriter.Build(context, plan, result, IsLocalBasePackage);
        }

        public static ManifestInfo BuildHotUpdateManifest(ResourceBuildContext context, ResourceBuildPlan plan, ResourceBuildResult result)
        {
            return ResourceManifestBuildWriter.Build(context, plan, result, package => package != null && package.IsHotUpdate);
        }

        public static ResourceBuildPlan CreateSbpPlan(ResourceBuildPlan plan)
        {
            var sbpPlan = new ResourceBuildPlan();
            if (plan == null)
            {
                return sbpPlan;
            }

            foreach (var bundle in plan.Bundles.Where(bundle => bundle?.Bundle != null && ResourceProviderIds.IsAssetBundle(bundle.Bundle.ProviderId)))
            {
                sbpPlan.AddBundle(bundle);
            }

            return sbpPlan;
        }

        public static IReadOnlyList<ResourceEditorPackage> GetLocalBasePackages(ResourceEditorSettings settings)
        {
            if (settings?.Packages == null)
            {
                return Array.Empty<ResourceEditorPackage>();
            }

            return settings.Packages
                .Where(IsLocalBasePackage)
                .ToList();
        }

        public static IReadOnlyList<ResourceEditorPackage> GetHotUpdatePackages(ResourceEditorSettings settings)
        {
            if (settings?.Packages == null)
            {
                return Array.Empty<ResourceEditorPackage>();
            }

            return settings.Packages
                .Where(package => package != null && package.IsHotUpdate)
                .ToList();
        }

        public static bool IsLocalBasePackage(ResourceEditorPackage package)
        {
            return package != null && (ResourceEditorBuiltinConstants.IsBuiltinPackage(package) || package.IsHotUpdate is false);
        }

        public static string ResolveLocalManifestPath(ResourceEditorSettings settings)
        {
            var outputPath = settings?.ManifestOutputPath;
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = $"Assets/StreamingAssets/{ResourceSettings.MANIFEST_NAME}";
            }

            return ResourceBuildUtilities.ProjectRelativeOrAbsolutePath(outputPath).Replace('\\', '/');
        }

        public static string ResolveLocalBundlePath(ResourceBuildArtifact artifact)
        {
            var fileName = string.IsNullOrWhiteSpace(artifact?.Hash)
                ? artifact?.BundleName
                : $"{artifact.Hash}.bundle";
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            return ResourceBuildUtilities.ProjectRelativeOrAbsolutePath($"Assets/StreamingAssets/{fileName}").Replace('\\', '/');
        }

        public static void WriteManifest(string path, ManifestInfo manifest)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? ".");
            System.IO.File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(manifest, Newtonsoft.Json.Formatting.Indented));
        }
    }
}
