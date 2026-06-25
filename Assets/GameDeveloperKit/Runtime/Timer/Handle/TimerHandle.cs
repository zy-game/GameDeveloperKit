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

        /// <summary>
        /// 执行 Cancel。
        /// </summary>
        public bool Cancel()
        {
            return Module != null ? Module.Cancel(this) : MarkCancelled();
        }

        /// <summary>
        /// 执行 Pause。
        /// </summary>
        public void Pause()
        {
            if (!IsCancelled && !IsCompleted)
            {
                IsPaused = true;
            }
        }

        /// <summary>
        /// 执行 Resume。
        /// </summary>
        public void Resume()
        {
            if (!IsCancelled && !IsCompleted)
            {
                IsPaused = false;
            }
        }

        /// <summary>
        /// 执行 Release。
        /// </summary>
        public void Release()
        {
            Cancel();
        }

        /// <summary>
        /// 执行 Attach。
        /// </summary>
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

        /// <summary>
        /// 执行 Detach。
        /// </summary>
        internal void Detach()
        {
            Module = null;
        }

        /// <summary>
        /// 执行 Mark Cancelled。
        /// </summary>
        internal bool MarkCancelled()
        {
            if (IsCancelled || IsCompleted)
            {
                return false;
            }

            IsCancelled = true;
            return true;
        }

        /// <summary>
        /// 执行 Advance。
        /// </summary>
        /// <param name="phaseUnscaledDeltaTime">phase Unscaled Delta Time 参数。</param>
        internal abstract void Advance(in TimerUpdateContext context, float phaseUnscaledDeltaTime);

        /// <summary>
        /// 处理 Attached 回调。
        /// </summary>
        protected virtual void OnAttached()
        {
        }

        /// <summary>
        /// 执行 Can Advance。
        /// </summary>
        protected bool CanAdvance()
        {
            return !IsCancelled && !IsCompleted && !IsPaused;
        }

        /// <summary>
        /// 执行 Complete。
        /// </summary>
        protected void Complete()
        {
            IsCompleted = true;
        }
    }
}
