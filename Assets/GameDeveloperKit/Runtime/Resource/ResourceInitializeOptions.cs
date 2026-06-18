namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源模块显式初始化参数。
    /// </summary>
    public sealed class ResourceInitializeOptions
    {
        /// <summary>
        /// 显式指定资源设置。未指定时从 Resources 读取默认 ResourceSettings。
        /// </summary>
        public ResourceSettings Settings { get; set; }
    }
}
