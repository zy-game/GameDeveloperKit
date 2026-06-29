namespace GameDeveloperKit.Story
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
        /// 跳转到章节。
        /// </summary>
        JumpChapter = 2,

        /// <summary>
        /// 并行执行。
        /// </summary>
        Parallel = 6,

        /// <summary>
        /// 等待。
        /// </summary>
        Wait = 7,

        /// <summary>
        /// 合流。
        /// </summary>
        Merge = 9,

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
        /// 发出外部事件。
        /// </summary>
        EmitEvent = 108,

        /// <summary>
        /// 选项交互。
        /// </summary>
        Choice = 200,

        /// <summary>
        /// 小游戏。
        /// </summary>
        MiniGame = 204,

        /// <summary>
        /// 限时快速输入互动。
        /// </summary>
        Qte = 205,

        /// <summary>
        /// 解锁互动。
        /// </summary>
        Unlock = 206
    }
}
