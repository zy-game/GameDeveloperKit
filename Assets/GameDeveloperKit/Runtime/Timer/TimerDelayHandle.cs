using System;

namespace GameDeveloperKit.Timer
{
    public sealed class TimerDelayHandle : TimerHandle
    {
        private readonly Action m_Callback;
        private readonly Action<float> m_LegacyCallback;

        internal TimerDelayHandle(Action callback)
        {
            m_Callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        internal TimerDelayHandle(Action<float> callback)
        {
            m_LegacyCallback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        internal Action<float> LegacyCallback => m_LegacyCallback;

        public override void Execute(float deltaTime)
        {
            if (m_Callback != null)
            {
                m_Callback();
                return;
            }

            m_LegacyCallback(deltaTime);
        }
    }
}
