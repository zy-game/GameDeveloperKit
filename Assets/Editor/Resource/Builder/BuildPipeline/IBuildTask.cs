namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 构建任务接口
    /// </summary>
    public interface IBuildTask
    {
        /// <summary>
        /// 任务名称
        /// </summary>
        string TaskName { get; }

        /// <summary>
        /// 执行任务
        /// </summary>
        /// <param name="context">构建上下文</param>
        /// <returns>任务执行结果</returns>
        TaskResult Run(BuildContext context);
    }
}
