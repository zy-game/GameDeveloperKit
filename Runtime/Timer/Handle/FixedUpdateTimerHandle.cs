namespace GameDeveloperKit.Timer
{
    public sealed class FixedUpdateTimerHandle : TimerUpdateHandle
    {
        /// <summary>
        /// 初始化 Fixed Update Timer Handle。
        /// </summary>
        public FixedUpdateTimerHandle(System.Action callback) : base(callback)
        {
        }

        /// <summary>
        /// 初始化 Fixed Update Timer Handle。
        /// </summary>
        public FixedUpdateTimerHandle(System.Action callback, float fps) : base(callback, fps)
        {
        }

        /// <summary>
        /// 初始化 Fixed Update Timer Handle。
        /// </summary>
        public FixedUpdateTimerHandle(System.Action<TimerUpdateContext> callback) : base(callback)
        {
        }

        /// <summary>
        /// 初始化 Fixed Update Timer Handle。
        /// </summary>
        public FixedUpdateTimerHandle(System.Action<TimerUpdateContext> callback, float fps) : base(callback, fps)
        {
        }
        internal override TimerTickKind TickKind => TimerTickKind.FixedUpdate;
    }
}
