using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源总清单
    /// </summary>
    public sealed class ManifestInfo
    {
        /// <summary>
        /// 资源版本
        /// </summary>
        public string Version;

        /// <summary>
        /// 资源编译时间
        /// </summary>
        public long BuildTime;

        /// <summary>
        /// 资源包列表
        /// </summary>
        public List<PackageInfo> Packages = new List<PackageInfo>();


        /// <summary>
        /// 根据资源包名获取资源包信息。
        /// </summary>
        /// <param name="bundleName">资源包名。</param>
        /// <returns>资源包信息；未找到时返回null。</returns>
        /// <exception cref="ArgumentNullException">资源包名为空时抛出。</exception>
        /// <exception cref="ArgumentException">资源包名为空字符串或空白字符串时抛出。</exception>
        public BundleInfo GetBundle(string bundleName)
        {
            ValidateKey(bundleName, nameof(bundleName));
            foreach (var package in Packages)
            {
                if (package?.Bundles == null)
                {
                    continue;
                }

                var bundle = package.Bundles.FirstOrDefault(x => x != null && x.Name == bundleName);
                if (bundle != null)
                {
                    return bundle;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取指定资源包的依赖资源包列表。
        /// </summary>
        /// <param name="bundleName">资源包名。</param>
        /// <returns>依赖资源包列表。</returns>
        /// <exception cref="ArgumentNullException">资源包名为空时抛出。</exception>
        /// <exception cref="ArgumentException">资源包名为空字符串或空白字符串时抛出。</exception>
        public IReadOnlyList<BundleInfo> GetDependencies(string bundleName)
        {
            ValidateKey(bundleName, nameof(bundleName));
            var bundle = GetBundle(bundleName);
            if (bundle?.Dependencies == null || bundle.Dependencies.Count == 0)
            {
                return Array.Empty<BundleInfo>();
            }

            var dependencies = new List<BundleInfo>();
            foreach (var dependencyName in bundle.Dependencies)
            {
                if (string.IsNullOrWhiteSpace(dependencyName))
                {
                    continue;
                }

                var dependency = GetBundle(dependencyName);
                if (dependency != null)
                {
                    dependencies.Add(dependency);
                }
            }

            return dependencies;
        }

        /// <summary>
        /// 获取资源包（含依赖）的完整 bundle 列表，依赖优先、去重、保持初始化顺序。
        /// </summary>
        /// <param name="packageName">资源包名。</param>
        /// <returns>bundle 列表；资源包不存在时返回 null，资源包为空时返回空列表。</returns>
        public IReadOnlyList<BundleInfo> GetPackageBundles(string packageName)
        {
            ValidateKey(packageName, nameof(packageName));
            var package = Packages?.FirstOrDefault(x => x != null && x.Name == packageName);
            if (package == null)
            {
                return null;
            }

            var bundles = new List<BundleInfo>();
            if (package.Bundles == null)
            {
                return bundles;
            }

            var visited = new HashSet<string>();
            foreach (var bundle in package.Bundles)
            {
                AddBundleWithDependencies(bundle, bundles, visited);
            }

            return bundles;
        }

        private void AddBundleWithDependencies(BundleInfo bundle, List<BundleInfo> bundles, HashSet<string> visited)
        {
            if (bundle == null || string.IsNullOrWhiteSpace(bundle.Name) || visited.Add(bundle.Name) is false)
            {
                return;
            }

            if (bundle.Dependencies != null)
            {
                foreach (var dependencyName in bundle.Dependencies)
                {
                    var dependency = GetBundle(dependencyName);
                    if (dependency == null)
                    {
                        throw new GameException($"Bundle dependency not found: {dependencyName}");
                    }

                    AddBundleWithDependencies(dependency, bundles, visited);
                }
            }

            bundles.Add(bundle);
        }

        /// <summary>
        /// 校验 Key。
        /// </summary>
        /// <param name="parameterName">parameter Name 参数。</param>
        private static void ValidateKey(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }
        }
    }

    public static class ManifestMergeUtility
    {
        public static ManifestInfo Merge(ManifestInfo localManifest, ManifestInfo hotUpdateManifest)
        {
            if (localManifest == null)
            {
                return hotUpdateManifest;
            }

            if (hotUpdateManifest == null)
            {
                return localManifest;
            }

            var merged = new ManifestInfo
            {
                Version = string.IsNullOrWhiteSpace(hotUpdateManifest.Version) ? localManifest.Version : hotUpdateManifest.Version,
                BuildTime = Math.Max(localManifest.BuildTime, hotUpdateManifest.BuildTime),
                Packages = new List<PackageInfo>()
            };

            foreach (var package in localManifest.Packages ?? Enumerable.Empty<PackageInfo>())
            {
                if (package != null)
                {
                    merged.Packages.Add(package);
                }
            }

            foreach (var package in hotUpdateManifest.Packages ?? Enumerable.Empty<PackageInfo>())
            {
                if (package == null || string.IsNullOrWhiteSpace(package.Name))
                {
                    continue;
                }

                if (string.Equals(package.Name, ResourceConstants.BUILTIN_PACKAGE_NAME, StringComparison.Ordinal))
                {
                    continue;
                }

                if (merged.Packages.Any(x => x != null && x.Name == package.Name))
                {
                    throw new GameException($"Duplicate package between local and hot update manifests: {package.Name}");
                }

                merged.Packages.Add(package);
            }

            return merged;
        }
    }
}
