using System;
using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Resource;

namespace GameDeveloperKit.ResourceEditor.Build
{
    /// <summary>
    /// 定义 Resource Manifest Build Writer 类型。
    /// </summary>
    public static class ManifestWriter
    {
        /// <summary>
        /// 构建 member。
        /// </summary>
        /// <param name="context">context 参数。</param>
        /// <param name="plan">plan 参数。</param>
        /// <param name="result">result 参数。</param>
        /// <returns>执行结果。</returns>
        public static ManifestInfo Build(Context context, Plan plan, Result result)
        {
            return Build(context, plan, result, _ => true);
        }

        public static ManifestInfo Build(Context context, Plan plan, Result result, Func<GameDeveloperKit.ResourceEditor.Authoring.Package, bool> packageFilter)
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
                .ToDictionary(x => x.BundleName, x => x, StringComparer.Ordinal);
            var filteredPlanBundles = plan.Bundles
                .Where(x => x != null && x.Package != null && packageFilter(x.Package))
                .ToList();
            var buildBundleNames = new HashSet<string>(plan.Bundles
                .Where(x => x != null && string.IsNullOrWhiteSpace(x.BundleName) is false)
                .Select(x => x.BundleName), StringComparer.Ordinal);

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
                    if (ShouldSkipEmptyAssetBundle(planBundle, artifact))
                    {
                        continue;
                    }

                    packageInfo.Bundles.Add(new BundleInfo
                    {
                        Name = planBundle.BundleName,
                        Hash = artifact?.Hash ?? string.Empty,
                        Size = artifact?.Size ?? 0,
                        Crc = artifact?.Crc ?? 0,
                        ProviderId = planBundle.Bundle.ProviderId,
                        Dependencies = ResolveDependencyKeys(artifact, buildBundleNames),
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
        public static string ResolveVersion(Context context)
        {
            return context.BuildSettings.ManifestVersion?.Trim() ?? string.Empty;
        }

        private static bool ShouldSkipEmptyAssetBundle(PlanBundle planBundle, Artifact artifact)
        {
            return planBundle?.Bundle != null &&
                   ResourceProviderIds.IsAssetBundle(planBundle.Bundle.ProviderId) &&
                   artifact == null &&
                   (planBundle.Resources == null || planBundle.Resources.Count == 0);
        }

        /// <summary>
        /// 解析 Dependency Keys。
        /// </summary>
        /// <param name="artifact">artifact 参数。</param>
        /// <param name="buildBundleNames">build Bundle Names 参数。</param>
        /// <returns>执行结果。</returns>
        private static List<string> ResolveDependencyKeys(Artifact artifact, ISet<string> buildBundleNames)
        {
            if (artifact?.Dependencies == null || artifact.Dependencies.Count == 0)
            {
                return new List<string>();
            }

            return artifact.Dependencies
                .Where(x => string.IsNullOrWhiteSpace(x) is false)
                .Select(x => ResolveDependencyBuildName(x, buildBundleNames))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// 解析 Dependency Build Name。
        /// </summary>
        /// <param name="bundleName">bundle Name 参数。</param>
        /// <param name="buildBundleNames">build Bundle Names 参数。</param>
        /// <returns>执行结果。</returns>
        private static string ResolveDependencyBuildName(string bundleName, ISet<string> buildBundleNames)
        {
            if (buildBundleNames.Contains(bundleName))
            {
                return bundleName;
            }

            throw new InvalidOperationException($"Dependency bundle is missing from build plan: {bundleName}");
        }
    }

    public static class ManifestPartitioner
    {
        public static ManifestInfo BuildLocalBaseManifest(Context context, Plan plan, Result result)
        {
            return ManifestWriter.Build(context, plan, result, IsLocalBasePackage);
        }

        public static ManifestInfo BuildHotUpdateManifest(Context context, Plan plan, Result result)
        {
            return ManifestWriter.Build(context, plan, result, package => package != null && package.IsHotUpdate);
        }

        public static Plan CreateSbpPlan(Plan plan)
        {
            var sbpPlan = new Plan();
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

        public static IReadOnlyList<GameDeveloperKit.ResourceEditor.Authoring.Package> GetLocalBasePackages(GameDeveloperKit.ResourceEditor.Authoring.Settings settings)
        {
            if (settings?.Packages == null)
            {
                return Array.Empty<GameDeveloperKit.ResourceEditor.Authoring.Package>();
            }

            return settings.Packages
                .Where(IsLocalBasePackage)
                .ToList();
        }

        public static IReadOnlyList<GameDeveloperKit.ResourceEditor.Authoring.Package> GetHotUpdatePackages(GameDeveloperKit.ResourceEditor.Authoring.Settings settings)
        {
            if (settings?.Packages == null)
            {
                return Array.Empty<GameDeveloperKit.ResourceEditor.Authoring.Package>();
            }

            return settings.Packages
                .Where(package => package != null && package.IsHotUpdate)
                .ToList();
        }

        public static bool IsLocalBasePackage(GameDeveloperKit.ResourceEditor.Authoring.Package package)
        {
            return package != null && (GameDeveloperKit.ResourceEditor.Authoring.BuiltinConstants.IsBuiltinPackage(package) || package.IsHotUpdate is false);
        }

        public static string ResolveLocalManifestPath(GameDeveloperKit.ResourceEditor.Authoring.Settings settings)
        {
            var outputPath = settings?.ManifestOutputPath;
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = $"Assets/StreamingAssets/{ResourceSettings.MANIFEST_NAME}";
            }

            return Utilities.ProjectRelativeOrAbsolutePath(outputPath).Replace('\\', '/');
        }

        public static string ResolveLocalBundlePath(Artifact artifact)
        {
            var fileName = artifact?.BundleName;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            return Utilities.ProjectRelativeOrAbsolutePath($"Assets/StreamingAssets/{fileName}").Replace('\\', '/');
        }

        public static void WriteManifest(string path, ManifestInfo manifest)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? ".");
            System.IO.File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(manifest, Newtonsoft.Json.Formatting.Indented));
        }
    }
}
