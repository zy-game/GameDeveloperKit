using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Command
{
    /// <summary>
    /// 可执行、可撤销、可重做的命令契约。
    /// </summary>
    public interface ICommand : IReference
    {
        /// <summary>
        /// 命令名称，用于展示当前可撤销或可重做的历史项。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 命令成功执行后对历史栈的影响方式。
        /// </summary>
        CommandHistoryMode HistoryMode { get; }

        /// <summary>
        /// 执行命令。
        /// </summary>
        /// <returns>执行任务。</returns>
        UniTask ExecuteAsync();

        /// <summary>
        /// 撤销命令。
        /// </summary>
        /// <returns>撤销任务。</returns>
        UniTask UndoAsync();

        /// <summary>
        /// 重做命令。
        /// </summary>
        /// <returns>重做任务。</returns>
        UniTask RedoAsync();
    }
}
