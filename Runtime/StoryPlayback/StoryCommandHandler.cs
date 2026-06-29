namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情命令执行器。
    /// </summary>
    public interface IStoryCommandHandler
    {
        /// <summary>
        /// 判断是否能执行指定命令。
        /// </summary>
        /// <param name="command">命令。</param>
        /// <returns>能执行时返回 true。</returns>
        bool CanHandle(StoryCommand command);

        /// <summary>
        /// 执行命令。
        /// </summary>
        /// <param name="command">命令。</param>
        /// <param name="context">运行上下文。</param>
        /// <returns>命令执行句柄。</returns>
        IStoryCommandHandle Execute(StoryCommand command, StoryRuntimeContext context);
    }

    /// <summary>
    /// 剧情视频命令播放器。
    /// </summary>
    public interface IStoryVideoCommandPlayer
    {
        /// <summary>
        /// 播放视频。
        /// </summary>
        /// <param name="command">命令。</param>
        /// <param name="context">运行上下文。</param>
        /// <param name="clipPath">视频路径。</param>
        /// <returns>命令句柄。</returns>
        IStoryCommandHandle PlayVideo(StoryCommand command, StoryRuntimeContext context, string clipPath);
    }

    /// <summary>
    /// 剧情图片命令播放器。
    /// </summary>
    public interface IStoryImageCommandPlayer
    {
        /// <summary>
        /// 显示图片。
        /// </summary>
        /// <param name="command">命令。</param>
        /// <param name="context">运行上下文。</param>
        /// <param name="imagePath">图片路径。</param>
        /// <returns>命令句柄。</returns>
        IStoryCommandHandle ShowImage(StoryCommand command, StoryRuntimeContext context, string imagePath);
    }

    /// <summary>
    /// 剧情音频命令播放器。
    /// </summary>
    public interface IStoryAudioCommandPlayer
    {
        /// <summary>
        /// 播放音频。
        /// </summary>
        /// <param name="command">命令。</param>
        /// <param name="context">运行上下文。</param>
        /// <param name="clipPath">音频路径。</param>
        /// <returns>命令句柄。</returns>
        IStoryCommandHandle PlayAudio(StoryCommand command, StoryRuntimeContext context, string clipPath);
    }
}
