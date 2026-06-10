using System;

namespace GameDeveloperKit.Timer
{
    /// <summary>
    /// 定义 Timer Delay Handle 类型。
    /// </summary>
    public sealed class TimerDelayHandle : TimerHandle
    {
        /// <summary>
        /// 定义 Epsilon 常量。
        /// </summary>
        private const float Epsilon = 0.000001f;

        /// <summary>
        /// 存储 Callback。
        /// </summary>
        private readonly Action m_Callback;
        /// <summary>
        /// 存储 Legacy Callback。
        /// </summary>
        private readonly Action<float> m_LegacyCallback;

        /// <summary>
        /// 初始化 Timer Delay Handle。
        /// </summary>
        /// <param name="delay">delay 参数。</param>
        /// <param name="callback">callback 参数。</param>
        /// <param name="useUnscaledTime">use Unscaled Time 参数。</param>
        public TimerDelayHandle(float delay, Action callback, bool useUnscaledTime = false)
        {
            ValidateDuration(delay, nameof(delay));
            Delay = delay;
            m_Callback = callback ?? throw new ArgumentNullException(nameof(callback));
            UseUnscaledTime = useUnscaledTime;
        }

        /// <summary>
        /// 初始化 Timer Delay Handle。
        /// </summary>
        /// <param name="delay">delay 参数。</param>
        /// <param name="callback">callback 参数。</param>
        /// <param name="useUnscaledTime">use Unscaled Time 参数。</param>
        public TimerDelayHandle(float delay, Action<float> callback, bool useUnscaledTime = false)
        {
            ValidateDuration(delay, nameof(delay));
            Delay = delay;
            m_LegacyCallback = callback ?? throw new ArgumentNullException(nameof(callback));
            UseUnscaledTime = useUnscaledTime;
        }

        /// <summary>
        /// 存储 Tick Kind。
        /// </summary>
        internal override TimerTickKind TickKind => TimerTickKind.Update;

        public float Delay { get; }

        public bool UseUnscaledTime { get; }

        public float Elapsed { get; private set; }

        public float Remaining { get; private set; }

        public float Progress { get; private set; }

        public double NextFireTime { get; private set; }

        /// <summary>
        /// 存储 Legacy Callback。
        /// </summary>
        internal Action<float> LegacyCallback => m_LegacyCallback;

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
        /// 执行 Execute。
        /// </summary>
        /// <param name="deltaTime">delta Time 参数。</param>
        private void Execute(float deltaTime)
        {
            if (m_Callback != null)
            {
                m_Callback();
                return;
            }

            m_LegacyCallback(deltaTime);
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
