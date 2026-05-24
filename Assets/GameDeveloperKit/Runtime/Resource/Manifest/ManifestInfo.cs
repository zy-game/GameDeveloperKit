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
            return default;
        }

        public IReadOnlyList<BundleInfo> GetDependencies(string bundleName)
        {
            ValidateKey(bundleName, nameof(bundleName));
            return default;
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