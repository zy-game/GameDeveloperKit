namespace GameDeveloperKit.Timer
{
    public readonly struct TimerUpdateContext
    {
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
