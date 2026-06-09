namespace GameDeveloperKit.Timer
{
    public sealed class FixedUpdateTimerHandle : TimerUpdateHandle
    {
        public FixedUpdateTimerHandle(System.Action callback) : base(callback)
        {
        }

        public FixedUpdateTimerHandle(System.Action callback, float fps) : base(callback, fps)
        {
        }

        public FixedUpdateTimerHandle(System.Action<TimerUpdateContext> callback) : base(callback)
        {
        }

        public FixedUpdateTimerHandle(System.Action<TimerUpdateContext> callback, float fps) : base(callback, fps)
        {
        }

        internal override TimerTickKind TickKind => TimerTickKind.FixedUpdate;
    }
}
