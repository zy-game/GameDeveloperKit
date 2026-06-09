using System;

namespace GameDeveloperKit.Timer
{
    public sealed class TimerCountdownHandle : TimerHandle
    {
        private readonly Action<float> m_OnTick;
        private readonly Action m_OnComplete;

        public TimerCountdownHandle(float duration, Action<float> onTick = null, Action onComplete = null, bool useUnscaledTime = false)
        {
            ValidateDuration(duration, nameof(duration));
            Duration = duration;
            m_OnTick = onTick;
            m_OnComplete = onComplete;
            UseUnscaledTime = useUnscaledTime;
        }

        internal override TimerTickKind TickKind => TimerTickKind.Update;

        public float Duration { get; }

        public bool UseUnscaledTime { get; }

        public float Elapsed { get; private set; }

        public float Remaining { get; private set; }

        public float Progress { get; private set; }

        public double NextFireTime { get; private set; }

        internal override void Advance(in TimerUpdateContext context, float phaseUnscaledDeltaTime)
        {
            if (!CanAdvance())
            {
                return;
            }

            var deltaTime = UseUnscaledTime ? context.UnscaledDeltaTime : context.DeltaTime;
            var time = UseUnscaledTime ? context.UnscaledTime : context.Time;
            Elapsed += deltaTime;
            Remaining = Math.Max(0f, Duration - Elapsed);
            Progress = Duration <= 0f ? 1f : Math.Min(1f, Elapsed / Duration);

            m_OnTick?.Invoke(Remaining);
            if (IsCancelled)
            {
                return;
            }

            if (Remaining > 0f)
            {
                NextFireTime = time;
                return;
            }

            m_OnComplete?.Invoke();
            if (!IsCancelled)
            {
                Complete();
                Remaining = 0f;
                Progress = 1f;
                NextFireTime = 0d;
            }
        }

        protected override void OnAttached()
        {
            Elapsed = 0f;
            Remaining = Duration;
            Progress = 0f;
            NextFireTime = Module.GetClockTime(TickKind, UseUnscaledTime);
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
