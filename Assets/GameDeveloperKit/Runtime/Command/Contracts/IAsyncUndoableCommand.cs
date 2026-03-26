using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 可撤销的异步命令接口，定义可以异步撤销的命令契约。
    /// </summary>
    public interface IAsyncUndoableCommand : IAsyncCommand
    {
        /// <summary>
        /// 异步撤销命令。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的UniTask。</returns>
        UniTask UndoAsync(CancellationToken cancellationToken = default);
    }
}
