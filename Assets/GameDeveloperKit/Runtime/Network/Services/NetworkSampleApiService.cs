using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 示例网络 API 服务，用于演示 API 调用。
    /// </summary>
    /// <remarks>
    /// 此服务提供了示例的 Echo API，可以通过 GET 和 POST 方法测试网络功能。
    /// 实现了 INetworkSampleApiService 接口。
    /// </remarks>
    public sealed class NetworkSampleApiService : NetworkApiService, INetworkSampleApiService
    {
        /// <summary>
        /// 通过 GET 方法发送 Echo 请求。
        /// </summary>
        /// <param name="message">要发送的消息。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>包含服务器响应的 Echo 消息。</returns>
        /// <remarks>
        /// 此方法将消息作为查询参数发送到 /sample/echo 端点。
        /// 服务器会返回相同的消息作为响应。
        /// </remarks>
        public UniTask<NetworkServiceResult<NetworkSampleEchoResponse>> GetEchoAsync(string message, CancellationToken cancellationToken = default)
        {
            var request = CreateRequest($"/sample/echo?message={Uri.EscapeDataString(message ?? string.Empty)}", NetworkMethod.Get, "GetEcho");
            return SendAsync<NetworkSampleEchoResponse>(request, cancellationToken);
        }

        /// <summary>
        /// 通过 POST 方法发送 Echo 请求。
        /// </summary>
        /// <param name="message">要发送的消息。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>包含服务器响应的 Echo 消息。</returns>
        /// <remarks>
        /// 此方法将消息作为 JSON 请求体发送到 /sample/echo 端点。
        /// 服务器会返回相同的消息作为响应。
        /// </remarks>
        public UniTask<NetworkServiceResult<NetworkSampleEchoResponse>> PostEchoAsync(string message, CancellationToken cancellationToken = default)
        {
            var request = CreateRequest("/sample/echo", NetworkMethod.Post, "PostEcho");
            return SendJsonAsync<NetworkSampleEchoRequest, NetworkSampleEchoResponse>(request, new NetworkSampleEchoRequest
            {
                Message = message
            }, cancellationToken);
        }
    }
}
