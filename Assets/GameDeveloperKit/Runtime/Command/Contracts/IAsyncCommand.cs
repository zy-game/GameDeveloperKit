using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 异步命令接口，定义可异步执行命令的基本契约。
    /// </summary>
    public interface IAsyncCommand
    {
        /// <summary>
        /// 异步执行命令。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的UniTask。</returns>
        UniTask ExecuteAsync(CancellationToken cancellationToken = default);
    }
}
