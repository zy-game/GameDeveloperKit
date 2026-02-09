using System;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// AB包清单
    /// </summary>
    [Serializable]
    public class BundleManifest
    {
        /// <summary>
        /// AB包名称
        /// </summary>
        public string name;
        /// <summary>
        /// AB包版本
        /// </summary>
        public string version;
        /// <summary>
        /// 资源清单列表
        /// </summary>
        public AssetInfo[] resources;
        /// <summary>
        /// 依赖的 Bundle 名称列表
        /// </summary>
        public string[] dependencies;
        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long size;
        /// <summary>
        /// 文件哈希值（用于完整性校验）
        /// </summary>
        public string hash;
    }
}