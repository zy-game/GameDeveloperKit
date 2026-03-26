using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 平台模块，提供平台特性检测、权限请求和平台桥接功能。
    /// 支持多渠道配置和平台特定操作。
    /// </summary>
    public sealed class PlatformModule : IGameFrameworkLifecycleModule
    {
        private readonly Dictionary<string, string> _platformValues = new(StringComparer.Ordinal);
        private readonly Dictionary<PlatformCapability, bool> _capabilityCache = new();
        private readonly Dictionary<PlatformPermission, bool> _permissionCache = new();
        private GameFrameworkModuleStatus _status = GameFrameworkModuleStatus.Created;
        private bool _diagnosticsRegistered;
        private string _lastFeatureReportJson = string.Empty;

        /// <summary>
        /// 获取当前运行时平台。
        /// </summary>
        public RuntimePlatform CurrentPlatform => Application.platform;

        /// <summary>
        /// 获取当前系统语言。
        /// </summary>
        public SystemLanguage CurrentLanguage => Application.systemLanguage;

        /// <summary>
        /// 获取网络可达性。
        /// </summary>
        public NetworkReachability NetworkReachability => Application.internetReachability;

        /// <summary>
        /// 获取是否为移动平台。
        /// </summary>
        public bool IsMobilePlatform => Application.isMobilePlatform;

        /// <summary>
        /// 获取是否在编辑器中运行。
        /// </summary>
        public bool IsEditor => Application.isEditor;

        /// <summary>
        /// 获取渠道名称。
        /// </summary>
        public string Channel => TryGetValue("Channel", out var channel) ? channel : string.Empty;

        /// <summary>
        /// 获取或设置平台桥接器。
        /// </summary>
        public IPlatformBridge Bridge { get; private set; }

        /// <summary>
        /// 获取模块状态。
        /// </summary>
        public GameFrameworkModuleStatus Status => _status;

        /// <summary>
        /// 异步初始化平台模块。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>初始化任务。</returns>
        public UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (!GameFrameworkModuleLifecycleUtility.TryEnterInitialization(nameof(PlatformModule), ref _status, cancellationToken))
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
        /// 异步关闭平台模块。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>关闭任务。</returns>
        public UniTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            if (!GameFrameworkModuleLifecycleUtility.TryEnterShutdown(nameof(PlatformModule), ref _status, cancellationToken))
            {
                return UniTask.CompletedTask;
            }

            Dispose();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 设置平台桥接器。
        /// </summary>
        /// <param name="bridge">平台桥接器。</param>
        public void SetBridge(IPlatformBridge bridge)
        {
            Bridge = bridge;
        }

        /// <summary>
        /// 设置平台值。
        /// </summary>
        /// <param name="key">键。</param>
        /// <param name="value">值。</param>
        /// <exception cref="ArgumentException">当键为空时抛出。</exception>
        public void SetValue(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Platform value key can not be empty.", nameof(key));
            }

            _platformValues[key] = value ?? string.Empty;
        }

        /// <summary>
        /// 尝试获取平台值。
        /// </summary>
        /// <param name="key">键。</param>
        /// <param name="value">输出值。</param>
        /// <returns>如果获取成功则返回 true，否则返回 false。</returns>
        public bool TryGetValue(string key, out string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = null;
                return false;
            }

            return _platformValues.TryGetValue(key, out value);
        }

        /// <summary>
        /// 加载渠道配置。
        /// </summary>
        /// <param name="config">渠道配置数据。</param>
        /// <param name="clearExisting">是否清除现有配置。</param>
        /// <exception cref="ArgumentNullException">当配置为 null 时抛出。</exception>
        public void LoadChannelConfig(PlatformChannelConfig config, bool clearExisting = false)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (clearExisting)
            {
                _platformValues.Clear();
            }

            if (config.Entries == null)
            {
                return;
            }

            for (var i = 0; i < config.Entries.Length; i++)
            {
                var entry = config.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                _platformValues[entry.Key] = entry.Value ?? string.Empty;
            }
        }

        /// <summary>
        /// 从 JSON 加载渠道配置。
        /// </summary>
        /// <param name="json">JSON 字符串。</param>
        /// <param name="clearExisting">是否清除现有配置。</param>
        /// <exception cref="ArgumentException">当 JSON 为空时抛出。</exception>
        /// <exception cref="InvalidOperationException">当反序列化失败时抛出。</exception>
        public void LoadChannelConfigJson(string json, bool clearExisting = false)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Json can not be empty.", nameof(json));
            }

            var config = JsonUtility.FromJson<PlatformChannelConfig>(json);
            if (config == null)
            {
                throw new InvalidOperationException("Failed to deserialize platform channel config.");
            }

            LoadChannelConfig(config, clearExisting);
        }

        /// <summary>
        /// 检查是否具备指定平台特性。
        /// </summary>
        /// <param name="capability">平台特性。</param>
        /// <param name="useCache">是否使用缓存。</param>
        /// <returns>如果具备该特性则返回 true，否则返回 false。</returns>
        public bool HasCapability(PlatformCapability capability, bool useCache = true)
        {
            if (useCache && _capabilityCache.TryGetValue(capability, out var cached))
            {
                return cached;
            }

            var value = EvaluateCapability(capability);
            _capabilityCache[capability] = value;
            return value;
        }

        /// <summary>
        /// 获取平台特性矩阵。
        /// </summary>
        /// <param name="refresh">是否刷新缓存。</param>
        /// <returns>特性字典。</returns>
        public IReadOnlyDictionary<PlatformCapability, bool> GetCapabilityMatrix(bool refresh = false)
        {
            if (refresh)
            {
                RefreshCapabilityCache();
            }

            return new Dictionary<PlatformCapability, bool>(_capabilityCache);
        }

        /// <summary>
        /// 刷新特性缓存。
        /// </summary>
        public void RefreshCapabilityCache()
        {
            _capabilityCache.Clear();
            foreach (PlatformCapability capability in Enum.GetValues(typeof(PlatformCapability)))
            {
                _capabilityCache[capability] = EvaluateCapability(capability);
            }
        }

        /// <summary>
        /// 异步请求平台权限。
        /// </summary>
        /// <param name="permission">权限类型。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>如果权限被授予则返回 true，否则返回 false。</returns>
        /// <exception cref="ArgumentOutOfRangeException">当权限类型无效时抛出。</exception>
        public async UniTask<bool> RequestPermissionAsync(PlatformPermission permission, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var authorization = permission switch
            {
                PlatformPermission.Camera => UserAuthorization.WebCam,
                PlatformPermission.Microphone => UserAuthorization.Microphone,
                _ => throw new ArgumentOutOfRangeException(nameof(permission))
            };

            await Application.RequestUserAuthorization(authorization);
            cancellationToken.ThrowIfCancellationRequested();

            var granted = permission switch
            {
                PlatformPermission.Camera => Application.HasUserAuthorization(UserAuthorization.WebCam),
                PlatformPermission.Microphone => Application.HasUserAuthorization(UserAuthorization.Microphone),
                _ => false
            };

            _permissionCache[permission] = granted;
            return granted;
        }

        /// <summary>
        /// 异步执行平台登录。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>操作结果。</returns>
        public UniTask<PlatformOperationResult> LoginAsync(CancellationToken cancellationToken = default)
        {
            return Bridge == null
                ? UniTask.FromResult(CreateBridgeMissingResult("Login"))
                : Bridge.LoginAsync(cancellationToken);
        }

        /// <summary>
        /// 异步执行平台支付。
        /// </summary>
        /// <param name="productId">商品 ID。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>操作结果。</returns>
        public UniTask<PlatformOperationResult> PayAsync(string productId, CancellationToken cancellationToken = default)
        {
            return Bridge == null
                ? UniTask.FromResult(CreateBridgeMissingResult("Pay"))
                : Bridge.PayAsync(productId, cancellationToken);
        }

        /// <summary>
        /// 异步执行平台分享。
        /// </summary>
        /// <param name="contentId">内容 ID。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>操作结果。</returns>
        public UniTask<PlatformOperationResult> ShareAsync(string contentId, CancellationToken cancellationToken = default)
        {
            return Bridge == null
                ? UniTask.FromResult(CreateBridgeMissingResult("Share"))
                : Bridge.ShareAsync(contentId, cancellationToken);
        }

        /// <summary>
        /// 创建平台特性报告。
        /// </summary>
        /// <param name="refreshCapabilities">是否刷新特性缓存。</param>
        /// <returns>特性报告。</returns>
        public PlatformFeatureReport CreateFeatureReport(bool refreshCapabilities = true)
        {
            if (refreshCapabilities)
            {
                RefreshCapabilityCache();
            }

            return new PlatformFeatureReport
            {
                Platform = CurrentPlatform.ToString(),
                Language = CurrentLanguage.ToString(),
                Channel = Channel,
                NetworkReachability = NetworkReachability.ToString(),
                Bridge = Bridge?.GetType().Name ?? string.Empty,
                IsMobilePlatform = IsMobilePlatform,
                IsEditor = IsEditor,
                Capabilities = BuildCapabilityEntries(),
                Permissions = BuildPermissionEntries(),
                CustomValues = BuildCustomValueEntries()
            };
        }

        /// <summary>
        /// 构建平台特性报告 JSON。
        /// </summary>
        /// <param name="refreshCapabilities">是否刷新特性缓存。</param>
        /// <returns>JSON 字符串。</returns>
        public string BuildFeatureReportJson(bool refreshCapabilities = true)
        {
            _lastFeatureReportJson = JsonUtility.ToJson(CreateFeatureReport(refreshCapabilities), true);
            return _lastFeatureReportJson;
        }

        /// <summary>
        /// 释放平台模块资源。
        /// </summary>
        public void Dispose()
        {
            RemoveDiagnosticsSnapshotProviders();
            _platformValues.Clear();
            _capabilityCache.Clear();
            _permissionCache.Clear();
            _lastFeatureReportJson = string.Empty;
            Bridge = null;
            _status = GameFrameworkModuleStatus.Disposed;
        }

        private bool EvaluateCapability(PlatformCapability capability)
        {
            return capability switch
            {
                PlatformCapability.Touch => Input.touchSupported,
                PlatformCapability.Keyboard => !Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Desktop,
                PlatformCapability.Mouse => Input.mousePresent,
                PlatformCapability.Microphone => Microphone.devices != null && Microphone.devices.Length > 0,
                PlatformCapability.Camera => WebCamTexture.devices != null && WebCamTexture.devices.Length > 0,
                PlatformCapability.NetworkReachability => Application.internetReachability != UnityEngine.NetworkReachability.NotReachable,
                PlatformCapability.CursorLock => !Application.isMobilePlatform,
                _ => false
            };
        }

        private void RegisterDiagnosticsSnapshotProviders()
        {
            if (_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RegisterSnapshotProvider("Platform.CurrentPlatform", () => CurrentPlatform.ToString());
            diagnostics.RegisterSnapshotProvider("Platform.Channel", () => Channel);
            diagnostics.RegisterSnapshotProvider("Platform.Bridge", () => Bridge?.GetType().Name ?? string.Empty);
            diagnostics.RegisterSnapshotProvider("Platform.CapabilityCacheCount", () => _capabilityCache.Count.ToString());
            diagnostics.RegisterSnapshotProvider("Platform.PermissionCacheCount", () => _permissionCache.Count.ToString());
            diagnostics.RegisterSnapshotProvider("Platform.LastFeatureReportLength", () => _lastFeatureReportJson.Length.ToString());
            _diagnosticsRegistered = true;
        }

        private void RemoveDiagnosticsSnapshotProviders()
        {
            if (!_diagnosticsRegistered || !Game.TryGetModule<DiagnosticsModule>(out var diagnostics))
            {
                return;
            }

            diagnostics.RemoveSnapshotProvider("Platform.CurrentPlatform");
            diagnostics.RemoveSnapshotProvider("Platform.Channel");
            diagnostics.RemoveSnapshotProvider("Platform.Bridge");
            diagnostics.RemoveSnapshotProvider("Platform.CapabilityCacheCount");
            diagnostics.RemoveSnapshotProvider("Platform.PermissionCacheCount");
            diagnostics.RemoveSnapshotProvider("Platform.LastFeatureReportLength");
            _diagnosticsRegistered = false;
        }

        private PlatformFeatureEntry[] BuildCapabilityEntries()
        {
            var entries = new List<PlatformFeatureEntry>(_capabilityCache.Count);
            foreach (var pair in _capabilityCache)
            {
                entries.Add(new PlatformFeatureEntry
                {
                    Key = pair.Key.ToString(),
                    Value = pair.Value.ToString()
                });
            }

            return entries.ToArray();
        }

        private PlatformFeatureEntry[] BuildPermissionEntries()
        {
            var entries = new List<PlatformFeatureEntry>(_permissionCache.Count);
            foreach (var pair in _permissionCache)
            {
                entries.Add(new PlatformFeatureEntry
                {
                    Key = pair.Key.ToString(),
                    Value = pair.Value.ToString()
                });
            }

            return entries.ToArray();
        }

        private PlatformFeatureEntry[] BuildCustomValueEntries()
        {
            var entries = new List<PlatformFeatureEntry>(_platformValues.Count);
            foreach (var pair in _platformValues)
            {
                entries.Add(new PlatformFeatureEntry
                {
                    Key = pair.Key,
                    Value = pair.Value
                });
            }

            return entries.ToArray();
        }

        private static PlatformOperationResult CreateBridgeMissingResult(string operationName)
        {
            return new PlatformOperationResult
            {
                Success = false,
                ErrorCode = "PlatformBridgeMissing",
                ErrorMessage = $"Platform bridge is not configured for '{operationName}'.",
                FailureKind = "PlatformBridgeMissing",
                Stage = FrameworkOperationStage.Failed,
                Error = FrameworkError.Create("PlatformBridgeMissing", $"Platform bridge is not configured for '{operationName}'.", FrameworkFailureCategory.Platform, false, operationName, stage: FrameworkOperationStage.Failed)
            };
        }
    }
}
