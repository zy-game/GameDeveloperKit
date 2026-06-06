using System;

namespace GameDeveloperKit.Timer
{
    public sealed class TimerIntervalHandle : TimerHandle
    {
        private readonly Action<float> m_Callback;

        internal TimerIntervalHandle(Action<float> callback)
        {
            m_Callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        internal Action<float> Callback => m_Callback;

        public override void Execute(float deltaTime)
        {
            m_Callback(deltaTime);
        }
    }
}
