using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 提供基于 HTTP API 的网络服务基类。
    /// </summary>
    /// <remarks>
    /// 此类扩展自 NetworkService，提供了用于构建和发送 HTTP API 请求的辅助方法。
    /// 支持请求/响应的序列化和反序列化，自动处理 JSON 数据。
    /// 适合用于实现 RESTful API 客户端。
    /// </remarks>
    public abstract class NetworkApiService : NetworkService
    {
        /// <summary>
        /// 获取 HTTP 服务实例。
        /// </summary>
        /// <remarks>
        /// 如果网络模块未初始化，抛出 InvalidOperationException。
        /// </remarks>
        protected HttpService Http => Module?.Http ?? throw new InvalidOperationException("Network module is not initialized.");

        /// <summary>
        /// 创建网络服务请求对象。
        /// </summary>
        /// <param name="path">API 路径。</param>
        /// <param name="method">HTTP 方法。</param>
        /// <param name="operationName">操作名称，用于日志和诊断。</param>
        /// <returns>创建的请求对象。</returns>
        protected NetworkServiceRequest CreateRequest(string path, NetworkMethod method = NetworkMethod.Get, string operationName = null)
        {
            return new NetworkServiceRequest
            {
                Path = path,
                Method = method,
                OperationName = string.IsNullOrWhiteSpace(operationName) ? $"{method}:{path}" : operationName
            };
        }

        /// <summary>
        /// 异步发送 GET 请求。
        /// </summary>
        /// <param name="url">请求的完整 URL。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>网络响应对象。</returns>
        protected UniTask<NetworkResponse> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            return Http.GetAsync(url, cancellationToken);
        }

        /// <summary>
        /// 异步发送 JSON POST 请求。
        /// </summary>
        /// <param name="url">请求的完整 URL。</param>
        /// <param name="json">JSON 格式的请求体。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>网络响应对象。</returns>
        protected UniTask<NetworkResponse> PostJsonAsync(string url, string json, CancellationToken cancellationToken = default)
        {
            return Http.PostJsonAsync(url, json, cancellationToken);
        }

        /// <summary>
        /// 异步发送网络请求。
        /// </summary>
        /// <param name="request">网络请求对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>网络响应对象。</returns>
        protected UniTask<NetworkResponse> SendAsync(NetworkRequest request, CancellationToken cancellationToken = default)
        {
            return Http.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// 获取指定类型的服务实例。
        /// </summary>
        /// <typeparam name="TService">服务类型。</typeparam>
        /// <returns>服务实例，如果未找到则返回 null。</returns>
        protected TService GetService<TService>()
            where TService : class
        {
            return Module.GetService<TService>();
        }

        /// <summary>
        /// 异步发送 GET 请求并解析 JSON 响应。
        /// </summary>
        /// <typeparam name="TResponse">响应类型。</typeparam>
        /// <param name="path">API 路径。</param>
        /// <param name="operationName">操作名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>包含解析后的响应结果。</returns>
        protected UniTask<NetworkServiceResult<TResponse>> GetJsonAsync<TResponse>(string path, string operationName = null, CancellationToken cancellationToken = default)
        {
            return SendAsync<TResponse>(CreateRequest(path, NetworkMethod.Get, operationName), cancellationToken);
        }

        /// <summary>
        /// 异步发送 JSON POST 请求并解析 JSON 响应。
        /// </summary>
        /// <typeparam name="TRequest">请求类型。</typeparam>
        /// <typeparam name="TResponse">响应类型。</typeparam>
        /// <param name="path">API 路径。</param>
        /// <param name="payload">请求对象。</param>
        /// <param name="operationName">操作名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>包含解析后的响应结果。</returns>
        protected UniTask<NetworkServiceResult<TResponse>> PostJsonAsync<TRequest, TResponse>(string path, TRequest payload, string operationName = null, CancellationToken cancellationToken = default)
        {
            return SendJsonAsync<TRequest, TResponse>(CreateRequest(path, NetworkMethod.Post, operationName), payload, cancellationToken);
        }

        /// <summary>
        /// 异步发送请求并解析响应。
        /// </summary>
        /// <typeparam name="TResponse">响应类型。</typeparam>
        /// <param name="request">网络服务请求对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>包含解析后的响应结果。</returns>
        protected UniTask<NetworkServiceResult<TResponse>> SendAsync<TResponse>(NetworkServiceRequest request, CancellationToken cancellationToken = default)
        {
            return ExecuteAsync<TResponse>(request, null, cancellationToken);
        }

        /// <summary>
        /// 异步发送 JSON 请求并解析响应。
        /// </summary>
        /// <typeparam name="TRequest">请求类型。</typeparam>
        /// <typeparam name="TResponse">响应类型。</typeparam>
        /// <param name="request">网络服务请求对象。</param>
        /// <param name="payload">请求对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>包含解析后的响应结果。</returns>
        protected UniTask<NetworkServiceResult<TResponse>> SendJsonAsync<TRequest, TResponse>(NetworkServiceRequest request, TRequest payload, CancellationToken cancellationToken = default)
        {
            return ExecuteAsync<TResponse>(request, SerializePayload(payload), cancellationToken);
        }

        /// <summary>
        /// 序列化请求负载为 JSON 字符串。
        /// </summary>
        /// <typeparam name="TRequest">请求类型。</typeparam>
        /// <param name="payload">要序列化的对象。</param>
        /// <returns>JSON 字符串，如果 payload 为 null 则返回 null。</returns>
        protected virtual string SerializePayload<TRequest>(TRequest payload)
        {
            if (payload == null)
            {
                return null;
            }

            if (payload is string text)
            {
                return text;
            }

            return JsonUtility.ToJson(payload);
        }

        /// <summary>
        /// 反序列化网络响应为指定类型。
        /// </summary>
        /// <typeparam name="TResponse">响应类型。</typeparam>
        /// <param name="response">网络响应对象。</param>
        /// <returns>反序列化后的对象，如果响应为 null 或文本为空则返回默认值。</returns>
        protected virtual TResponse DeserializeResponse<TResponse>(NetworkResponse response)
        {
            if (response == null)
            {
                return default;
            }

            if (typeof(TResponse) == typeof(NetworkResponse))
            {
                return (TResponse)(object)response;
            }

            if (typeof(TResponse) == typeof(string))
            {
                return (TResponse)(object)(response.Text ?? string.Empty);
            }

            if (typeof(TResponse) == typeof(byte[]))
            {
                return (TResponse)(object)(response.Data ?? Array.Empty<byte>());
            }

            if (string.IsNullOrWhiteSpace(response.Text))
            {
                return default;
            }

            return JsonUtility.FromJson<TResponse>(response.Text);
        }

        private async UniTask<NetworkServiceResult<TResponse>> ExecuteAsync<TResponse>(NetworkServiceRequest request, string body, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var serviceName = GetType().Name;
            Module?.CaptureServiceCall(serviceName, request.OperationName, request.Path);

            var networkRequest = BuildNetworkRequest(request, body);
            try
            {
                var response = await Http.SendAsync(networkRequest, cancellationToken);
                if (!response.IsSuccess)
                {
                    return CreateFailureResult<TResponse>(serviceName, request, networkRequest, response, response.ErrorMessage, response.Error);
                }

                try
                {
                    var value = DeserializeResponse<TResponse>(response);
                    return CreateSuccessResult(serviceName, request, networkRequest, response, value);
                }
                catch (Exception exception)
                {
                    var error = FrameworkError.FromException("NetworkServiceDeserializeFailed", exception, FrameworkFailureCategory.Network, false, request.Path, FrameworkOperationStage.Failed);
                    return CreateFailureResult<TResponse>(serviceName, request, networkRequest, response, exception.Message, error);
                }
            }
            catch (FrameworkException exception)
            {
                return CreateFailureResult<TResponse>(serviceName, request, networkRequest, null, exception.Message, exception.Error);
            }
            catch (Exception exception)
            {
                var error = FrameworkError.FromException("NetworkServiceRequestFailed", exception, FrameworkFailureCategory.Network, true, request.Path, FrameworkOperationStage.Failed);
                return CreateFailureResult<TResponse>(serviceName, request, networkRequest, null, exception.Message, error);
            }
        }

        private static NetworkRequest BuildNetworkRequest(NetworkServiceRequest request, string body)
        {
            var networkRequest = new NetworkRequest
            {
                Url = request.Path,
                Method = request.Method,
                Body = body,
                ContentType = request.ContentType,
                TimeoutSeconds = request.TimeoutSeconds,
                UseBaseUrl = request.UseBaseUrl,
                TraceId = request.TraceId,
                Policy = request.Policy
            };

            foreach (var pair in request.Headers)
            {
                networkRequest.SetHeader(pair.Key, pair.Value);
            }

            return networkRequest;
        }

        private static NetworkServiceResult<TResponse> CreateSuccessResult<TResponse>(string serviceName, NetworkServiceRequest request, NetworkRequest networkRequest, NetworkResponse response, TResponse value)
        {
            return new NetworkServiceResult<TResponse>
            {
                Success = true,
                ServiceName = serviceName ?? string.Empty,
                OperationName = request?.OperationName ?? string.Empty,
                Url = response?.Url ?? request?.Path ?? string.Empty,
                StatusCode = response?.StatusCode ?? 0L,
                TraceId = response?.TraceId ?? networkRequest?.TraceId ?? request?.TraceId ?? string.Empty,
                Stage = response?.Stage ?? FrameworkOperationStage.Completed,
                Response = response,
                Value = value
            };
        }

        private static NetworkServiceResult<TResponse> CreateFailureResult<TResponse>(string serviceName, NetworkServiceRequest request, NetworkRequest networkRequest, NetworkResponse response, string errorMessage, FrameworkError error)
        {
            return new NetworkServiceResult<TResponse>
            {
                Success = false,
                ServiceName = serviceName ?? string.Empty,
                OperationName = request?.OperationName ?? string.Empty,
                Url = response?.Url ?? request?.Path ?? string.Empty,
                StatusCode = response?.StatusCode ?? 0L,
                TraceId = response?.TraceId ?? networkRequest?.TraceId ?? request?.TraceId ?? string.Empty,
                Stage = response?.Stage ?? error?.Stage ?? FrameworkOperationStage.Failed,
                ErrorMessage = errorMessage ?? error?.Message ?? string.Empty,
                Error = error,
                Response = response
            };
        }
    }
}
