using System;

namespace GameDeveloperKit.Timer
{
    public sealed class TimerDelayHandle : TimerHandle
    {
        private const float Epsilon = 0.000001f;
        private readonly Action m_Callback;

        /// <summary>
        /// 初始化 Timer Delay Handle。
        /// </summary>
        /// <param name="useUnscaledTime">use Unscaled Time 参数。</param>
        public TimerDelayHandle(float delay, Action callback, bool useUnscaledTime = false)
        {
            ValidateDuration(delay, nameof(delay));
            Delay = delay;
            m_Callback = callback ?? throw new ArgumentNullException(nameof(callback));
            UseUnscaledTime = useUnscaledTime;
        }

        internal override TimerTickKind TickKind => TimerTickKind.Update;

        public float Delay { get; }

        public bool UseUnscaledTime { get; }

        public float Elapsed { get; private set; }

        public float Remaining { get; private set; }

        public float Progress { get; private set; }

        public double NextFireTime { get; private set; }
        /// <summary>
        /// 执行 Advance。
        /// </summary>
        /// <param name="phaseUnscaledDeltaTime">phase Unscaled Delta Time 参数。</param>
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

            m_Callback();
            if (IsCancelled)
            {
                return;
            }

            Complete();
            Remaining = 0f;
            Progress = 1f;
            NextFireTime = 0d;
        }

        /// <summary>
        /// 处理 Attached 回调。
        /// </summary>
        protected override void OnAttached()
        {
            Elapsed = 0f;
            Remaining = Delay;
            Progress = 0f;
            NextFireTime = Module.GetClockTime(TickKind, UseUnscaledTime) + Delay;
        }

        /// <summary>
        /// 校验 Duration。
        /// </summary>
        /// <param name="paramName">param Name 参数。</param>
        private static void ValidateDuration(float value, string paramName)
        {
            if (value < 0f)
            {
                throw new ArgumentException("Timer duration cannot be negative.", paramName);
            }
        }
    }
}
