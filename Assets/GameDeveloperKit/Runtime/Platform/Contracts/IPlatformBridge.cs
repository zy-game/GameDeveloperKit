using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 平台桥接接口，用于与不同平台（如移动平台）进行交互。
    /// </summary>
    public interface IPlatformBridge
    {
        /// <summary>
        /// 异步执行登录操作。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>平台操作结果的异步任务。</returns>
        UniTask<PlatformOperationResult> LoginAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步执行支付操作。
        /// </summary>
        /// <param name="productId">产品ID。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>平台操作结果的异步任务。</returns>
        UniTask<PlatformOperationResult> PayAsync(string productId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步执行分享操作。
        /// </summary>
        /// <param name="contentId">内容ID。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>平台操作结果的异步任务。</returns>
        UniTask<PlatformOperationResult> ShareAsync(string contentId, CancellationToken cancellationToken = default);
    }
}
