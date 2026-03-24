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
    public sealed class ResourcePackage : IResourcePackage
    {
        private readonly IResourceRuntime _runtime;
        private readonly ResourcePackageContext _context;
        private readonly List<ResourceEntry> _entries;
        private readonly Dictionary<string, AssetRecord> _assetRecords = new(StringComparer.Ordinal);
        private readonly Dictionary<string, RawFileRecord> _rawFileRecords = new(StringComparer.Ordinal);
        private ResourcePackageState _state = ResourcePackageState.Uninitialized;
        private string _lastError;

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
        }

        public string PackageName { get; }

        public ResourcePackageOptions Options { get; }

        public ResourcePackageState State => _state;

        public string LastError => _lastError;

        public bool IsReady => _state == ResourcePackageState.Ready;

        public IReadOnlyList<ResourceEntry> Entries => _entries;

        public ResourceUpdateReport LastUpdateReport => _context.LastUpdateReport;

        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_state == ResourcePackageState.Ready)
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
                _state = ResourcePackageState.Ready;
            }
            catch (Exception exception)
            {
                _state = ResourcePackageState.Failed;
                _lastError = exception.Message;
                throw;
            }
        }

        public void RegisterEntry(ResourceEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            _entries.Add(CloneEntry(entry));
        }

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

            return removedCount;
        }

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
        }

        public AssetHandle LoadAsset(ResourceLocation location)
        {
            EnsureReady();
            return CreateAssetHandle(location);
        }

        public UniTask<AssetHandle> LoadAssetAsync(ResourceLocation location, CancellationToken cancellationToken = default)
        {
            return LoadAssetAsyncInternal(location, cancellationToken);
        }

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

        public UniTask<IReadOnlyList<AssetHandle>> LoadAssetsAsync(ResourceLocation location, CancellationToken cancellationToken = default)
        {
            return LoadAssetsAsyncInternal(location, cancellationToken);
        }

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

        public UniTask<RawFileHandle> LoadRawFileAsync(ResourceLocation location, CancellationToken cancellationToken = default)
        {
            return LoadRawFileAsyncInternal(location, cancellationToken);
        }

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

        internal sealed class AssetRecord
        {
            private readonly ResourcePackage _package;
            private readonly AssetRecord[] _dependencies;
            private float? _pendingReleaseTime;

            public AssetRecord(ResourcePackage package, ResourceLocation location, UnityEngine.Object asset, string recordKey, AssetRecord[] dependencies)
            {
                _package = package;
                PackageName = package.PackageName;
                Location = location;
                Asset = asset;
                RecordKey = recordKey;
                _dependencies = dependencies ?? Array.Empty<AssetRecord>();
                RefCount = 1;
            }

            public string PackageName { get; }

            public ResourceLocation Location { get; }

            public UnityEngine.Object Asset { get; private set; }

            public string RecordKey { get; }

            public int RefCount { get; private set; }

            public bool IsValid => Asset != null;

            public void Retain()
            {
                RefCount++;
                _pendingReleaseTime = null;
            }

            public void Release()
            {
                if (RefCount <= 0)
                {
                    return;
                }

                RefCount--;
                if (RefCount == 0)
                {
                    ReleaseDependencies();
                    _pendingReleaseTime = Time.realtimeSinceStartup + Mathf.Max(0f, _package.Options.ReleaseDelaySeconds);
                }
            }

            public bool CanUnload(float now, bool force)
            {
                if (RefCount > 0)
                {
                    return false;
                }

                if (force)
                {
                    return true;
                }

                return _pendingReleaseTime.HasValue && now >= _pendingReleaseTime.Value;
            }

            public void Unload()
            {
                Asset = null;
            }

            private void ReleaseDependencies()
            {
                for (var i = 0; i < _dependencies.Length; i++)
                {
                    _dependencies[i].Release();
                }
            }
        }

        internal sealed class RawFileRecord
        {
            private readonly ResourcePackage _package;
            private float? _pendingReleaseTime;

            public RawFileRecord(ResourcePackage package, ResourceLocation location, string fullPath, byte[] data)
            {
                _package = package;
                PackageName = package.PackageName;
                Location = location;
                FullPath = fullPath;
                Data = data;
                Text = TryReadText(data);
                RefCount = 1;
            }

            public string PackageName { get; }

            public ResourceLocation Location { get; }

            public string FullPath { get; }

            public byte[] Data { get; private set; }

            public string Text { get; private set; }

            public int RefCount { get; private set; }

            public void Retain()
            {
                RefCount++;
                _pendingReleaseTime = null;
            }

            public void Release()
            {
                if (RefCount <= 0)
                {
                    return;
                }

                RefCount--;
                if (RefCount == 0)
                {
                    _pendingReleaseTime = Time.realtimeSinceStartup + Mathf.Max(0f, _package.Options.ReleaseDelaySeconds);
                }
            }

            public bool CanUnload(float now, bool force)
            {
                if (RefCount > 0)
                {
                    return false;
                }

                if (force)
                {
                    return true;
                }

                return _pendingReleaseTime.HasValue && now >= _pendingReleaseTime.Value;
            }

            public void Unload()
            {
                Data = null;
                Text = null;
            }

            private static string TryReadText(byte[] data)
            {
                if (data == null || data.Length == 0)
                {
                    return string.Empty;
                }

                try
                {
                    return System.Text.Encoding.UTF8.GetString(data);
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        private AssetHandle CreateAssetHandle(ResourceLocation location)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            var entry = ResolveAssetEntry(location);
            return CreateAssetHandle(entry);
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

        private ResourceEntry ResolveAssetEntry(ResourceLocation location)
        {
            var matches = Find(location, ResourceEntryKind.Asset);
            if (matches.Count == 0)
            {
                throw new InvalidOperationException($"Failed to find asset in package '{PackageName}'.");
            }

            return matches[0];
        }

        private ResourceEntry ResolveSceneEntry(ResourceLocation location)
        {
            var matches = Find(location, ResourceEntryKind.Scene);
            if (matches.Count == 0)
            {
                throw new InvalidOperationException($"Failed to find scene in package '{PackageName}'.");
            }

            return matches[0];
        }

        private ResourceEntry ResolveRawFileEntry(ResourceLocation location)
        {
            var matches = Find(location, ResourceEntryKind.RawFile);
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

        private static ResourceLocation CreateLocation(ResourceEntry entry)
        {
            return new ResourceLocation
            {
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

            if (_state != ResourcePackageState.Ready)
            {
                await InitializeAsync(cancellationToken);
            }

            try
            {
                await _runtime.EnsurePackageReadyAsync(_context, cancellationToken);
            }
            catch (Exception exception)
            {
                _state = ResourcePackageState.Failed;
                _lastError = exception.Message;
                throw;
            }
        }

        private void EnsureReady()
        {
            if (_state == ResourcePackageState.Failed)
            {
                throw new InvalidOperationException($"Package '{PackageName}' initialization failed: {_lastError}");
            }

            if (_state != ResourcePackageState.Ready)
            {
                InitializeAsync().GetAwaiter().GetResult();
            }

            try
            {
                _runtime.EnsurePackageReadyAsync(_context).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                _state = ResourcePackageState.Failed;
                _lastError = exception.Message;
                throw;
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
                Kind = entry.Kind
            };
        }

        private AssetRecord[] ResolveDependencies(ResourceEntry entry)
        {
            var records = new List<AssetRecord>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            ResolveDependenciesRecursive(entry, records, visited, new HashSet<string>(StringComparer.Ordinal));
            return records.ToArray();
        }

        private void ResolveDependenciesRecursive(ResourceEntry entry, List<AssetRecord> results, HashSet<string> visited, HashSet<string> stack)
        {
            if (entry.Dependencies == null || entry.Dependencies.Count == 0)
            {
                return;
            }

            for (var i = 0; i < entry.Dependencies.Count; i++)
            {
                var dependencyName = entry.Dependencies[i];
                if (string.IsNullOrWhiteSpace(dependencyName))
                {
                    continue;
                }

                if (!stack.Add(dependencyName))
                {
                    throw new InvalidOperationException($"Detected circular resource dependency in package '{PackageName}': {dependencyName}");
                }

                var dependencyEntry = ResolveAssetEntry(new ResourceLocation { Name = dependencyName });
                var dependencyIdentity = BuildAssetKey(dependencyEntry);
                if (visited.Contains(dependencyIdentity))
                {
                    stack.Remove(dependencyName);
                    continue;
                }

                ResolveDependenciesRecursive(dependencyEntry, results, visited, stack);

                var dependencyRecordKey = BuildAssetKey(dependencyEntry);
                if (!_assetRecords.TryGetValue(dependencyRecordKey, out var dependencyRecord))
                {
                    var dependencyAsset = ResolveAsset(dependencyEntry);
                    if (dependencyAsset == null)
                    {
                        stack.Remove(dependencyName);
                        continue;
                    }

                    dependencyRecord = new AssetRecord(this, CreateLocation(dependencyEntry), dependencyAsset, dependencyRecordKey, Array.Empty<AssetRecord>());
                    _assetRecords.Add(dependencyRecordKey, dependencyRecord);
                }
                else
                {
                    dependencyRecord.Retain();
                }

                visited.Add(dependencyIdentity);
                results.Add(dependencyRecord);
                stack.Remove(dependencyName);
            }
        }
    }
}
