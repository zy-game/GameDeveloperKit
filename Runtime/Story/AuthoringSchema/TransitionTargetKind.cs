namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情 transition 的目标类型。
    /// </summary>
    public enum TransitionTargetKind
    {
        /// <summary>
        /// 跳转到当前图内的节点。
        /// </summary>
        Node = 0,

        /// <summary>
        /// 跳转到指定章节。
        /// </summary>
        Chapter = 1,

        /// <summary>
        /// 结束当前剧情。
        /// </summary>
        StoryEnd = 2
    }
}
