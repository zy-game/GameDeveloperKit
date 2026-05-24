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
}
