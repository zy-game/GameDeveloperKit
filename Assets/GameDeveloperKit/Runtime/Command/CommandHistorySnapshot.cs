namespace GameDeveloperKit.Command
{
    /// <summary>
    /// 命令历史状态快照。
    /// </summary>
    public readonly struct CommandHistorySnapshot
    {
        /// <summary>
        /// 初始化命令历史状态快照。
        /// </summary>
        /// <param name="canUndo">是否可以撤销。</param>
        /// <param name="canRedo">是否可以重做。</param>
        /// <param name="undoCount">撤销栈命令数量。</param>
        /// <param name="redoCount">重做栈命令数量。</param>
        /// <param name="undoName">当前可撤销命令名称。</param>
        /// <param name="redoName">当前可重做命令名称。</param>
        public CommandHistorySnapshot(bool canUndo, bool canRedo, int undoCount, int redoCount, string undoName, string redoName)
        {
            CanUndo = canUndo;
            CanRedo = canRedo;
            UndoCount = undoCount;
            RedoCount = redoCount;
            UndoName = undoName;
            RedoName = redoName;
        }

        /// <summary>
        /// 是否可以撤销。
        /// </summary>
        public bool CanUndo { get; }

        /// <summary>
        /// 是否可以重做。
        /// </summary>
        public bool CanRedo { get; }

        /// <summary>
        /// 撤销栈命令数量。
        /// </summary>
        public int UndoCount { get; }

        /// <summary>
        /// 重做栈命令数量。
        /// </summary>
        public int RedoCount { get; }

        /// <summary>
        /// 当前可撤销命令名称。
        /// </summary>
        public string UndoName { get; }

        /// <summary>
        /// 当前可重做命令名称。
        /// </summary>
        public string RedoName { get; }
    }
}
