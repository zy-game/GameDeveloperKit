using System;

namespace GameDeveloperKit.Timer
{
    public sealed class TimerCountdownHandle : TimerHandle
    {
        private readonly Action<float> m_OnTick;
        private readonly Action m_OnComplete;

        internal TimerCountdownHandle(Action<float> onTick, Action onComplete)
        {
            m_OnTick = onTick;
            m_OnComplete = onComplete;
        }

        public override void Execute(float deltaTime)
        {
            m_OnTick?.Invoke(Remaining);
        }

        internal override void Advance(float deltaTime, double time)
        {
            if (IsCancelled || IsCompleted || IsPaused)
            {
                return;
            }

            AdvanceElapsed(deltaTime);
            Execute(deltaTime);
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
            }
        }
    }
}
