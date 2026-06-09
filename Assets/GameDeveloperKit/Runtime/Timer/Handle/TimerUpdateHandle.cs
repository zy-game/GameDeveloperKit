using System;

namespace GameDeveloperKit.Timer
{
    public abstract class TimerUpdateHandle : TimerHandle
    {
        private const float Epsilon = 0.000001f;

        private readonly Action m_Callback;
        private readonly Action<TimerUpdateContext> m_ContextCallback;
        private float m_MinInterval;
        private float m_ElapsedSinceLastInvoke;

        protected TimerUpdateHandle(Action callback)
        {
            m_Callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        protected TimerUpdateHandle(Action callback, float fps) : this(callback)
        {
            m_MinInterval = GetInterval(fps);
        }

        protected TimerUpdateHandle(Action<TimerUpdateContext> callback)
        {
            m_ContextCallback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        protected TimerUpdateHandle(Action<TimerUpdateContext> callback, float fps) : this(callback)
        {
            m_MinInterval = GetInterval(fps);
        }

        public bool Enabled { get; set; } = true;

        public long LastTick { get; private set; }

        public Exception LastException { get; private set; }

        public bool HasError => LastException != null;

        internal override void Advance(in TimerUpdateContext context, float phaseUnscaledDeltaTime)
        {
            if (!CanAdvance() || !Enabled)
            {
                return;
            }

            if (!CanPassFrequencyGate(phaseUnscaledDeltaTime))
            {
                return;
            }

            try
            {
                if (m_ContextCallback != null)
                {
                    m_ContextCallback(context);
                }
                else
                {
                    m_Callback();
                }

                LastTick = context.Tick;
            }
            catch (Exception exception)
            {
                LastTick = context.Tick;
                LastException = exception;
            }
        }

        protected override void OnAttached()
        {
            m_ElapsedSinceLastInvoke = 0f;
        }

        private bool CanPassFrequencyGate(float phaseUnscaledDeltaTime)
        {
            if (m_MinInterval <= 0f)
            {
                return true;
            }

            m_ElapsedSinceLastInvoke += Math.Max(0f, phaseUnscaledDeltaTime);
            if (m_ElapsedSinceLastInvoke + Epsilon < m_MinInterval)
            {
                return false;
            }

            m_ElapsedSinceLastInvoke = Math.Max(0f, m_ElapsedSinceLastInvoke - m_MinInterval);
            return true;
        }

        private static float GetInterval(float fps)
        {
            if (fps <= 0f)
            {
                throw new ArgumentException("Timer update fps must be greater than zero.", nameof(fps));
            }

            return 1f / fps;
        }
    }
}
