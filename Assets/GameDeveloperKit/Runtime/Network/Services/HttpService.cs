using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// HTTP服务，提供HTTP请求的发送和管理功能
    /// </summary>
    public sealed class HttpService : NetworkService
    {
        private readonly Dictionary<string, string> _defaultHeaders = new(StringComparer.Ordinal);
        private NetworkPolicy _defaultPolicy = new();

        /// <summary>
        /// 获取基础URL
        /// </summary>
        public string BaseUrl { get; private set; }

        /// <summary>
        /// 获取默认超时时间（秒）
        /// </summary>
        public int DefaultTimeoutSeconds { get; private set; } = 30;

        /// <summary>
        /// 获取默认请求头的只读集合
        /// </summary>
        public IReadOnlyDictionary<string, string> DefaultHeaders => _defaultHeaders;

        /// <summary>
        /// 获取默认网络策略
        /// </summary>
        public NetworkPolicy DefaultPolicy => _defaultPolicy;

        /// <summary>
        /// 请求开始事件
        /// </summary>
        public event Action<NetworkRequest> RequestStarted;

        /// <summary>
        /// 请求完成事件
        /// </summary>
        public event Action<NetworkRequest, NetworkResponse> RequestCompleted;

        /// <summary>
        /// 请求失败事件
        /// </summary>
        public event Action<NetworkRequest, Exception> RequestFailed;

        /// <summary>
        /// 配置HTTP服务
        /// </summary>
        /// <param name="baseUrl">基础URL</param>
        /// <param name="defaultTimeoutSeconds">默认超时时间（秒）</param>
        /// <exception cref="FrameworkException">基础URL无效</exception>
        public void Configure(string baseUrl = null, int defaultTimeoutSeconds = 30)
        {
            if (!string.IsNullOrWhiteSpace(baseUrl)
                && (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
                    || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)))
            {
                throw new FrameworkException(FrameworkError.Create("NetworkBaseUrlInvalid", $"Base url '{baseUrl}' is invalid.", FrameworkFailureCategory.Configuration));
            }

            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.TrimEnd('/');
            DefaultTimeoutSeconds = defaultTimeoutSeconds > 0 ? defaultTimeoutSeconds : 30;
        }

        /// <summary>
        /// 配置网络策略
        /// </summary>
        /// <param name="policy">网络策略</param>
        public void ConfigurePolicy(NetworkPolicy policy)
        {
            _defaultPolicy = policy ?? new NetworkPolicy();
        }

        /// <summary>
        /// 设置默认请求头
        /// </summary>
        /// <param name="key">请求头键</param>
        /// <param name="value">请求头值</param>
        /// <exception cref="ArgumentException">请求头键为空</exception>
        public void SetDefaultHeader(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Header key can not be empty.", nameof(key));
            }

            _defaultHeaders[key] = value ?? string.Empty;
        }

        /// <summary>
        /// 移除默认请求头
        /// </summary>
        /// <param name="key">请求头键</param>
        /// <returns>如果移除成功返回true，否则返回false</returns>
        public bool RemoveDefaultHeader(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return _defaultHeaders.Remove(key);
        }

        /// <summary>
        /// 清除所有默认请求头
        /// </summary>
        public void ClearDefaultHeaders()
        {
            _defaultHeaders.Clear();
        }

        /// <summary>
        /// 异步发送GET请求
        /// </summary>
        /// <param name="url">请求URL</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>网络响应的异步任务</returns>
        public UniTask<NetworkResponse> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            return SendAsync(new NetworkRequest { Url = url, Method = NetworkMethod.Get }, cancellationToken);
        }

        /// <summary>
        /// 异步发送HEAD请求
        /// </summary>
        /// <param name="url">请求URL</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>网络响应的异步任务</returns>
        public UniTask<NetworkResponse> HeadAsync(string url, CancellationToken cancellationToken = default)
        {
            return SendAsync(new NetworkRequest { Url = url, Method = NetworkMethod.Head }, cancellationToken);
        }

        /// <summary>
        /// 异步发送POST JSON请求
        /// </summary>
        /// <param name="url">请求URL</param>
        /// <param name="json">JSON数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>网络响应的异步任务</returns>
        public UniTask<NetworkResponse> PostJsonAsync(string url, string json, CancellationToken cancellationToken = default)
        {
            return SendAsync(new NetworkRequest
            {
                Url = url,
                Method = NetworkMethod.Post,
                Body = json ?? string.Empty,
                ContentType = "application/json"
            }, cancellationToken);
        }

        /// <summary>
        /// 异步发送网络请求
        /// </summary>
        /// <param name="request">网络请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>网络响应的异步任务</returns>
        /// <exception cref="ArgumentNullException">请求为空</exception>
        /// <exception cref="InvalidOperationException">所有重试尝试失败</exception>
        public async UniTask<NetworkResponse> SendAsync(NetworkRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var url = ResolveUrl(request);
            var effectivePolicy = ResolvePolicy(request);
            EnsureTraceId(request, effectivePolicy);
            var startedAt = DateTimeOffset.UtcNow;
            var retryCount = Math.Max(0, effectivePolicy.RetryCount);

            for (var attempt = 0; attempt <= retryCount; attempt++)
            {
                using var unityRequest = CreateUnityWebRequest(url, request, effectivePolicy);
                ApplyHeaders(unityRequest, request, effectivePolicy);

                RequestStarted?.Invoke(request);

                try
                {
                    await unityRequest.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

                    var response = new NetworkResponse
                    {
                        Url = url,
                        TraceId = request.TraceId,
                        StatusCode = unityRequest.responseCode,
                        Stage = unityRequest.result == UnityWebRequest.Result.Success ? FrameworkOperationStage.Completed : FrameworkOperationStage.Failed,
                        IsSuccess = unityRequest.result == UnityWebRequest.Result.Success,
                        Text = unityRequest.downloadHandler?.text,
                        Data = unityRequest.downloadHandler?.data,
                        ErrorMessage = unityRequest.result == UnityWebRequest.Result.Success ? null : unityRequest.error,
                        Error = unityRequest.result == UnityWebRequest.Result.Success
                            ? null
                            : CreateNetworkError(unityRequest, url),
                        DurationMilliseconds = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds
                    };

                    RequestCompleted?.Invoke(request, response);
                    if (response.IsSuccess || !ShouldRetry(unityRequest, attempt, retryCount))
                    {
                        return response;
                    }
                }
                catch (Exception exception)
                {
                    RequestFailed?.Invoke(request, exception);
                    if (attempt >= retryCount)
                    {
                        throw;
                    }
                }
            }

            throw new InvalidOperationException("Network request failed after all retry attempts.");
        }

        /// <summary>
        /// 释放HTTP服务占用的资源
        /// </summary>
        protected override void OnDispose()
        {
            _defaultHeaders.Clear();
            BaseUrl = null;
            DefaultTimeoutSeconds = 30;
            _defaultPolicy = new NetworkPolicy();
            RequestStarted = null;
            RequestCompleted = null;
            RequestFailed = null;
        }

        private UnityWebRequest CreateUnityWebRequest(string url, NetworkRequest request, NetworkPolicy policy)
        {
            var unityRequest = new UnityWebRequest(url, ToHttpMethod(request.Method))
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = request.TimeoutSeconds > 0
                    ? request.TimeoutSeconds
                    : policy.TimeoutSecondsOverride > 0 ? policy.TimeoutSecondsOverride : DefaultTimeoutSeconds
            };

            if (!string.IsNullOrEmpty(request.Body))
            {
                var bytes = Encoding.UTF8.GetBytes(request.Body);
                unityRequest.uploadHandler = new UploadHandlerRaw(bytes);
                unityRequest.uploadHandler.contentType = string.IsNullOrWhiteSpace(request.ContentType)
                    ? "application/json"
                    : request.ContentType;
            }

            return unityRequest;
        }

        private void ApplyHeaders(UnityWebRequest unityRequest, NetworkRequest request, NetworkPolicy policy)
        {
            foreach (var pair in _defaultHeaders)
            {
                unityRequest.SetRequestHeader(pair.Key, pair.Value);
            }

            foreach (var pair in request.Headers)
            {
                unityRequest.SetRequestHeader(pair.Key, pair.Value);
            }

            if (!string.IsNullOrWhiteSpace(policy.AuthorizationHeaderValue))
            {
                unityRequest.SetRequestHeader("Authorization", policy.AuthorizationHeaderValue);
            }

            if (!string.IsNullOrWhiteSpace(request.TraceId))
            {
                unityRequest.SetRequestHeader("X-Trace-Id", request.TraceId);
            }
        }

        private string ResolveUrl(NetworkRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                throw new FrameworkException(FrameworkError.Create("NetworkUrlMissing", "Request url can not be empty.", FrameworkFailureCategory.Configuration));
            }

            if (!request.UseBaseUrl || string.IsNullOrWhiteSpace(BaseUrl) || Uri.TryCreate(request.Url, UriKind.Absolute, out _))
            {
                return EnsureAbsoluteUrl(request.Url);
            }

            if (request.Url.StartsWith("/", StringComparison.Ordinal))
            {
                return EnsureAbsoluteUrl(BaseUrl + request.Url);
            }

            return EnsureAbsoluteUrl(BaseUrl + "/" + request.Url);
        }

        private static string ToHttpMethod(NetworkMethod method)
        {
            switch (method)
            {
                case NetworkMethod.Get:
                    return UnityWebRequest.kHttpVerbGET;
                case NetworkMethod.Post:
                    return UnityWebRequest.kHttpVerbPOST;
                case NetworkMethod.Put:
                    return UnityWebRequest.kHttpVerbPUT;
                case NetworkMethod.Delete:
                    return UnityWebRequest.kHttpVerbDELETE;
                case NetworkMethod.Patch:
                    return "PATCH";
                case NetworkMethod.Head:
                    return UnityWebRequest.kHttpVerbHEAD;
                default:
                    throw new ArgumentOutOfRangeException(nameof(method), method, null);
            }
        }

        private static FrameworkError CreateNetworkError(UnityWebRequest request, string url)
        {
            var errorCode = request.responseCode switch
            {
                401 => "NetworkUnauthorized",
                403 => "NetworkForbidden",
                404 => "NetworkNotFound",
                >= 500 => "NetworkServerError",
                _ => request.result switch
                {
                    UnityWebRequest.Result.ConnectionError => "NetworkConnectionError",
                    UnityWebRequest.Result.ProtocolError => "NetworkProtocolError",
                    UnityWebRequest.Result.DataProcessingError => "NetworkDataProcessingError",
                    _ => "NetworkRequestFailed"
                }
            };

            var retryable = request.result == UnityWebRequest.Result.ConnectionError || request.responseCode >= 500;
            return FrameworkError.Create(errorCode, request.error, FrameworkFailureCategory.Network, retryable, url, stage: FrameworkOperationStage.Failed);
        }

        private NetworkPolicy ResolvePolicy(NetworkRequest request)
        {
            return request.Policy ?? _defaultPolicy ?? new NetworkPolicy();
        }

        private static void EnsureTraceId(NetworkRequest request, NetworkPolicy policy)
        {
            if (!string.IsNullOrWhiteSpace(request.TraceId) || !policy.GenerateTraceId)
            {
                return;
            }

            request.TraceId = Guid.NewGuid().ToString("N");
        }

        private static bool ShouldRetry(UnityWebRequest request, int attempt, int retryCount)
        {
            if (attempt >= retryCount)
            {
                return false;
            }

            return request.result == UnityWebRequest.Result.ConnectionError || request.responseCode >= 500;
        }

        private static string EnsureAbsoluteUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return url;
            }

            throw new FrameworkException(FrameworkError.Create("NetworkUrlInvalid", $"Resolved request url '{url}' is invalid.", FrameworkFailureCategory.Configuration));
        }
    }
}
