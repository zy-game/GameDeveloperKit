using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 定义启动流程中的自定义任务。
    /// </summary>
    public interface IStartupTask
    {
        /// <summary>
        /// 异步执行启动任务。
        /// </summary>
        /// <param name="startup">当前启动管理器实例。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步执行过程的任务。</returns>
        UniTask ExecuteAsync(Startup startup, CancellationToken cancellationToken = default);
    }
}
