namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 表示已调度任务的句柄，用于管理和操作已注册的定时任务。
    /// </summary>
    /// <remarks>
    /// 每个通过 SchedulerModule 调度的任务都会获得唯一的句柄。
    /// 通过句柄可以取消任务、查询任务状态以及获取任务的元数据。
    /// </remarks>
    public sealed class ScheduledTaskHandle
    {
        /// <summary>
        /// 初始化 ScheduledTaskHandle 的新实例。
        /// </summary>
        /// <param name="id">任务的唯一标识符。</param>
        /// <param name="mountPoint">任务挂载点，指定任务在哪个调度上下文中执行。</param>
        /// <param name="group">任务所属的组，用于批量管理。</param>
        /// <param name="tag">任务的标签，用于筛选和分类。</param>
        internal ScheduledTaskHandle(int id, SchedulerMountPoint mountPoint = SchedulerMountPoint.Default, string group = null, string tag = null)
        {
            Id = id;
            MountPoint = mountPoint;
            Group = group;
            Tag = tag;
        }

        /// <summary>
        /// 获取任务的唯一标识符。
        /// </summary>
        /// <remarks>
        /// 此 ID 由 SchedulerModule 自动分配，在整个调度器生命周期内保证唯一性。
        /// 可用于引用和操作特定的调度任务。
        /// </remarks>
        public int Id { get; }

        /// <summary>
        /// 获取任务的挂载点。
        /// </summary>
        /// <remarks>
        /// 挂载点决定了任务在哪个调度上下文中执行。
        /// 例如，Startup 挂载点的任务会在游戏启动期间执行，而 UI 挂载点的任务会在 UI 更新时执行。
        /// </remarks>
        public SchedulerMountPoint MountPoint { get; }

        /// <summary>
        /// 获取任务所属的组。
        /// </summary>
        /// <remarks>
        /// 组用于批量管理任务，可以一次取消或操作同一组的多个任务。
        /// 如果任务未指定组，此值为 null。
        /// </remarks>
        public string Group { get; }

        /// <summary>
        /// 获取任务的标签。
        /// </summary>
        /// <remarks>
        /// 标签用于筛选和分类任务，便于查找和管理具有相似特征的任务。
        /// 如果任务未指定标签，此值为 null。
        /// </remarks>
        public string Tag { get; }

        /// <summary>
        /// 获取任务是否已被取消。
        /// </summary>
        /// <remarks>
        /// 当任务被取消时，此属性返回 true，已取消的任务将不再执行。
        /// 可以通过调用 SchedulerModule 的 Cancel 方法来取消任务。
        /// </remarks>
        public bool IsCancelled { get; internal set; }
    }
}
