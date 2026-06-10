namespace GameDeveloperKit.Timer
{
    /// <summary>
    /// 定义 Timer Update Context 结构。
    /// </summary>
    public readonly struct TimerUpdateContext
    {
        /// <summary>
        /// 初始化 Timer Update Context。
        /// </summary>
        /// <param name="tickKind">tick Kind 参数。</param>
        /// <param name="tick">tick 参数。</param>
        /// <param name="time">time 参数。</param>
        /// <param name="unscaledTime">unscaled Time 参数。</param>
        /// <param name="deltaTime">delta Time 参数。</param>
        /// <param name="unscaledDeltaTime">unscaled Delta Time 参数。</param>
        internal TimerUpdateContext(
            TimerTickKind tickKind,
            long tick,
            double time,
            double unscaledTime,
            float deltaTime,
            float unscaledDeltaTime)
        {
            TickKind = tickKind;
            Tick = tick;
            Time = time;
            UnscaledTime = unscaledTime;
            DeltaTime = deltaTime;
            UnscaledDeltaTime = unscaledDeltaTime;
        }

        internal TimerTickKind TickKind { get; }

        public long Tick { get; }

        public double Time { get; }

        public double UnscaledTime { get; }

        public float DeltaTime { get; }

        public float UnscaledDeltaTime { get; }
    }
}
