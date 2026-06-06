using System.Collections.Generic;

namespace GameDeveloperKit.Timer
{
    public readonly struct TimerSnapshot
    {
        public TimerSnapshot(
            long tick,
            double time,
            float deltaTime,
            float unscaledDeltaTime,
            IReadOnlyList<TimerDelayHandle> delays,
            IReadOnlyList<TimerCountdownHandle> countdowns,
            IReadOnlyList<TimerIntervalHandle> intervals)
        {
            Tick = tick;
            Time = time;
            DeltaTime = deltaTime;
            UnscaledDeltaTime = unscaledDeltaTime;
            Delays = delays;
            Countdowns = countdowns;
            Intervals = intervals;
        }

        public long Tick { get; }

        public double Time { get; }

        public float DeltaTime { get; }

        public float UnscaledDeltaTime { get; }

        public IReadOnlyList<TimerDelayHandle> Delays { get; }

        public IReadOnlyList<TimerCountdownHandle> Countdowns { get; }

        public IReadOnlyList<TimerIntervalHandle> Intervals { get; }
    }
}
