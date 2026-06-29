using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Command
{
    /// <summary>
    /// 命令基类，提供默认名称、历史模式和重做语义。
    /// </summary>
    public abstract class CommandBase : ICommand
    {
        /// <summary>
        /// 命令名称。
        /// </summary>
        public virtual string Name => GetType().Name;

        /// <summary>
        /// 命令历史模式。
        /// </summary>
        public virtual CommandHistoryMode HistoryMode => CommandHistoryMode.Undoable;

        /// <summary>
        /// 执行命令。
        /// </summary>
        /// <returns>执行任务。</returns>
        public abstract UniTask ExecuteAsync();

        /// <summary>
        /// 撤销命令。
        /// </summary>
        /// <returns>撤销任务。</returns>
        public abstract UniTask UndoAsync();

        /// <summary>
        /// 重做命令，默认复用执行逻辑。
        /// </summary>
        /// <returns>重做任务。</returns>
        public virtual UniTask RedoAsync()
        {
            return ExecuteAsync();
        }

        /// <summary>
        /// 释放命令。
        /// </summary>
        public virtual void Release()
        {
        }
    }
}
