using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    public sealed partial class CommandHistory
    {
        /// <summary>
        /// 历史条目结构体，存储命令历史中的一个条目。
        /// </summary>
        private readonly struct HistoryEntry
        {
            /// <summary>
            /// 初始化历史条目的新实例，使用同步命令。
            /// </summary>
            /// <param name="syncCommand">同步可撤销命令。</param>
            private HistoryEntry(IUndoableCommand syncCommand)
            {
                SyncCommand = syncCommand;
                AsyncCommand = null;
            }

            /// <summary>
            /// 初始化历史条目的新实例，使用异步命令。
            /// </summary>
            /// <param name="asyncCommand">异步可撤销命令。</param>
            private HistoryEntry(IAsyncUndoableCommand asyncCommand)
            {
                SyncCommand = null;
                AsyncCommand = asyncCommand;
            }

            /// <summary>
            /// 获取同步命令。
            /// </summary>
            public IUndoableCommand SyncCommand { get; }

            /// <summary>
            /// 获取异步命令。
            /// </summary>
            public IAsyncUndoableCommand AsyncCommand { get; }

            /// <summary>
            /// 获取命令对象。
            /// </summary>
            public object Command => (object)AsyncCommand ?? SyncCommand;

            /// <summary>
            /// 获取是否为异步命令。
            /// </summary>
            public bool IsAsync => AsyncCommand != null;

            /// <summary>
            /// 从同步命令创建历史条目。
            /// </summary>
            /// <param name="command">同步命令。</param>
            /// <returns>历史条目。</returns>
            public static HistoryEntry Create(IUndoableCommand command)
            {
                return new HistoryEntry(command);
            }

            /// <summary>
            /// 从异步命令创建历史条目。
            /// </summary>
            /// <param name="command">异步命令。</param>
            /// <returns>历史条目。</returns>
            public static HistoryEntry Create(IAsyncUndoableCommand command)
            {
                return new HistoryEntry(command);
            }

            /// <summary>
            /// 执行命令。
            /// </summary>
            /// <exception cref="InvalidOperationException">当只能异步执行时抛出。</exception>
            public void Execute()
            {
                if (SyncCommand == null)
                {
                    throw new InvalidOperationException("The next command can only be redone asynchronously.");
                }

                SyncCommand.Execute();
            }

            /// <summary>
            /// 异步执行命令。
            /// </summary>
            /// <param name="cancellationToken">取消令牌。</param>
            /// <returns>异步任务。</returns>
            public async UniTask ExecuteAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (AsyncCommand != null)
                {
                    await AsyncCommand.ExecuteAsync(cancellationToken);
                    return;
                }

                SyncCommand.Execute();
            }

            /// <summary>
            /// 撤销命令。
            /// </summary>
            /// <exception cref="InvalidOperationException">当只能异步撤销时抛出。</exception>
            public void Undo()
            {
                if (SyncCommand == null)
                {
                    throw new InvalidOperationException("The next command can only be undone asynchronously.");
                }

                SyncCommand.Undo();
            }

            /// <summary>
            /// 异步撤销命令。
            /// </summary>
            /// <param name="cancellationToken">取消令牌。</param>
            /// <returns>异步任务。</returns>
            public async UniTask UndoAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (AsyncCommand != null)
                {
                    await AsyncCommand.UndoAsync(cancellationToken);
                    return;
                }

                SyncCommand.Undo();
            }
        }
    }
}
