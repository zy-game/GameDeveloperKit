namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 表示框架操作失败的分类。
    /// </summary>
    public enum FrameworkFailureCategory
    {
        /// <summary>
        /// 未指定失败分类。
        /// </summary>
        None = 0,

        /// <summary>
        /// 启动流程相关失败。
        /// </summary>
        Startup = 1,

        /// <summary>
        /// 资源系统相关失败。
        /// </summary>
        Resource = 2,

        /// <summary>
        /// 下载流程相关失败。
        /// </summary>
        Download = 3,

        /// <summary>
        /// 网络请求相关失败。
        /// </summary>
        Network = 4,

        /// <summary>
        /// 数据校验相关失败。
        /// </summary>
        Validation = 5,

        /// <summary>
        /// 配置相关失败。
        /// </summary>
        Configuration = 6,

        /// <summary>
        /// 平台能力相关失败。
        /// </summary>
        Platform = 7
    }
}
