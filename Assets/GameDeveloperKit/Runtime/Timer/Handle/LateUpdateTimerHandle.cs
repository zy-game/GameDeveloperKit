namespace GameDeveloperKit.Timer
{
    /// <summary>
    /// 定义 Late Update Timer Handle 类型。
    /// </summary>
    public sealed class LateUpdateTimerHandle : TimerUpdateHandle
    {
        /// <summary>
        /// 初始化 Late Update Timer Handle。
        /// </summary>
        /// <param name="callback">callback 参数。</param>
        public LateUpdateTimerHandle(System.Action callback) : base(callback)
        {
        }

        /// <summary>
        /// 初始化 Late Update Timer Handle。
        /// </summary>
        /// <param name="callback">callback 参数。</param>
        /// <param name="fps">fps 参数。</param>
        public LateUpdateTimerHandle(System.Action callback, float fps) : base(callback, fps)
        {
        }

        /// <summary>
        /// 初始化 Late Update Timer Handle。
        /// </summary>
        /// <param name="callback">callback 参数。</param>
        public LateUpdateTimerHandle(System.Action<TimerUpdateContext> callback) : base(callback)
        {
        }

        /// <summary>
        /// 初始化 Late Update Timer Handle。
        /// </summary>
        /// <param name="callback">callback 参数。</param>
        /// <param name="fps">fps 参数。</param>
        public LateUpdateTimerHandle(System.Action<TimerUpdateContext> callback, float fps) : base(callback, fps)
        {
        }

        /// <summary>
        /// 存储 Tick Kind。
        /// </summary>
        internal override TimerTickKind TickKind => TimerTickKind.LateUpdate;
    }
}
