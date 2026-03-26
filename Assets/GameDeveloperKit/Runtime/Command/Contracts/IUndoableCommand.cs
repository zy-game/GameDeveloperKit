namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 可撤销命令接口，定义可以撤销的命令契约。
    /// </summary>
    public interface IUndoableCommand : ICommand
    {
        /// <summary>
        /// 撤销命令。
        /// </summary>
        void Undo();
    }
}
