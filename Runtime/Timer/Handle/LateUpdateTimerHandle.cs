namespace GameDeveloperKit.Timer
{
    public sealed class LateUpdateTimerHandle : TimerUpdateHandle
    {
        /// <summary>
        /// 初始化 Late Update Timer Handle。
        /// </summary>
        public LateUpdateTimerHandle(System.Action callback) : base(callback)
        {
        }

        /// <summary>
        /// 初始化 Late Update Timer Handle。
        /// </summary>
        public LateUpdateTimerHandle(System.Action callback, float fps) : base(callback, fps)
        {
        }

        /// <summary>
        /// 初始化 Late Update Timer Handle。
        /// </summary>
        public LateUpdateTimerHandle(System.Action<TimerUpdateContext> callback) : base(callback)
        {
        }

        /// <summary>
        /// 初始化 Late Update Timer Handle。
        /// </summary>
        public LateUpdateTimerHandle(System.Action<TimerUpdateContext> callback, float fps) : base(callback, fps)
        {
        }
        internal override TimerTickKind TickKind => TimerTickKind.LateUpdate;
    }
}
