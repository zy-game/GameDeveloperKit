using System.Collections.Generic;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源组信息
    /// </summary>
    public sealed class PackageInfo
    {
        /// <summary>
        /// 资源组名称。
        /// </summary>
        public string Name;

        /// <summary>
        /// 资源组包含的资源包列表。
        /// </summary>
        public List<BundleInfo> Bundles = new List<BundleInfo>();

        /// <summary>
        /// 根据资源包名查找资源包信息。
        /// </summary>
        /// <param name="name">资源包名。</param>
        /// <param name="bundleInfo">输出资源包信息。</param>
        /// <returns>如果找到资源包信息，则返回true；否则返回false。</returns>
        public bool TryGetBundle(string name, out BundleInfo bundleInfo)
        {
            if (Bundles == null)
            {
                bundleInfo = null;
                return false;
            }

            foreach (var bundle in Bundles)
            {
                if (bundle == null)
                {
                    continue;
                }

                if (bundle.Name == name)
                {
                    bundleInfo = bundle;
                    return true;
                }
            }

            bundleInfo = null;
            return false;
        }
    }
}
