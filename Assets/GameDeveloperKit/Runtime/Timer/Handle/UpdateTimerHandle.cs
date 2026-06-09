namespace GameDeveloperKit.Timer
{
    public sealed class UpdateTimerHandle : TimerUpdateHandle
    {
        public UpdateTimerHandle(System.Action callback) : base(callback)
        {
        }

        public UpdateTimerHandle(System.Action<TimerUpdateContext> callback) : base(callback)
        {
        }

        internal override TimerTickKind TickKind => TimerTickKind.Update;
    }
}
