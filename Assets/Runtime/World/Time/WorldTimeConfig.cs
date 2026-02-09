namespace GameDeveloperKit.World
{
    /// <summary>
    /// World时间配置
    /// 用于配置World的帧率、固定时间步长、时间缩放等
    /// </summary>
    public class WorldTimeConfig
    {
        /// <summary>
        /// 目标帧率（FPS），0表示跟随Unity主循环
        /// </summary>
        public int TargetFrameRate { get; set; } = 0;

        /// <summary>
        /// 固定时间步长（秒），用于物理模拟等需要固定时间的系统
        /// 默认0.02秒（50 FPS）
        /// </summary>
        public float FixedTimeStep { get; set; } = 0.02f;

        /// <summary>
        /// 最大追帧次数，防止死循环
        /// 当某一帧耗时过长时，会执行多次固定更新来追上进度
        /// </summary>
        public int MaxCatchUpFrames { get; set; } = 5;

        /// <summary>
        /// 时间缩放，1.0为正常速度
        /// 可用于慢动作、加速等效果
        /// </summary>
        public float TimeScale { get; set; } = 1.0f;

        /// <summary>
        /// 是否暂停
        /// </summary>
        public bool IsPaused { get; set; } = false;
    }
}
