using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源包，负责管理资源条目、资源加载、更新和释放。
    /// </summary>
    public sealed partial class ResourcePackage : IResourcePackage
    {
        private readonly IResourceRuntime _runtime;
        private readonly ResourcePackageContext _context;
        private readonly List<ResourceEntry> _entries;
        private readonly Dictionary<string, AssetRecord> _assetRecords = new(StringComparer.Ordinal);
        private readonly Dictionary<string, RawFileRecord> _rawFileRecords = new(StringComparer.Ordinal);
        private ResourcePackageState _state = ResourcePackageState.Uninitialized;
        private string _lastError;
        private bool _isPrepared;

        /// <summary>
        /// 初始化资源包实例。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <param name="options">资源包选项。</param>
        /// <param name="runtime">资源运行时实现。</param>
        /// <param name="context">资源包上下文。</param>
        /// <exception cref="ArgumentException">当资源包名称为空时抛出。</exception>
        /// <exception cref="ArgumentNullException">当 <paramref name="runtime"/> 或 <paramref name="context"/> 为空时抛出。</exception>
        public ResourcePackage(string packageName, ResourcePackageOptions options, IResourceRuntime runtime, ResourcePackageContext context)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentException("Package name can not be empty.", nameof(packageName));
            }
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _context = context ?? throw new ArgumentNullException(nameof(context));

            PackageName = packageName;
            Options = options ?? new ResourcePackageOptions();
            _entries = BuildEntries(Options, _runtime, _context);
            ValidateEntries(_entries);
        }

        /// <summary>
        /// 获取资源包名称。
        /// </summary>
        public string PackageName { get; }

        /// <summary>
        /// 获取资源包选项。
        /// </summary>
        public ResourcePackageOptions Options { get; }

        /// <summary>
        /// 获取资源包当前状态。
        /// </summary>
        public ResourcePackageState State => _state;

        /// <summary>
        /// 获取最后一次错误信息。
        /// </summary>
        public string LastError => _lastError;

        /// <summary>
        /// 获取资源包是否已就绪。
        /// </summary>
        public bool IsReady => _state == ResourcePackageState.Ready;

        /// <summary>
        /// 获取资源包是否已完成准备。
        /// </summary>
        public bool IsPrepared => _isPrepared;

        /// <summary>
        /// 获取资源包中的资源条目列表。
        /// </summary>
        public IReadOnlyList<ResourceEntry> Entries => _entries;

        /// <summary>
        /// 获取最近一次更新报告。
        /// </summary>
        public ResourceUpdateReport LastUpdateReport => _context.LastUpdateReport;

        /// <summary>
        /// 异步准备资源包，使其可供同步资源接口使用。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>准备任务。</returns>
        public UniTask PrepareAsync(CancellationToken cancellationToken = default)
        {
            return EnsureReadyAsync(cancellationToken);
        }

        /// <summary>
        /// 异步初始化资源包。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>初始化任务。</returns>
        /// <exception cref="InvalidOperationException">当资源包已处于初始化中时抛出。</exception>
        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_state == ResourcePackageState.Ready
                || _state == ResourcePackageState.Updated
                || _state == ResourcePackageState.Initialized)
            {
                return;
            }

            if (_state == ResourcePackageState.Initializing)
            {
                throw new InvalidOperationException($"Package '{PackageName}' is already initializing.");
            }

            _state = ResourcePackageState.Initializing;
            _lastError = null;

            try
            {
                await _runtime.InitializePackageAsync(_context, cancellationToken);
                _state = ResourcePackageState.Initialized;
            }
            catch (Exception exception)
            {
                _state = ResourcePackageState.Failed;
                _lastError = exception.Message;
                _isPrepared = false;
                throw;
            }
        }

        /// <summary>
        /// 异步更新资源包内容。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源更新结果。</returns>
        /// <exception cref="InvalidOperationException">当资源包之前已更新失败时抛出。</exception>
        public async UniTask<ResourceUpdateResult> UpdateAsync(CancellationToken cancellationToken = default)
        {
            if (_state == ResourcePackageState.Failed)
            {
                throw new InvalidOperationException($"Package '{PackageName}' update failed previously: {_lastError}");
            }

            if (_state == ResourcePackageState.Uninitialized)
            {
                await InitializeAsync(cancellationToken);
            }

            _state = ResourcePackageState.Updating;
            _lastError = null;
            _isPrepared = false;

            try
            {
                await _runtime.EnsurePackageReadyAsync(_context, cancellationToken);
                _state = ResourcePackageState.Updated;
                return CreateUpdateResult();
            }
            catch (Exception exception)
            {
                _state = ResourcePackageState.Failed;
                _lastError = exception.Message;
                throw;
            }
        }

        /// <summary>
        /// 注册单个资源条目。
        /// </summary>
        /// <param name="entry">资源条目。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="entry"/> 为空时抛出。</exception>
        public void RegisterEntry(ResourceEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            _entries.Add(CloneEntry(entry));
            ValidateEntries(_entries);
        }

        /// <summary>
        /// 批量注册资源条目。
        /// </summary>
        /// <param name="entries">资源条目集合。</param>
        public void RegisterEntries(IEnumerable<ResourceEntry> entries)
        {
            if (entries == null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                RegisterEntry(entry);
            }
        }

        /// <summary>
        /// 移除匹配指定位置和类型的资源条目。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="kind">资源条目类型。</param>
        /// <returns>实际移除的条目数量。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="location"/> 为空时抛出。</exception>
        public int RemoveEntries(ResourceLocation location, ResourceEntryKind? kind = null)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            var removedCount = 0;
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (!location.Matches(_entries[i], kind))
                {
                    continue;
                }

                _entries.RemoveAt(i);
                removedCount++;
            }

            if (removedCount > 0)
            {
                ValidateEntries(_entries);
            }

            return removedCount;
        }

        /// <summary>
        /// 清空指定类型的资源条目。
        /// </summary>
        /// <param name="kind">资源条目类型；为空时清空全部条目。</param>
        public void ClearEntries(ResourceEntryKind? kind = null)
        {
            if (!kind.HasValue)
            {
                _entries.Clear();
                return;
            }

            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].Kind == kind.Value)
                {
                    _entries.RemoveAt(i);
                }
            }

            ValidateEntries(_entries);
        }

        /// <summary>
        /// 同步加载单个资源。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <returns>资源句柄。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="location"/> 为空时抛出。</exception>
        /// <exception cref="InvalidOperationException">当资源包未准备完成或资源不存在时抛出。</exception>
        public AssetHandle LoadAsset(ResourceLocation location)
        {
            EnsureReady();
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            return CreateAssetHandle(ResolveAssetEntry(location, requireSingle: true));
        }

        /// <summary>
        /// 异步加载单个资源。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源句柄。</returns>
        public UniTask<AssetHandle> LoadAssetAsync(ResourceLocation location, CancellationToken cancellationToken = default)
        {
            return LoadAssetAsyncInternal(location, cancellationToken);
        }

        /// <summary>
        /// 同步加载匹配位置的资源列表。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <returns>资源句柄列表。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="location"/> 为空时抛出。</exception>
        /// <exception cref="InvalidOperationException">当资源包未准备完成时抛出。</exception>
        public IReadOnlyList<AssetHandle> LoadAssets(ResourceLocation location)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }
            EnsureReady();

            var matches = Find(location, ResourceEntryKind.Asset);
            if (matches.Count == 0)
            {
                return Array.Empty<AssetHandle>();
            }

            var handles = new AssetHandle[matches.Count];
            for (var i = 0; i < matches.Count; i++)
            {
                handles[i] = CreateAssetHandle(matches[i]);
            }

            return handles;
        }

        /// <summary>
        /// 异步加载匹配位置的资源列表。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源句柄列表。</returns>
        public UniTask<IReadOnlyList<AssetHandle>> LoadAssetsAsync(ResourceLocation location, CancellationToken cancellationToken = default)
        {
            return LoadAssetsAsyncInternal(location, cancellationToken);
        }

        /// <summary>
        /// 同步加载场景资源。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="loadMode">场景加载模式。</param>
        /// <returns>场景句柄。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="location"/> 为空时抛出。</exception>
        /// <exception cref="InvalidOperationException">当资源包未准备完成或场景不存在时抛出。</exception>
        public SceneHandle LoadScene(ResourceLocation location, LoadSceneMode loadMode = LoadSceneMode.Single)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }
            EnsureReady();

            var scenePath = ResolveScenePath(location);
            SceneManager.LoadScene(scenePath, loadMode);
            return new SceneHandle(PackageName, location, SceneManager.GetSceneByPath(scenePath), scenePath);
        }

        /// <summary>
        /// 异步加载场景资源。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="loadMode">场景加载模式。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>场景句柄。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="location"/> 为空时抛出。</exception>
        /// <exception cref="InvalidOperationException">当场景加载失败时抛出。</exception>
        public async UniTask<SceneHandle> LoadSceneAsync(ResourceLocation location, LoadSceneMode loadMode = LoadSceneMode.Single, CancellationToken cancellationToken = default)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            cancellationToken.ThrowIfCancellationRequested();
            await EnsureReadyAsync(cancellationToken);

            var scenePath = ResolveScenePath(location);
            var operation = SceneManager.LoadSceneAsync(scenePath, loadMode);
            if (operation == null)
            {
                throw new InvalidOperationException($"Failed to load scene '{scenePath}'.");
            }

            await operation;
            return new SceneHandle(PackageName, location, SceneManager.GetSceneByPath(scenePath), scenePath);
        }

        /// <summary>
        /// 同步加载原始文件。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <returns>原始文件句柄。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="location"/> 为空时抛出。</exception>
        /// <exception cref="InvalidOperationException">当资源包未准备完成或原始文件路径无效时抛出。</exception>
        public RawFileHandle LoadRawFile(ResourceLocation location)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }
            EnsureReady();

            var fullPath = ResolveRawFilePath(location);
            var recordKey = BuildRawFileKey(fullPath);
            if (!_rawFileRecords.TryGetValue(recordKey, out var record))
            {
                var data = File.ReadAllBytes(fullPath);
                record = new RawFileRecord(this, location.Clone(), fullPath, data);
                _rawFileRecords.Add(recordKey, record);
            }

            record.Retain();
            return new RawFileHandle(record);
        }

        /// <summary>
        /// 异步加载原始文件。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>原始文件句柄。</returns>
        public UniTask<RawFileHandle> LoadRawFileAsync(ResourceLocation location, CancellationToken cancellationToken = default)
        {
            return LoadRawFileAsyncInternal(location, cancellationToken);
        }

        /// <summary>
        /// 查找匹配指定位置和类型的资源条目。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="kind">资源条目类型。</param>
        /// <returns>匹配到的资源条目列表。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="location"/> 为空时抛出。</exception>
        public IReadOnlyList<ResourceEntry> Find(ResourceLocation location, ResourceEntryKind? kind = null)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            var results = new List<ResourceEntry>();
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (location.Matches(entry, kind))
                {
                    results.Add(entry);
                }
            }

            return results;
        }

        /// <summary>
        /// 回收当前未使用的资源和原始文件记录。
        /// </summary>
        /// <param name="force">是否强制回收并忽略延迟释放时间。</param>
        public void CollectUnused(bool force = false)
        {
            var now = Time.realtimeSinceStartup;

            foreach (var pair in _assetRecords.ToArray())
            {
                if (pair.Value.CanUnload(now, force))
                {
                    pair.Value.Unload();
                    _assetRecords.Remove(pair.Key);
                }
            }

            foreach (var pair in _rawFileRecords.ToArray())
            {
                if (pair.Value.CanUnload(now, force))
                {
                    pair.Value.Unload();
                    _rawFileRecords.Remove(pair.Key);
                }
            }
        }

        private AssetHandle CreateAssetHandle(ResourceLocation location)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            return CreateAssetHandle(ResolveAssetEntry(location, requireSingle: false));
        }

        private async UniTask<AssetHandle> LoadAssetAsyncInternal(ResourceLocation location, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureReadyAsync(cancellationToken);
            return CreateAssetHandle(location);
        }

        private async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsAsyncInternal(ResourceLocation location, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureReadyAsync(cancellationToken);
            return LoadAssets(location);
        }

        private async UniTask<RawFileHandle> LoadRawFileAsyncInternal(ResourceLocation location, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureReadyAsync(cancellationToken);
            return LoadRawFile(location);
        }

        private AssetHandle CreateAssetHandle(ResourceEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            var recordKey = BuildAssetKey(entry);
            if (!_assetRecords.TryGetValue(recordKey, out var record))
            {
                var asset = ResolveAsset(entry);
                if (asset == null)
                {
                    throw new InvalidOperationException($"Failed to resolve asset in package '{PackageName}'.");
                }

                var dependencies = ResolveDependencies(entry);
                record = new AssetRecord(this, CreateLocation(entry), asset, recordKey, dependencies);
                _assetRecords.Add(recordKey, record);
            }
            else
            {
                record.Retain();
            }

            return new AssetHandle(record);
        }

        private UnityEngine.Object ResolveAsset(ResourceEntry entry)
        {
            return _runtime.LoadAsset(_context, entry);
        }

        private string ResolveScenePath(ResourceLocation location)
        {
            var entry = ResolveSceneEntry(location);
            return _runtime.ResolveScenePath(_context, entry);
        }

        private string ResolveRawFilePath(ResourceLocation location)
        {
            var entry = ResolveRawFileEntry(location);
            var path = ResolveFullPath(entry);

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("Raw file location requires FullPath or Name.");
            }

            return path;
        }

        private static string ResolvePath(ResourceEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.FullPath))
            {
                return entry.FullPath;
            }

            return entry.Name;
        }

        private string ResolveFullPath(ResourceEntry entry)
        {
            return _runtime.ResolveFilePath(_context, entry);
        }

        private static string NormalizeResourcePath(ResourceEntry entry)
        {
            var path = entry.Name;
            if (string.IsNullOrWhiteSpace(path))
            {
                path = entry.FullPath;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            path = path.Replace('\\', '/');
            if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                path = Path.ChangeExtension(path, null);
            }

            var resourcesIndex = path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
            if (resourcesIndex >= 0)
            {
                path = path.Substring(resourcesIndex + "/Resources/".Length);
            }

            return path.TrimStart('/');
        }

        private static string BuildAssetKey(ResourceEntry entry)
        {
            var labels = entry.Labels == null ? string.Empty : string.Join("|", entry.Labels);
            return $"{entry.Name}#{entry.FullPath}#{entry.AssetType?.FullName}#{labels}#{entry.Kind}";
        }

        private static string BuildRawFileKey(string fullPath)
        {
            return fullPath ?? string.Empty;
        }

        private ResourceEntry ResolveAssetEntry(ResourceLocation location, bool requireSingle)
        {
            if (IsResourcesLocation(location))
            {
                return CreateResourcesEntry(location);
            }

            var matches = Find(location, null);
            if (matches.Count == 0)
            {
                throw new InvalidOperationException($"Failed to find asset in package '{PackageName}'.");
            }

            if (requireSingle && matches.Count > 1)
            {
                throw new InvalidOperationException($"LoadAsset matched multiple resources in package '{PackageName}'. Use LoadByName/LoadByType/LoadByLabel/LoadByPath for strict query.");
            }

            return matches[0];
        }

        private ResourceEntry ResolveSceneEntry(ResourceLocation location)
        {
            var matches = Find(location, null);
            if (matches.Count == 0)
            {
                throw new InvalidOperationException($"Failed to find scene in package '{PackageName}'.");
            }

            for (var i = 0; i < matches.Count; i++)
            {
                var entry = matches[i];
                if (!string.IsNullOrWhiteSpace(entry?.FullPath) &&
                    entry.FullPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            throw new InvalidOperationException($"Failed to find scene entry in package '{PackageName}'.");
        }

        private ResourceEntry ResolveRawFileEntry(ResourceLocation location)
        {
            var matches = Find(location, null);
            if (matches.Count == 0)
            {
                return new ResourceEntry
                {
                    Name = location.Name,
                    AssetType = location.AssetType,
                    Labels = location.Labels,
                    FullPath = location.FullPath,
                    Kind = ResourceEntryKind.RawFile
                };
            }

            return matches[0];
        }

        private static bool IsResourcesLocation(ResourceLocation location)
        {
            if (location == null)
            {
                return false;
            }

            return IsResourcesPath(location.Name) || IsResourcesPath(location.FullPath);
        }

        private static bool IsResourcesPath(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Replace('\\', '/').StartsWith("Resources/", StringComparison.OrdinalIgnoreCase);
        }

        private static ResourceEntry CreateResourcesEntry(ResourceLocation location)
        {
            var path = !string.IsNullOrWhiteSpace(location?.Name) ? location.Name : location?.FullPath;
            return new ResourceEntry
            {
                Name = path,
                FullPath = path,
                AssetType = location?.AssetType,
                Labels = location?.Labels == null ? null : new List<string>(location.Labels)
            };
        }

        private ResourceLocation CreateLocation(ResourceEntry entry)
        {
            return new ResourceLocation
            {
                PackageName = PackageName,
                Name = entry.Name,
                AssetType = entry.AssetType,
                Labels = entry.Labels == null ? null : new List<string>(entry.Labels),
                FullPath = entry.FullPath
            };
        }

        private async UniTask EnsureReadyAsync(CancellationToken cancellationToken)
        {
            if (_state == ResourcePackageState.Failed)
            {
                throw new InvalidOperationException($"Package '{PackageName}' initialization failed: {_lastError}");
            }

            if (_state == ResourcePackageState.Uninitialized)
            {
                await InitializeAsync(cancellationToken);
            }

            try
            {
                if (_state == ResourcePackageState.Initialized || _state == ResourcePackageState.Updated)
                {
                    _state = ResourcePackageState.Preparing;
                }

                await _runtime.EnsurePackageReadyAsync(_context, cancellationToken);
                RefreshEntries();
                _state = ResourcePackageState.Ready;
                _isPrepared = true;
            }
            catch (Exception exception)
            {
                _state = ResourcePackageState.Failed;
                _lastError = exception.Message;
                _isPrepared = false;
                throw;
            }
        }

        private ResourceUpdateResult CreateUpdateResult()
        {
            return new ResourceUpdateResult
            {
                PackageName = PackageName,
                IsUpdated = LastUpdateReport?.IsUpdated ?? false,
                Stage = LastUpdateReport?.Stage ?? "Completed",
                State = LastUpdateReport?.State ?? ResourceUpdateState.Completed,
                IsRolledBack = (LastUpdateReport?.RolledBackFileCount ?? 0) > 0,
                DownloadedFileCount = LastUpdateReport?.DownloadedFileCount ?? 0,
                RemovedFileCount = LastUpdateReport?.RemovedFileCount ?? 0,
                DownloadedBytes = LastUpdateReport?.DownloadedBytes ?? 0,
                RolledBackBytes = LastUpdateReport?.RolledBackBytes ?? 0,
                ErrorMessage = LastUpdateReport?.ErrorMessage,
                Error = LastUpdateReport?.Error,
                RecoveryMessage = LastUpdateReport?.RecoveryMessage,
                FailureKind = LastUpdateReport?.FailureKind
            };
        }

        private void EnsureReady()
        {
            if (_state == ResourcePackageState.Failed)
            {
                throw new InvalidOperationException($"Package '{PackageName}' initialization failed: {_lastError}");
            }

            if (_state != ResourcePackageState.Ready)
            {
                throw new InvalidOperationException($"Package '{PackageName}' is not initialized for synchronous access. Prepare it asynchronously from Startup first.");
            }

            if (!_isPrepared)
            {
                throw new InvalidOperationException($"Package '{PackageName}' is not prepared for synchronous access. Call PrepareAsync before using sync resource APIs.");
            }
        }

        private static List<ResourceEntry> BuildEntries(ResourcePackageOptions options, IResourceRuntime runtime, ResourcePackageContext context)
        {
            var entries = new List<ResourceEntry>();
            var runtimeEntries = runtime.BuildEntries(context);
            if (runtimeEntries != null)
            {
                for (var i = 0; i < runtimeEntries.Count; i++)
                {
                    var entry = runtimeEntries[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    entries.Add(CloneEntry(entry));
                }
            }

            if (options?.Entries != null)
            {
                for (var i = 0; i < options.Entries.Count; i++)
                {
                    var entry = options.Entries[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    entries.Add(CloneEntry(entry));
                }
            }

            return entries;
        }

        private void ValidateEntries(IReadOnlyList<ResourceEntry> entries)
        {
            var nameLookup = new Dictionary<string, ResourceEntry>(StringComparer.Ordinal);
            var pathLookup = new Dictionary<string, ResourceEntry>(StringComparer.Ordinal);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.Name) && string.IsNullOrWhiteSpace(entry.FullPath))
                {
                    throw new InvalidOperationException($"Resource entry at index {i} in package '{PackageName}' must have at least Name or FullPath.");
                }

                if (!string.IsNullOrWhiteSpace(entry.Name))
                {
                    if (!nameLookup.TryAdd(entry.Name, entry))
                    {
                        throw new InvalidOperationException($"Resource package '{PackageName}' contains duplicate entry name '{entry.Name}'.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(entry.FullPath))
                {
                    if (!pathLookup.TryAdd(entry.FullPath, entry))
                    {
                        throw new InvalidOperationException($"Resource package '{PackageName}' contains duplicate entry path '{entry.FullPath}'.");
                    }
                }
            }
        }

        private static ResourceEntry CloneEntry(ResourceEntry entry)
        {
            return new ResourceEntry
            {
                Name = entry.Name,
                Version = entry.Version,
                Hash = entry.Hash,
                SizeBytes = entry.SizeBytes,
                AssetType = entry.AssetType,
                Labels = entry.Labels == null ? null : new List<string>(entry.Labels),
                Dependencies = entry.Dependencies == null ? null : new List<string>(entry.Dependencies),
                FullPath = entry.FullPath,
                BundleName = entry.BundleName
            };
        }

        private void RefreshEntries()
        {
            var latest = BuildEntries(Options, _runtime, _context);
            _entries.Clear();
            if (latest != null)
            {
                for (var i = 0; i < latest.Count; i++)
                {
                    var entry = latest[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    _entries.Add(CloneEntry(entry));
                }
            }

            ValidateEntries(_entries);
        }

        private AssetRecord[] ResolveDependencies(ResourceEntry entry)
        {
            return Array.Empty<AssetRecord>();
        }
    }
}

