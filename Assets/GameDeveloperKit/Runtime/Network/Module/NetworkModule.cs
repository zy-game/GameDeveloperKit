using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 网络模块，提供网络请求和服务的统一管理。
    /// </summary>
    public sealed class NetworkModule : IGameFrameworkLifecycleModule
    {
        private readonly Dictionary<Type, INetworkService> _services = new();
        private readonly Dictionary<Type, Type> _serviceContracts = new();
        private GameFrameworkModuleStatus _status = GameFrameworkModuleStatus.Created;
        private bool _diagnosticsRegistered;
        private int _requestCount;
        private int _successCount;
        private int _failureCount;
        private long _totalDurationMilliseconds;
        private string _lastRegisteredServiceName;
        private string _lastServiceCallName;
        private string _lastServiceCallPath;

        /// <summary>
        /// 初始化网络模块的新实例。
        /// </summary>
        public NetworkModule()
        {
            RegisterService(new HttpService());
            Http.RequestCompleted += HandleRequestCompletedInternal;
            Http.RequestFailed += HandleRequestFailedInternal;
        }

        /// <summary>
        /// 获取HTTP服务。
        /// </summary>
        public HttpService Http => GetService<HttpService>();

        /// <summary>
        /// 获取基础URL。
        /// </summary>
        public string BaseUrl => Http.BaseUrl;

        /// <summary>
        /// 获取默认超时时间（秒）。
        /// </summary>
        public int DefaultTimeoutSeconds => Http.DefaultTimeoutSeconds;

        /// <summary>
        /// 获取默认请求头。
        /// </summary>
        public IReadOnlyDictionary<string, string> DefaultHeaders => Http.DefaultHeaders;

        /// <summary>
        /// 获取默认网络策略。
        /// </summary>
        public NetworkPolicy DefaultPolicy => Http.DefaultPolicy;

        /// <summary>
        /// 获取模块状态。
        /// </summary>
        public GameFrameworkModuleStatus Status => _status;

        /// <summary>
        /// 网络请求开始事件。
        /// </summary>
        public event Action<NetworkRequest> RequestStarted
        {
            add => Http.RequestStarted += value;
            remove => Http.RequestStarted -= value;
        }

        /// <summary>
        /// 网络请求完成事件。
        /// </summary>
        public event Action<NetworkRequest, NetworkResponse> RequestCompleted
        {
            add => Http.RequestCompleted += value;
            remove => Http.RequestCompleted -= value;
        }

        /// <summary>
        /// 网络请求失败事件。
        /// </summary>
        public event Action<NetworkRequest, Exception> RequestFailed
        {
            add => Http.RequestFailed += value;
            remove => Http.RequestFailed -= value;
        }

        /// <summary>
        /// 异步初始化网络模块。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (!GameFrameworkModuleLifecycleUtility.TryEnterInitialization(nameof(NetworkModule), ref _status, cancellationToken))
            {
                return UniTask.CompletedTask;
            }

            try
            {
                RegisterDiagnosticsSnapshotProviders();
                GameFrameworkModuleLifecycleUtility.CompleteInitialization(ref _status);
                return UniTask.CompletedTask;
            }
            catch
            {
                GameFrameworkModuleLifecycleUtility.FailInitialization(ref _status);
                throw;
            }
        }

        /// <summary>
        /// 异步关闭网络模块。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public UniTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            if (!GameFrameworkModuleLifecycleUtility.TryEnterShutdown(nameof(NetworkModule), ref _status, cancellationToken))
            {
                return UniTask.CompletedTask;
            }

            Dispose();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 配置网络模块。
        /// </summary>
        /// <param name="baseUrl">基础URL。</param>
        /// <param name="defaultTimeoutSeconds">默认超时时间（秒）。</param>
        public void Configure(string baseUrl = null, int defaultTimeoutSeconds = 30)
        {
            Http.Configure(baseUrl, defaultTimeoutSeconds);
        }

        /// <summary>
        /// 配置网络策略。
        /// </summary>
        /// <param name="policy">网络策略。</param>
        public void ConfigurePolicy(NetworkPolicy policy)
        {
            Http.ConfigurePolicy(policy);
        }

        /// <summary>
        /// 设置默认请求头。
        /// </summary>
        /// <param name="key">请求头键。</param>
        /// <param name="value">请求头值。</param>
        public void SetDefaultHeader(string key, string value)
        {
            Http.SetDefaultHeader(key, value);
        }

        /// <summary>
        /// 移除默认请求头。
        /// </summary>
        /// <param name="key">请求头键。</param>
        /// <returns>如果移除成功返回true，否则返回false。</returns>
        public bool RemoveDefaultHeader(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return Http.RemoveDefaultHeader(key);
        }

        /// <summary>
        /// 清除所有默认请求头。
        /// </summary>
        public void ClearDefaultHeaders()
        {
            Http.ClearDefaultHeaders();
        }

        /// <summary>
        /// 注册网络服务。
        /// </summary>
        /// <typeparam name="TService">服务类型。</typeparam>
        /// <param name="service">服务实例。</param>
        public void RegisterService<TService>(TService service)
            where TService : class, INetworkService
        {
            RegisterService(typeof(TService), service);
        }

        /// <summary>
        /// 注册网络服务及其契约。
        /// </summary>
        /// <typeparam name="TContract">契约类型。</typeparam>
        /// <typeparam name="TService">服务类型。</typeparam>
        /// <param name="service">服务实例。</param>
        public void RegisterService<TContract, TService>(TService service)
            where TContract : class
            where TService : class, TContract, INetworkService
        {
            RegisterService(typeof(TContract), service);
        }

        /// <summary>
        /// 检查是否存在指定的网络服务。
        /// </summary>
        /// <typeparam name="TService">服务类型。</typeparam>
        /// <returns>如果存在返回true，否则返回false。</returns>
        public bool HasService<TService>()
            where TService : class
        {
            return _services.ContainsKey(typeof(TService));
        }

        /// <summary>
        /// 获取指定的网络服务。
        /// </summary>
        /// <typeparam name="TService">服务类型。</typeparam>
        /// <returns>服务实例。</returns>
        /// <exception cref="InvalidOperationException">当服务未注册时抛出。</exception>
        public TService GetService<TService>()
            where TService : class
        {
            if (!TryGetService<TService>(out var service))
            {
                throw new InvalidOperationException($"Network service '{typeof(TService).FullName}' is not registered.");
            }

            return service;
        }

        /// <summary>
        /// 尝试获取指定的网络服务。
        /// </summary>
        /// <typeparam name="TService">服务类型。</typeparam>
        /// <param name="service">输出的服务实例。</param>
        /// <returns>如果获取成功返回true，否则返回false。</returns>
        public bool TryGetService<TService>(out TService service)
            where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out var instance) && instance is TService typedService)
            {
                service = typedService;
                return true;
            }

            service = null;
            return false;
        }

        /// <summary>
        /// 移除指定的网络服务。
        /// </summary>
        /// <typeparam name="TService">服务类型。</typeparam>
        /// <returns>如果移除成功返回true，否则返回false。</returns>
        public bool RemoveService<TService>()
            where TService : class
        {
            var serviceType = typeof(TService);
            if (!_services.Remove(serviceType, out var service))
            {
                return false;
            }

            _serviceContracts.Remove(serviceType);
            service.Dispose();
            return true;
        }

        /// <summary>
        /// 异步发送GET请求。
        /// </summary>
        /// <param name="url">请求URL。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>网络响应的异步任务。</returns>
        public UniTask<NetworkResponse> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            return Http.GetAsync(url, cancellationToken);
        }

        /// <summary>
        /// 异步发送HEAD请求。
        /// </summary>
        /// <param name="url">请求URL。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>网络响应的异步任务。</returns>
        public UniTask<NetworkResponse> HeadAsync(string url, CancellationToken cancellationToken = default)
        {
            return Http.HeadAsync(url, cancellationToken);
        }

        /// <summary>
        /// 异步发送POST JSON请求。
        /// </summary>
        /// <param name="url">请求URL。</param>
        /// <param name="json">JSON数据。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>网络响应的异步任务。</returns>
        public UniTask<NetworkResponse> PostJsonAsync(string url, string json, CancellationToken cancellationToken = default)
        {
            return Http.PostJsonAsync(url, json, cancellationToken);
        }

        /// <summary>
        /// 异步发送网络请求。
        /// </summary>
        /// <param name="request">网络请求。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>网络响应的异步任务。</returns>
        public UniTask<NetworkResponse> SendAsync(NetworkRequest request, CancellationToken cancellationToken = default)
        {
            return Http.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// 释放网络模块占用的所有资源。
        /// </summary>
        public void Dispose()
        {
            Http.RequestCompleted -= HandleRequestCompletedInternal;
            Http.RequestFailed -= HandleRequestFailedInternal;
            RemoveDiagnosticsSnapshotProviders();

            foreach (var service in _services.Values)
            {
                service.Dispose();
            }

            _services.Clear();
            _status = GameFrameworkModuleStatus.Disposed;
        }

        private void HandleRequestCompletedInternal(NetworkRequest request, NetworkResponse response)
        {
            _requestCount++;
            _totalDurationMilliseconds += Math.Max(0L, response?.DurationMilliseconds ?? 0L);
            if (response?.IsSuccess == true)
            {
                _successCount++;
            }
            else
            {
                _failureCount++;
            }

            if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                diagnostics.CaptureSnapshot("Network.LastUrl", response?.Url ?? request?.Url ?? string.Empty);
                diagnostics.CaptureSnapshot("Network.LastStatusCode", (response?.StatusCode ?? 0L).ToString());
                diagnostics.CaptureSnapshot("Network.LastStage", (response?.Stage ?? FrameworkOperationStage.None).ToString());
                diagnostics.CaptureSnapshot("Network.LastTraceId", response?.TraceId ?? request?.TraceId ?? string.Empty);
            }
        }

        private void HandleRequestFailedInternal(NetworkRequest request, Exception exception)
        {
            _requestCount++;
            _failureCount++;

            if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                diagnostics.CaptureSnapshot("Network.LastUrl", request?.Url ?? string.Empty);
                diagnostics.CaptureSnapshot("Network.LastError", exception?.Message ?? string.Empty);
                diagnostics.CaptureSnapshot("Network.LastTraceId", request?.TraceId ?? string.Empty);
            }
        }

        private void RegisterDiagnosticsSnapshotProviders()
        {
            if (_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RegisterSnapshotProvider("Network.RequestCount", () => _requestCount.ToString());
            diagnostics.RegisterSnapshotProvider("Network.SuccessCount", () => _successCount.ToString());
            diagnostics.RegisterSnapshotProvider("Network.FailureCount", () => _failureCount.ToString());
            diagnostics.RegisterSnapshotProvider("Network.AverageDurationMs", () => _requestCount == 0 ? "0" : (_totalDurationMilliseconds / _requestCount).ToString());
            diagnostics.RegisterSnapshotProvider("Network.DefaultRetryCount", () => DefaultPolicy?.RetryCount.ToString() ?? "0");
            diagnostics.RegisterSnapshotProvider("Network.ServiceCount", () => _services.Count.ToString());
            diagnostics.RegisterSnapshotProvider("Network.LastRegisteredService", () => _lastRegisteredServiceName ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Network.LastServiceCall", () => _lastServiceCallName ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Network.LastServiceCallPath", () => _lastServiceCallPath ?? string.Empty);
            _diagnosticsRegistered = true;
        }

        private void RemoveDiagnosticsSnapshotProviders()
        {
            if (!_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RemoveSnapshotProvider("Network.RequestCount");
            diagnostics.RemoveSnapshotProvider("Network.SuccessCount");
            diagnostics.RemoveSnapshotProvider("Network.FailureCount");
            diagnostics.RemoveSnapshotProvider("Network.AverageDurationMs");
            diagnostics.RemoveSnapshotProvider("Network.DefaultRetryCount");
            diagnostics.RemoveSnapshotProvider("Network.ServiceCount");
            diagnostics.RemoveSnapshotProvider("Network.LastRegisteredService");
            diagnostics.RemoveSnapshotProvider("Network.LastServiceCall");
            diagnostics.RemoveSnapshotProvider("Network.LastServiceCallPath");
            _diagnosticsRegistered = false;
        }

        private void RegisterService(Type contractType, INetworkService service)
        {
            if (contractType == null)
            {
                throw new ArgumentNullException(nameof(contractType));
            }

            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (!contractType.IsAssignableFrom(service.GetType()))
            {
                throw new ArgumentException($"Network service '{service.GetType().FullName}' does not implement contract '{contractType.FullName}'.", nameof(service));
            }

            if (_services.TryGetValue(contractType, out var existingService))
            {
                existingService.Dispose();
            }

            service.Initialize(this);
            _services[contractType] = service;
            _serviceContracts[contractType] = service.GetType();
            _lastRegisteredServiceName = $"{contractType.Name}->{service.GetType().Name}";
        }

        /// <summary>
        /// 记录服务调用信息到诊断快照
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <param name="operationName">操作名称</param>
        /// <param name="path">调用路径</param>
        internal void CaptureServiceCall(string serviceName, string operationName, string path)
        {
            _lastServiceCallName = string.IsNullOrWhiteSpace(operationName) ? serviceName ?? string.Empty : $"{serviceName}.{operationName}";
            _lastServiceCallPath = path ?? string.Empty;

            if (Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                diagnostics.CaptureSnapshot("Network.LastServiceCall", _lastServiceCallName);
                diagnostics.CaptureSnapshot("Network.LastServiceCallPath", _lastServiceCallPath);
            }
        }
    }
}
