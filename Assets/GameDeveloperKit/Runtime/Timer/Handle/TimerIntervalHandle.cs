using System;

namespace GameDeveloperKit.Timer
{
    /// <summary>
    /// 定义 Timer Interval Handle 类型。
    /// </summary>
    public sealed class TimerIntervalHandle : TimerHandle
    {
        /// <summary>
        /// 定义 Epsilon 常量。
        /// </summary>
        private const float Epsilon = 0.000001f;

        /// <summary>
        /// 存储 Callback。
        /// </summary>
        private readonly Action<float> m_Callback;

        /// <summary>
        /// 初始化 Timer Interval Handle。
        /// </summary>
        /// <param name="interval">interval 参数。</param>
        /// <param name="callback">callback 参数。</param>
        /// <param name="useUnscaledTime">use Unscaled Time 参数。</param>
        public TimerIntervalHandle(float interval, Action<float> callback, bool useUnscaledTime = false)
        {
            ValidateDuration(interval, nameof(interval));
            Interval = interval;
            m_Callback = callback ?? throw new ArgumentNullException(nameof(callback));
            UseUnscaledTime = useUnscaledTime;
        }

        /// <summary>
        /// 存储 Tick Kind。
        /// </summary>
        internal override TimerTickKind TickKind => TimerTickKind.Update;

        public float Interval { get; }

        public bool UseUnscaledTime { get; }

        public float Elapsed { get; private set; }

        public float Remaining { get; private set; }

        public float Progress { get; private set; }

        public double NextFireTime { get; private set; }

        /// <summary>
        /// 存储 Callback。
        /// </summary>
        internal Action<float> Callback => m_Callback;

        /// <summary>
        /// 执行 Advance。
        /// </summary>
        /// <param name="context">context 参数。</param>
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
            if (Elapsed + Epsilon < Interval)
            {
                Remaining = Math.Max(0f, Interval - Elapsed);
                Progress = Interval <= Epsilon ? 1f : Math.Min(1f, Elapsed / Interval);
                NextFireTime = time + Remaining;
                return;
            }

            if (Interval <= Epsilon)
            {
                m_Callback(deltaTime);
                if (IsCancelled)
                {
                    return;
                }

                Elapsed = 0f;
                Remaining = 0f;
                Progress = 1f;
                NextFireTime = time;
                return;
            }

            while (Elapsed + Epsilon >= Interval)
            {
                m_Callback(Interval);
                if (IsCancelled)
                {
                    return;
                }

                Elapsed = Math.Max(0f, Elapsed - Interval);
            }

            Remaining = Math.Max(0f, Interval - Elapsed);
            Progress = Math.Min(1f, Elapsed / Interval);
            NextFireTime = time + Remaining;
        }

        /// <summary>
        /// 处理 Attached 回调。
        /// </summary>
        protected override void OnAttached()
        {
            Elapsed = 0f;
            Remaining = Interval;
            Progress = 0f;
            NextFireTime = Module.GetClockTime(TickKind, UseUnscaledTime) + Interval;
        }

        /// <summary>
        /// 校验 Duration。
        /// </summary>
        /// <param name="value">value 参数。</param>
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
