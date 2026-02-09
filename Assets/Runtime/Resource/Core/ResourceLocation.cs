namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源定位信息
    /// </summary>
    public class ResourceLocation
    {
        /// <summary>
        /// 资源信息
        /// </summary>
        public AssetInfo AssetInfo { get; set; }

        /// <summary>
        /// 所属 Bundle 名称
        /// </summary>
        public string BundleName { get; set; }

        /// <summary>
        /// Bundle 内资源路径
        /// </summary>
        public string AssetPath { get; set; }

        /// <summary>
        /// 资源类型
        /// </summary>
        public ResourceLocationType LocationType { get; set; }
    }
}