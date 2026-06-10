using System.Collections.Generic;

namespace GameDeveloperKit.Timer
{
    /// <summary>
    /// 定义 Timer Snapshot 结构。
    /// </summary>
    public readonly struct TimerSnapshot
    {
        /// <summary>
        /// 初始化 Timer Snapshot。
        /// </summary>
        /// <param name="tick">tick 参数。</param>
        /// <param name="time">time 参数。</param>
        /// <param name="unscaledTime">unscaled Time 参数。</param>
        /// <param name="deltaTime">delta Time 参数。</param>
        /// <param name="unscaledDeltaTime">unscaled Delta Time 参数。</param>
        /// <param name="delays">delays 参数。</param>
        /// <param name="countdowns">countdowns 参数。</param>
        /// <param name="intervals">intervals 参数。</param>
        /// <param name="updates">updates 参数。</param>
        public TimerSnapshot(
            long tick,
            double time,
            double unscaledTime,
            float deltaTime,
            float unscaledDeltaTime,
            IReadOnlyList<TimerDelayHandle> delays,
            IReadOnlyList<TimerCountdownHandle> countdowns,
            IReadOnlyList<TimerIntervalHandle> intervals,
            IReadOnlyList<TimerUpdateHandle> updates)
        {
            Tick = tick;
            Time = time;
            UnscaledTime = unscaledTime;
            DeltaTime = deltaTime;
            UnscaledDeltaTime = unscaledDeltaTime;
            Delays = delays;
            Countdowns = countdowns;
            Intervals = intervals;
            Updates = updates;
        }

        public long Tick { get; }

        public double Time { get; }

        public double UnscaledTime { get; }

        public float DeltaTime { get; }

        public float UnscaledDeltaTime { get; }

        public IReadOnlyList<TimerDelayHandle> Delays { get; }

        public IReadOnlyList<TimerCountdownHandle> Countdowns { get; }

        public IReadOnlyList<TimerIntervalHandle> Intervals { get; }

        public IReadOnlyList<TimerUpdateHandle> Updates { get; }
    }
}
