namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// UI窗口状态枚举，表示UI窗口的当前生命周期状态。
    /// </summary>
    public enum UIStatus
    {
        /// <summary>
        /// 窗口已关闭。
        /// </summary>
        Closed = 0,

        /// <summary>
        /// 窗口正在加载资源。
        /// </summary>
        Loading = 1,

        /// <summary>
        /// 窗口正在打开。
        /// </summary>
        Opening = 2,

        /// <summary>
        /// 窗口已激活并显示。
        /// </summary>
        Active = 3,

        /// <summary>
        /// 窗口已暂停（被其他窗口覆盖）。
        /// </summary>
        Paused = 4,

        /// <summary>
        /// 窗口正在关闭。
        /// </summary>
        Closing = 5
    }
}
