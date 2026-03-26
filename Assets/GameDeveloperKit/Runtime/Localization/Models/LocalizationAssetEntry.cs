using System;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 本地化资源条目，定义本地化资源的引用信息
    /// </summary>
    [Serializable]
    public sealed class LocalizationAssetEntry
    {
        /// <summary>
        /// 本地化键
        /// </summary>
        public string Key;

        /// <summary>
        /// 资源名称
        /// </summary>
        public string Name;

        /// <summary>
        /// 资源完整路径
        /// </summary>
        public string FullPath;
    }
}
