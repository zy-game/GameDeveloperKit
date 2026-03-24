using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    public readonly struct CommandStep
    {
        public CommandStep(IUndoableCommand syncCommand)
        {
            SyncCommand = syncCommand ?? throw new ArgumentNullException(nameof(syncCommand));
            AsyncCommand = null;
        }

        public CommandStep(IAsyncUndoableCommand asyncCommand)
        {
            SyncCommand = null;
            AsyncCommand = asyncCommand ?? throw new ArgumentNullException(nameof(asyncCommand));
        }

        public IUndoableCommand SyncCommand { get; }
        public IAsyncUndoableCommand AsyncCommand { get; }
        public bool IsAsync => AsyncCommand != null;

        public void Execute()
        {
            if (SyncCommand == null)
            {
                throw new InvalidOperationException("This command step can only execute asynchronously.");
            }

            SyncCommand.Execute();
        }

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

        public void Undo()
        {
            if (SyncCommand == null)
            {
                throw new InvalidOperationException("This command step can only undo asynchronously.");
            }

            SyncCommand.Undo();
        }

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
