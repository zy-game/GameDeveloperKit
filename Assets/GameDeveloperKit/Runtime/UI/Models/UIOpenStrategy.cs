namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// UI打开策略枚举，定义打开已存在窗口时的处理方式。
    /// </summary>
    public enum UIOpenStrategy
    {
        /// <summary>
        /// 返回已存在的窗口，不重新创建或刷新。
        /// </summary>
        ReturnExisting = 0,

        /// <summary>
        /// 刷新已存在的窗口，重新执行打开逻辑。
        /// </summary>
        RefreshExisting = 1,

        /// <summary>
        /// 重新创建窗口，关闭已存在的窗口并创建新实例。
        /// </summary>
        Recreate = 2
    }
}
