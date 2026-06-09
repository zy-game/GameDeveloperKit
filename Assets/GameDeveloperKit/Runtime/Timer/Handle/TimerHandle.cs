using System;

namespace GameDeveloperKit.Timer
{
    public abstract class TimerHandle : IReference
    {
        internal TimerModule Module { get; private set; }

        public object Owner { get; private set; }

        public string Tag { get; private set; }

        internal abstract TimerTickKind TickKind { get; }

        public bool IsCancelled { get; private set; }

        public bool IsCompleted { get; private set; }

        public bool IsPaused { get; private set; }

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

        internal void Attach(TimerModule module, object owner, string tag)
        {
            Module = module ?? throw new ArgumentNullException(nameof(module));
            Owner = owner;
            Tag = tag;
            IsCancelled = false;
            IsCompleted = false;
            IsPaused = false;
            OnAttached();
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

        internal abstract void Advance(in TimerUpdateContext context, float phaseUnscaledDeltaTime);

        protected virtual void OnAttached()
        {
        }

        protected bool CanAdvance()
        {
            return !IsCancelled && !IsCompleted && !IsPaused;
        }

        protected void Complete()
        {
            IsCompleted = true;
        }
    }
}
