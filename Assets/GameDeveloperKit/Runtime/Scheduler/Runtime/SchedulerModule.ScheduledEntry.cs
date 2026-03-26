using System;

namespace GameDeveloperKit.Runtime
{
    public sealed partial class SchedulerModule
    {
        /// <summary>
        /// 表示调度器中已注册的任务条目。
        /// </summary>
        /// <remarks>
        /// 此类是 SchedulerModule 的内部数据结构，用于存储调度任务的完整信息，
        /// 包括任务句柄、执行时间、执行间隔和重复次数等。
        /// </remarks>
        private sealed class ScheduledEntry
        {
            /// <summary>
            /// 获取或设置任务句柄。
            /// </summary>
            /// <remarks>
            /// 此句柄提供了任务的唯一标识和元数据，包括挂载点、组和标签。
            /// 用于与外部交互和管理任务。
            /// </remarks>
            public ScheduledTaskHandle Handle;

            /// <summary>
            /// 获取或设置要执行的任务委托。
            /// </summary>
            /// <remarks>
            /// 这是任务的实际执行逻辑，当任务到达执行时间时会被调用。
            /// 可以是同步操作或异步操作。
            /// </remarks>
            public Action Action;

            /// <summary>
            /// 获取或设置任务的下次执行时间（秒）。
            /// </summary>
            /// <remarks>
            /// 此时间使用 Time.time 作为基准，表示自游戏开始以来的秒数。
            /// 当当前时间达到此值时，任务将被执行。
            /// </remarks>
            public double ExecuteAt;

            /// <summary>
            /// 获取或设置任务的执行间隔（秒）。
            /// </summary>
            /// <remarks>
            /// 对于重复执行的任务，此值定义了两次执行之间的时间间隔。
            /// 如果任务不重复，此值应为 0。
            /// </remarks>
            public double Interval;

            /// <summary>
            /// 获取或设置任务是否重复执行。
            /// </summary>
            /// <remarks>
            /// 如果为 true，任务会在每次执行后根据 Interval 重新调度。
            /// 如果为 false，任务只执行一次，执行后自动移除。
            /// </remarks>
            public bool Repeat;

            /// <summary>
            /// 获取或设置任务剩余的执行次数。
            /// </summary>
            /// <remarks>
            /// 对于有限次数的重复任务，此值在每次执行后递减。
            /// 当值达到 0 时，即使 Repeat 为 true，任务也会停止执行。
            /// 如果值为 -1，表示无限重复执行。
            /// </remarks>
            public int RemainingExecutions;
        }
    }
}
