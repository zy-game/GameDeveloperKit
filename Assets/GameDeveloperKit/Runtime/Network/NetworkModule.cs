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
        private readonly Dictionary<string, NetworkChannel> m_Channels = new Dictionary<string, NetworkChannel>(StringComparer.Ordinal);

        public override UniTask Startup()
        {
            return UniTask.CompletedTask;
        }

        public override async UniTask Shutdown()
        {
            var channels = new List<NetworkChannel>(m_Channels.Values);
            foreach (var channel in channels)
            {
                await channel.CloseAsync();
                channel.Release();
            }

            m_Channels.Clear();
        }

        public IChannel CreateChannel(string name, NetworkEndpoint endpoint, INetworkCodec codec = null)
        {
            return CreateChannel(name, endpoint, codec, null);
        }

        internal NetworkChannel CreateChannel(string name, NetworkEndpoint endpoint, INetworkCodec codec, INetworkTransport transport)
        {
            ValidateName(name);
            ValidateEndpoint(endpoint);
            if (m_Channels.ContainsKey(name))
            {
                throw new GameException($"Network channel '{name}' has already been created.");
            }

            var channel = new NetworkChannel(name, endpoint, codec ?? new JsonNetworkCodec(), transport ?? new NullNetworkTransport());
            m_Channels.Add(name, channel);
            return channel;
        }

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

        public IChannel GetChannel(string name)
        {
            ValidateName(name);
            if (m_Channels.TryGetValue(name, out var channel))
            {
                return channel;
            }

            throw new GameException($"Network channel '{name}' does not exist.");
        }

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

        private static void ValidateEndpoint(NetworkEndpoint endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }
        }

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
