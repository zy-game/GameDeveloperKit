namespace GameDeveloperKit.Timer
{
    /// <summary>
    /// 定义 Update Timer Handle 类型。
    /// </summary>
    public sealed class UpdateTimerHandle : TimerUpdateHandle
    {
        /// <summary>
        /// 初始化 Update Timer Handle。
        /// </summary>
        /// <param name="callback">callback 参数。</param>
        public UpdateTimerHandle(System.Action callback) : base(callback)
        {
        }

        /// <summary>
        /// 初始化 Update Timer Handle。
        /// </summary>
        /// <param name="callback">callback 参数。</param>
        public UpdateTimerHandle(System.Action<TimerUpdateContext> callback) : base(callback)
        {
        }

        /// <summary>
        /// 存储 Tick Kind。
        /// </summary>
        internal override TimerTickKind TickKind => TimerTickKind.Update;
    }
}
