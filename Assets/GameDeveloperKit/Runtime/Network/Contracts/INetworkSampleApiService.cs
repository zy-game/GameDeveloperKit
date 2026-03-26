using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 示例网络API服务接口，用于演示网络请求的基本用法。
    /// </summary>
    public interface INetworkSampleApiService
    {
        /// <summary>
        /// 通过GET请求发送回显消息。
        /// </summary>
        /// <param name="message">回显消息。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>网络服务结果的异步任务，包含回显响应。</returns>
        UniTask<NetworkServiceResult<NetworkSampleEchoResponse>> GetEchoAsync(string message, CancellationToken cancellationToken = default);

        /// <summary>
        /// 通过POST请求发送回显消息。
        /// </summary>
        /// <param name="message">回显消息。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>网络服务结果的异步任务，包含回显响应。</returns>
        UniTask<NetworkServiceResult<NetworkSampleEchoResponse>> PostEchoAsync(string message, CancellationToken cancellationToken = default);
    }
}
