namespace GameDeveloperKit.World
{
    /// <summary>
    /// World时间状态
    /// 记录World的运行时间、帧计数等信息
    /// </summary>
    public class WorldTime
    {
        /// <summary>
        /// World累计运行时间（秒，受TimeScale影响）
        /// </summary>
        public float TotalTime { get; internal set; }

        /// <summary>
        /// 上一帧的DeltaTime（受TimeScale影响）
        /// </summary>
        public float DeltaTime { get; internal set; }

        /// <summary>
        /// 不受TimeScale影响的真实DeltaTime
        /// </summary>
        public float UnscaledDeltaTime { get; internal set; }

        /// <summary>
        /// 固定更新累计时间
        /// </summary>
        public float FixedTime { get; internal set; }

        /// <summary>
        /// 固定更新的DeltaTime
        /// </summary>
        public float FixedDeltaTime { get; internal set; }

        /// <summary>
        /// 普通更新帧计数
        /// </summary>
        public int FrameCount { get; internal set; }

        /// <summary>
        /// 固定更新帧计数
        /// </summary>
        public int FixedFrameCount { get; internal set; }
    }
}
