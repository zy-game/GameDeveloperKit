namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源定位类型
    /// </summary>
    public enum ResourceLocationType : byte
    {
        /// <summary>
        /// AssetBundle 资源
        /// </summary>
        Bundle,

        /// <summary>
        /// Resources 内置资源
        /// </summary>
        Builtin,

        /// <summary>
        /// 网络资源
        /// </summary>
        Remote
    }
}