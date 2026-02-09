namespace GameDeveloperKit.UI
{
    /// <summary>
    /// UI 状态枚举
    /// </summary>
    public enum UIStatus
    {
        /// <summary>
        /// 已关闭
        /// </summary>
        Closed,

        /// <summary>
        /// 加载中
        /// </summary>
        Loading,

        /// <summary>
        /// 打开中（播放动画）
        /// </summary>
        Opening,

        /// <summary>
        /// 已激活（显示中）
        /// </summary>
        Active,

        /// <summary>
        /// 已暂停（被隐藏但未关闭）
        /// </summary>
        Paused,

        /// <summary>
        /// 关闭中（播放动画）
        /// </summary>
        Closing
    }
}
