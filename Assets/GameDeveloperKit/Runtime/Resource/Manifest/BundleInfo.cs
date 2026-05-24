using System;
using System.Collections.Generic;
using Newtonsoft.Json;

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
        /// 资源哈希值
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
        /// 资源包版本
        /// </summary>
        public string Version;

        /// <summary>
        /// 资源列表
        /// </summary>
        public List<AssetInfo> Assets;

        /// <summary>
        /// 资源依赖列表
        /// </summary>
        public List<string> Dependencies;
    }
}