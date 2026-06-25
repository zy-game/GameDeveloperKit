using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络模块，负责 socket channel 管理和 HTTP 请求封装。
    /// </summary>
    public sealed class NetworkModule : GameModuleBase
    {
        /// <summary>
        /// 存储 Channels。
        /// </summary>
        private readonly Dictionary<string, NetworkChannel> m_Channels = new Dictionary<string, NetworkChannel>(StringComparer.Ordinal);

        /// <summary>
        /// 启动 member。
        /// </summary>
        public override void Startup()
        {
        }

        /// <summary>
        /// 关闭 member。
        /// </summary>
        public override void Shutdown()
        {
            var channels = new List<NetworkChannel>(m_Channels.Values);
            foreach (var channel in channels)
            {
                channel.Release();
            }

            m_Channels.Clear();
        }

        /// <summary>
        /// 创建 Channel。
        /// </summary>
        /// <param name="name">name 参数。</param>
        /// <param name="endpoint">endpoint 参数。</param>
        /// <param name="codec">codec 参数。</param>
        /// <returns>执行结果。</returns>
        public IChannel CreateChannel(string name, NetworkEndpoint endpoint, INetworkCodec codec = null)
        {
            return CreateChannel(name, endpoint, codec, null);
        }

        /// <summary>
        /// 创建 Channel。
        /// </summary>
        /// <param name="name">name 参数。</param>
        /// <param name="endpoint">endpoint 参数。</param>
        /// <param name="codec">codec 参数。</param>
        /// <param name="transport">transport 参数。</param>
        /// <returns>执行结果。</returns>
        internal NetworkChannel CreateChannel(string name, NetworkEndpoint endpoint, INetworkCodec codec, INetworkTransport transport)
        {
            ValidateName(name);
            ValidateEndpoint(endpoint);
            if (m_Channels.ContainsKey(name))
            {
                throw new GameException($"Network channel '{name}' has already been created.");
            }

            var channel = new NetworkChannel(name, endpoint, codec ?? new MemoryPackNetworkCodec(), transport ?? new NullNetworkTransport());
            m_Channels.Add(name, channel);
            return channel;
        }

        /// <summary>
        /// 尝试获取 Channel。
        /// </summary>
        /// <param name="name">name 参数。</param>
        /// <param name="channel">channel 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        public bool TryGetChannel(string name, out IChannel channel)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                channel = null;
                return false;
            }

            if (m_Channels.TryGetValue(name, out var value))
            {
                channel = value;
                return true;
            }

            channel = null;
            return false;
        }

        /// <summary>
        /// 获取 Channel。
        /// </summary>
        /// <param name="name">name 参数。</param>
        /// <returns>执行结果。</returns>
        public IChannel GetChannel(string name)
        {
            ValidateName(name);
            if (m_Channels.TryGetValue(name, out var channel))
            {
                return channel;
            }

            throw new GameException($"Network channel '{name}' does not exist.");
        }

        /// <summary>
        /// 执行 Close Channel Async。
        /// </summary>
        /// <param name="name">name 参数。</param>
        /// <returns>操作完成任务。</returns>
        public async UniTask CloseChannelAsync(string name)
        {
            ValidateName(name);
            if (!m_Channels.TryGetValue(name, out var channel))
            {
                return;
            }

            m_Channels.Remove(name);
            await channel.CloseAsync();
            channel.Release();
        }

        /// <summary>
        /// 执行 Send Http Async。
        /// </summary>
        /// <param name="request">request 参数。</param>
        /// <returns>操作完成任务。</returns>
        public async UniTask<HttpResponse> SendHttpAsync(HttpRequest request)
        {
            ValidateHttpRequest(request);
            using (var webRequest = new UnityWebRequest(request.Url, ToUnityMethod(request.Method)))
            {
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                if (request.Body != null)
                {
                    webRequest.uploadHandler = new UploadHandlerRaw(request.Body);
                }

                if (request.Headers != null)
                {
                    foreach (var pair in request.Headers)
                    {
                        webRequest.SetRequestHeader(pair.Key, pair.Value);
                    }
                }

                if (request.Timeout > TimeSpan.Zero)
                {
                    webRequest.timeout = Math.Max(1, (int)Math.Ceiling(request.Timeout.TotalSeconds));
                }

                try
                {
                    await webRequest.SendWebRequest();
                }
                catch (UnityWebRequestException exception)
                {
                    throw CreateHttpException(webRequest, exception.Message);
                }

                var response = new HttpResponse(
                    webRequest.responseCode,
                    webRequest.GetResponseHeaders(),
                    webRequest.downloadHandler?.data);

                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    throw CreateHttpException(webRequest);
                }

                if (webRequest.responseCode < 200L || webRequest.responseCode >= 300L)
                {
                    throw new NetworkException(
                        webRequest.error ?? $"Unexpected HTTP status code {webRequest.responseCode}.",
                        NetworkFailureKind.HttpStatus,
                        webRequest.responseCode);
                }

                return response;
            }
        }

        /// <summary>
        /// 校验 Name。
        /// </summary>
        /// <param name="name">name 参数。</param>
        private static void ValidateName(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Network channel name cannot be empty.", nameof(name));
            }
        }

        /// <summary>
        /// 校验 Endpoint。
        /// </summary>
        /// <param name="endpoint">endpoint 参数。</param>
        private static void ValidateEndpoint(NetworkEndpoint endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }
        }

        /// <summary>
        /// 校验 Http Request。
        /// </summary>
        /// <param name="request">request 参数。</param>
        private static void ValidateHttpRequest(HttpRequest request)
        {
            if (request.Url == null)
            {
                throw new ArgumentNullException(nameof(request.Url));
            }

            if (string.IsNullOrWhiteSpace(request.Url))
            {
                throw new ArgumentException("HTTP url cannot be empty.", nameof(request));
            }

            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("HTTP url must be an absolute HTTP or HTTPS url.", nameof(request));
            }
        }

        /// <summary>
        /// 执行 To Unity Method。
        /// </summary>
        /// <param name="method">method 参数。</param>
        /// <returns>执行结果。</returns>
        private static string ToUnityMethod(NetworkHttpMethod method)
        {
            switch (method)
            {
                case NetworkHttpMethod.Get:
                    return UnityWebRequest.kHttpVerbGET;
                case NetworkHttpMethod.Post:
                    return UnityWebRequest.kHttpVerbPOST;
                case NetworkHttpMethod.Put:
                    return UnityWebRequest.kHttpVerbPUT;
                case NetworkHttpMethod.Patch:
                    return "PATCH";
                case NetworkHttpMethod.Delete:
                    return UnityWebRequest.kHttpVerbDELETE;
                case NetworkHttpMethod.Head:
                    return "HEAD";
                default:
                    throw new ArgumentException("HTTP method is invalid.", nameof(method));
            }
        }

        /// <summary>
        /// 创建 Http Exception。
        /// </summary>
        /// <param name="request">request 参数。</param>
        /// <param name="message">message 参数。</param>
        /// <returns>执行结果。</returns>
        private static NetworkException CreateHttpException(UnityWebRequest request, string message = null)
        {
            var kind = request.result == UnityWebRequest.Result.ProtocolError
                ? NetworkFailureKind.HttpStatus
                : request.result == UnityWebRequest.Result.DataProcessingError
                    ? NetworkFailureKind.InvalidResponse
                    : NetworkFailureKind.Receive;
            return new NetworkException(
                message ?? request.error ?? $"Unexpected HTTP status code {request.responseCode}.",
                kind,
                request.responseCode);
        }
    }
}
