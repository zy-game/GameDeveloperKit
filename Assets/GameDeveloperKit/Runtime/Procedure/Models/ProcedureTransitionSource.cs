namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 流程转换来源枚举
    /// </summary>
    public enum ProcedureTransitionSource
    {
        /// <summary>
        /// 运行时调用
        /// </summary>
        Runtime = 0,

        /// <summary>
        /// 启动流程
        /// </summary>
        Startup = 1,

        /// <summary>
        /// 场景流程
        /// </summary>
        Scene = 2,

        /// <summary>
        /// UI流程
        /// </summary>
        UI = 3
    }
}
