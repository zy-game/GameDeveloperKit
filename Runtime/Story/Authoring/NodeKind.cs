namespace GameDeveloperKit.Story.Authoring
{
    /// <summary>
    /// 剧情图中的语义节点类型。
    /// </summary>
    public enum NodeKind
    {
        /// <summary>
        /// 起始节点。
        /// </summary>
        Start = 0,

        /// <summary>
        /// 结束节点。
        /// </summary>
        End = 1,

        /// <summary>
        /// 并行执行。
        /// </summary>
        Parallel = 6,

        /// <summary>
        /// 等待。
        /// </summary>
        Wait = 7,

        /// <summary>
        /// 对白。
        /// </summary>
        Dialogue = 100,

        /// <summary>
        /// 旁白。
        /// </summary>
        Narration = 101,

        /// <summary>
        /// 播放视频。
        /// </summary>
        PlayVideo = 102,

        /// <summary>
        /// 显示图片。
        /// </summary>
        ShowImage = 103,

        /// <summary>
        /// 播放音频。
        /// </summary>
        PlayAudio = 104,

        /// <summary>
        /// 业务代码扩展节点。
        /// </summary>
        Logic = 109,

        /// <summary>
        /// 选项交互。
        /// </summary>
        Choice = 200
    }
}
