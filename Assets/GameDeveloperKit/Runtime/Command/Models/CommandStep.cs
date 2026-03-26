using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 命令步骤结构体，表示复合命令中的一个步骤，支持同步和异步命令。
    /// </summary>
    public readonly struct CommandStep
    {
        /// <summary>
        /// 初始化命令步骤的新实例，使用同步命令。
        /// </summary>
        /// <param name="syncCommand">同步可撤销命令。</param>
        /// <exception cref="ArgumentNullException">当命令为null时抛出。</exception>
        public CommandStep(IUndoableCommand syncCommand)
        {
            SyncCommand = syncCommand ?? throw new ArgumentNullException(nameof(syncCommand));
            AsyncCommand = null;
        }

        /// <summary>
        /// 初始化命令步骤的新实例，使用异步命令。
        /// </summary>
        /// <param name="asyncCommand">异步可撤销命令。</param>
        /// <exception cref="ArgumentNullException">当命令为null时抛出。</exception>
        public CommandStep(IAsyncUndoableCommand asyncCommand)
        {
            SyncCommand = null;
            AsyncCommand = asyncCommand ?? throw new ArgumentNullException(nameof(asyncCommand));
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
        /// 获取是否为异步命令。
        /// </summary>
        public bool IsAsync => AsyncCommand != null;

        /// <summary>
        /// 执行命令。
        /// </summary>
        /// <exception cref="InvalidOperationException">当只能异步执行时抛出。</exception>
        public void Execute()
        {
            if (SyncCommand == null)
            {
                throw new InvalidOperationException("This command step can only execute asynchronously.");
            }

            SyncCommand.Execute();
        }

        /// <summary>
        /// 异步执行命令。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public async UniTask ExecuteAsync(CancellationToken cancellationToken = default)
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
                throw new InvalidOperationException("This command step can only undo asynchronously.");
            }

            SyncCommand.Undo();
        }

        /// <summary>
        /// 异步撤销命令。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public async UniTask UndoAsync(CancellationToken cancellationToken = default)
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
