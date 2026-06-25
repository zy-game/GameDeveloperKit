namespace GameDeveloperKit.Timer
{
    public class UpdateTimerHandle : TimerUpdateHandle
    {
        /// <summary>
        /// 初始化 Update Timer Handle。
        /// </summary>
        public UpdateTimerHandle(System.Action callback) : base(callback)
        {
        }

        /// <summary>
        /// 初始化 Update Timer Handle。
        /// </summary>
        public UpdateTimerHandle(System.Action<TimerUpdateContext> callback) : base(callback)
        {
        }
        internal override TimerTickKind TickKind => TimerTickKind.Update;
    }
}
