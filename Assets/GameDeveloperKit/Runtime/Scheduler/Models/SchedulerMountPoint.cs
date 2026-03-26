namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 定义调度任务的执行挂载点。
    /// </summary>
    /// <remarks>
    /// 挂载点决定了任务在游戏循环中的执行时机和上下文。
    /// 不同的挂载点对应不同的更新频率和优先级。
    /// </remarks>
    public enum SchedulerMountPoint
    {
        /// <summary>
        /// 默认挂载点，任务在每帧更新时执行。
        /// </summary>
        /// <remarks>
        /// 这是最常用的挂载点，适用于大多数需要定期执行的任务。
        /// 任务会在 MonoBehaviour 的 Update 方法中执行。
        /// </remarks>
        Default = 0,

        /// <summary>
        /// 启动挂载点，任务在游戏启动阶段执行。
        /// </summary>
        /// <remarks>
        /// 此挂载点的任务只在游戏启动期间执行一次，用于执行初始化逻辑。
        /// 适合需要等待框架初始化完成的启动任务。
        /// </remarks>
        Startup = 1,

        /// <summary>
        /// UI 挂载点，任务在 UI 更新时执行。
        /// </summary>
        /// <remarks>
        /// 此挂载点的任务与 UI 系统同步，适合需要与 UI 交互的任务。
        /// 可能有与默认挂载点不同的更新频率。
        /// </remarks>
        UI = 2,

        /// <summary>
        /// 流程挂载点，任务在流程系统更新时执行。
        /// </summary>
        /// <remarks>
        /// 此挂载点的任务与游戏流程管理系统同步，适合与流程状态相关的任务。
        /// 可能有与默认挂载点不同的更新频率。
        /// </remarks>
        Procedure = 3
    }
}
