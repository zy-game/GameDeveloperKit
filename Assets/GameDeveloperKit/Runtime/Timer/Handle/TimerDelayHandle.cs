using System;

namespace GameDeveloperKit.Timer
{
    public sealed class TimerDelayHandle : TimerHandle
    {
        private const float Epsilon = 0.000001f;

        private readonly Action m_Callback;
        private readonly Action<float> m_LegacyCallback;

        public TimerDelayHandle(float delay, Action callback, bool useUnscaledTime = false)
        {
            ValidateDuration(delay, nameof(delay));
            Delay = delay;
            m_Callback = callback ?? throw new ArgumentNullException(nameof(callback));
            UseUnscaledTime = useUnscaledTime;
        }

        public TimerDelayHandle(float delay, Action<float> callback, bool useUnscaledTime = false)
        {
            ValidateDuration(delay, nameof(delay));
            Delay = delay;
            m_LegacyCallback = callback ?? throw new ArgumentNullException(nameof(callback));
            UseUnscaledTime = useUnscaledTime;
        }

        internal override TimerTickKind TickKind => TimerTickKind.Update;

        public float Delay { get; }

        public bool UseUnscaledTime { get; }

        public float Elapsed { get; private set; }

        public float Remaining { get; private set; }

        public float Progress { get; private set; }

        public double NextFireTime { get; private set; }

        internal Action<float> LegacyCallback => m_LegacyCallback;

        internal override void Advance(in TimerUpdateContext context, float phaseUnscaledDeltaTime)
        {
            if (!CanAdvance())
            {
                return;
            }

            var deltaTime = UseUnscaledTime ? context.UnscaledDeltaTime : context.DeltaTime;
            var time = UseUnscaledTime ? context.UnscaledTime : context.Time;
            Elapsed += deltaTime;
            Remaining = Math.Max(0f, Delay - Elapsed);
            Progress = Delay <= Epsilon ? 1f : Math.Min(1f, Elapsed / Delay);

            if (Elapsed + Epsilon < Delay)
            {
                NextFireTime = time + Remaining;
                return;
            }

            Execute(deltaTime);
            if (IsCancelled)
            {
                return;
            }

            Complete();
            Remaining = 0f;
            Progress = 1f;
            NextFireTime = 0d;
        }

        protected override void OnAttached()
        {
            Elapsed = 0f;
            Remaining = Delay;
            Progress = 0f;
            NextFireTime = Module.GetClockTime(TickKind, UseUnscaledTime) + Delay;
        }

        private void Execute(float deltaTime)
        {
            if (m_Callback != null)
            {
                m_Callback();
                return;
            }

            m_LegacyCallback(deltaTime);
        }

        private static void ValidateDuration(float value, string paramName)
        {
            if (value < 0f)
            {
                throw new ArgumentException("Timer duration cannot be negative.", paramName);
            }
        }
    }
}
