using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    public interface IUndoableCommand : ICommand
    {
        void Undo();
    }

    public interface IAsyncUndoableCommand : IAsyncCommand
    {
        UniTask UndoAsync(CancellationToken cancellationToken = default);
    }
}
