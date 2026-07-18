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

}
