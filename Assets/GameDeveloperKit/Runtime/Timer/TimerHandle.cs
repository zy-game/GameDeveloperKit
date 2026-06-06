using System;

namespace GameDeveloperKit.Timer
{
    public abstract class TimerHandle : IReference
    {
        private const float Epsilon = 0.000001f;

        internal TimerModule Module { get; private set; }

        internal bool Repeat { get; private set; }

        public object Owner { get; private set; }

        public string Tag { get; private set; }

        public bool UseUnscaledTime { get; private set; }

        public bool IsCancelled { get; private set; }

        public bool IsCompleted { get; private set; }

        public bool IsPaused { get; private set; }

        public float Delay { get; private set; }

        public float Interval { get; private set; }

        public float Duration { get; private set; }

        public float Elapsed { get; private set; }

        public float Remaining { get; private set; }

        public float Progress { get; private set; }

        public double NextFireTime { get; protected set; }

        public abstract void Execute(float deltaTime);

        public bool Cancel()
        {
            return Module != null ? Module.Cancel(this) : MarkCancelled();
        }

        public void Pause()
        {
            if (!IsCancelled && !IsCompleted)
            {
                IsPaused = true;
            }
        }

        public void Resume()
        {
            if (!IsCancelled && !IsCompleted)
            {
                IsPaused = false;
            }
        }

        public void Release()
        {
            Cancel();
        }

        internal void Schedule(
            TimerModule module,
            float delay,
            bool repeat,
            bool useUnscaledTime,
            object owner,
            string tag)
        {
            Module = module ?? throw new ArgumentNullException(nameof(module));
            Repeat = repeat;
            Owner = owner;
            Tag = tag;
            UseUnscaledTime = useUnscaledTime;
            IsCancelled = false;
            IsCompleted = false;
            IsPaused = false;
            Delay = delay;
            Interval = delay;
            Duration = delay;
            Elapsed = 0f;
            Remaining = delay;
            Progress = 0f;
            NextFireTime = module.Time + delay;
        }

        internal void Detach()
        {
            Module = null;
        }

        internal bool MarkCancelled()
        {
            if (IsCancelled || IsCompleted)
            {
                return false;
            }

            IsCancelled = true;
            return true;
        }

        internal virtual void Advance(float deltaTime, double time)
        {
            if (IsCancelled || IsCompleted || IsPaused)
            {
                return;
            }

            AdvanceElapsed(deltaTime);
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

            if (Repeat)
            {
                Elapsed = 0f;
                Remaining = Interval;
                Progress = 0f;
                NextFireTime = time + Interval;
                return;
            }

            Complete();
        }

        protected void AdvanceElapsed(float deltaTime)
        {
            Elapsed += deltaTime;
            Remaining = Math.Max(0f, Duration - Elapsed);
            Progress = Duration <= Epsilon ? 1f : Math.Min(1f, Elapsed / Duration);
        }

        protected void Complete()
        {
            IsCompleted = true;
            Remaining = 0f;
            Progress = 1f;
            NextFireTime = 0d;
        }
    }
}
