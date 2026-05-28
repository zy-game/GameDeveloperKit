namespace GameDeveloperKit.Command
{
    /// <summary>
    /// 命令成功执行后对历史栈的影响方式。
    /// </summary>
    public enum CommandHistoryMode : byte
    {
        /// <summary>
        /// 成功执行后进入撤销栈。
        /// </summary>
        Undoable = 0,

        /// <summary>
        /// 成功执行后不进入历史，也不清理已有历史。
        /// </summary>
        Transient = 1,

        /// <summary>
        /// 成功执行后清空撤销栈和重做栈。
        /// </summary>
        Barrier = 2,
    }
}
