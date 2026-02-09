namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源包状态
    /// </summary>
    public enum PackageStatus
    {
        /// <summary>
        /// 未初始化
        /// </summary>
        None,
        
        /// <summary>
        /// 初始化中
        /// </summary>
        Initializing,
        
        /// <summary>
        /// 就绪
        /// </summary>
        Ready,
        
        /// <summary>
        /// 初始化失败
        /// </summary>
        Failed
    }
}
