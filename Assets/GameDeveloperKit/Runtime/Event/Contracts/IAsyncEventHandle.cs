using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 异步事件处理程序接口。
    /// </summary>
    public interface IAsyncEventHandle
    {
        /// <summary>
        /// 异步处理事件。
        /// </summary>
        /// <param name="context">事件上下文。</param>
        /// <returns>表示异步操作的UniTask。</returns>
        UniTask HandleAsync(IEventContext context);
    }
}
