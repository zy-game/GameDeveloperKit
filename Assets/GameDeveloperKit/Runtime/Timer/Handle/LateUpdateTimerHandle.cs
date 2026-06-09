namespace GameDeveloperKit.Timer
{
    public sealed class LateUpdateTimerHandle : TimerUpdateHandle
    {
        public LateUpdateTimerHandle(System.Action callback) : base(callback)
        {
        }

        public LateUpdateTimerHandle(System.Action callback, float fps) : base(callback, fps)
        {
        }

        public LateUpdateTimerHandle(System.Action<TimerUpdateContext> callback) : base(callback)
        {
        }

        public LateUpdateTimerHandle(System.Action<TimerUpdateContext> callback, float fps) : base(callback, fps)
        {
        }

        internal override TimerTickKind TickKind => TimerTickKind.LateUpdate;
    }
}
