using System.Collections.Generic;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源包信息
    /// </summary>
    public sealed class BundleInfo
    {
        /// <summary>
        /// 资源包名
        /// </summary>
        public string Name;

        /// <summary>
        /// 资源包文件哈希
        /// </summary>
        public string Hash;

        /// <summary>
        /// 资源大小
        /// </summary>
        public long Size;

        /// <summary>
        /// 资源包校验值
        /// </summary>
        public uint Crc;

        /// <summary>
        /// 资源加载 Provider 标识。
        /// </summary>
        public string ProviderId;

        /// <summary>
        /// 资源列表
        /// </summary>
        public List<AssetInfo> Assets = new List<AssetInfo>();

        /// <summary>
        /// 资源依赖列表
        /// </summary>
        public List<string> Dependencies = new List<string>();

        /// <summary>
        /// 根据 Location 查找资源信息。
        /// </summary>
        /// <param name="location">资源 Location。</param>
        /// <param name="assetInfo">输出资源信息。</param>
        /// <returns>如果找到资源信息，则返回true；否则返回false。</returns>
        public bool TryGetAsset(string location, out AssetInfo assetInfo)
        {
            if (Assets == null)
            {
                assetInfo = null;
                return false;
            }

            foreach (var asset in Assets)
            {
                if (asset == null)
                {
                    continue;
                }
                if (string.Equals(asset.Location, location, System.StringComparison.Ordinal))
                {
                    assetInfo = asset;
                    return true;
                }
            }

            assetInfo = null;
            return false;
        }
    }
}
