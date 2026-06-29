namespace GameDeveloperKit.Story
{
    /// <summary>
    /// AVProVideo Story 视频预热状态。
    /// </summary>
    public enum StoryAvProVideoPreloadStatus
    {
        /// <summary>
        /// 等待 AVPro 准备。
        /// </summary>
        Pending,

        /// <summary>
        /// AVPro 已可播放。
        /// </summary>
        ReadyToPlay,

        /// <summary>
        /// 首帧纹理已可显示。
        /// </summary>
        FirstFrameReady,

        /// <summary>
        /// 预热失败。
        /// </summary>
        Failed,

        /// <summary>
        /// 预热已取消。
        /// </summary>
        Canceled
    }
}
