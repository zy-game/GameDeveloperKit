using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    public interface ICommand
    {
        void Execute();
    }

    public interface IAsyncCommand
    {
        UniTask ExecuteAsync(CancellationToken cancellationToken = default);
    }
}
