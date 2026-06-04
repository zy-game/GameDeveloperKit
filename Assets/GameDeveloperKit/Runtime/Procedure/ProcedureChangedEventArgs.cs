namespace GameDeveloperKit.Procedure
{
    /// <summary>
    /// 流程变化事件参数。
    /// </summary>
    public readonly struct ProcedureChangedEventArgs
    {
        /// <summary>
        /// 初始化流程变化事件参数。
        /// </summary>
        /// <param name="previous">上一个流程。</param>
        /// <param name="current">当前流程。</param>
        /// <param name="userData">切换参数。</param>
        public ProcedureChangedEventArgs(ProcedureBase previous, ProcedureBase current, object userData)
        {
            Previous = previous;
            Current = current;
            UserData = userData;
        }

        /// <summary>
        /// 上一个流程。
        /// </summary>
        public ProcedureBase Previous { get; }

        /// <summary>
        /// 当前流程。
        /// </summary>
        public ProcedureBase Current { get; }

        /// <summary>
        /// 切换参数。
        /// </summary>
        public object UserData { get; }
    }
}
