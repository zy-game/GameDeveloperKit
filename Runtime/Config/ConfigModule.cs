using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Download;
using GameDeveloperKit.File;
using GameDeveloperKit.Operation;
using GameDeveloperKit.Resource;
using Newtonsoft.Json.Linq;

namespace GameDeveloperKit.Config
{
    [ModuleDependency(typeof(ResourceModule))]
    [ModuleDependency(typeof(DownloadModule))]
    [ModuleDependency(typeof(FileModule))]
    public sealed partial class ConfigModule : GameModuleBase
    {
        private readonly Dictionary<Type, LoadedTableEntry> m_Tables = new Dictionary<Type, LoadedTableEntry>();

        private readonly Dictionary<Type, PendingTableLoad> m_PendingLoads = new Dictionary<Type, PendingTableLoad>();

        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

        private TimeSpan m_LoadTimeout = DefaultTimeout;

        public TimeSpan LoadTimeout
        {
            get => m_LoadTimeout;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Config load timeout must be positive.");
                }

                m_LoadTimeout = value;
            }
        }

        /// <summary>
        /// 启动 member。
        /// </summary>
        public override void Startup()
        {
            Clear();
            LoadTagCatalog();
        }

        /// <summary>
        /// 关闭 member。
        /// </summary>
        public override void Shutdown()
        {
            Clear();
        }

        /// <summary>
        /// 加载 Table Async。
        /// </summary>
        /// <typeparam name="TRow">泛型类型参数。</typeparam>
        public UniTask<Table<TRow>> LoadTableAsync<TRow>() where TRow : IConfig
        {
            return LoadTableAsync<TRow>(GetTablePath(typeof(TRow)));
        }

        /// <summary>
        /// 加载 Table Async。
        /// </summary>
        /// <typeparam name="TRow">泛型类型参数。</typeparam>
        public async UniTask<Table<TRow>> LoadTableAsync<TRow>(string path) where TRow : IConfig
        {
            var rowType = typeof(TRow);
            ValidatePath(path);
            var source = NormalizeSource(path);

            if (m_Tables.TryGetValue(rowType, out var cached))
            {
                EnsureSourceMatches(rowType, cached.Source, source, "loaded");
                return (Table<TRow>)cached.Table;
            }

            if (m_PendingLoads.TryGetValue(rowType, out var pending))
            {
                EnsureSourceMatches(rowType, pending.Source, source, "loading");
                return await WaitForPendingLoadAsync<TRow>(rowType, pending, path);
            }

            pending = new PendingTableLoad(source);
            m_PendingLoads.Add(rowType, pending);
            pending.AddWaiter();
            RunTableLoadAsync<TRow>(rowType, pending, path).Forget(UnityEngine.Debug.LogException);
            try
            {
                return (Table<TRow>)await pending.Completion.Task.Timeout(m_LoadTimeout);
            }
            catch (Exception exception)
            {
                throw CreateLoadException(rowType, path, exception);
            }
            finally
            {
                ReleasePendingWaiter(rowType, pending);
            }
        }

        private async UniTask<Table<TRow>> WaitForPendingLoadAsync<TRow>(
            Type rowType,
            PendingTableLoad pending,
            string path) where TRow : IConfig
        {
            pending.AddWaiter();
            try
            {
                return (Table<TRow>)await pending.Completion.Task.Timeout(m_LoadTimeout);
            }
            catch (Exception exception)
            {
                throw CreateLoadException(rowType, path, exception);
            }
            finally
            {
                ReleasePendingWaiter(rowType, pending);
            }
        }

        private async UniTask RunTableLoadAsync<TRow>(
            Type rowType,
            PendingTableLoad pending,
            string path) where TRow : IConfig
        {
            try
            {
                var json = await LoadJsonTextAsync<TRow>(path, pending.Cancellation.Token);
                pending.Cancellation.Token.ThrowIfCancellationRequested();
                var table = new Table<TRow>(DeserializeRows<TRow>(json, path));
                pending.IsCompleted = true;
                if (IsCurrentPending(rowType, pending) && pending.WaiterCount > 0)
                {
                    m_Tables.Add(rowType, new LoadedTableEntry(pending.Source, table));
                }

                RemovePending(rowType, pending);
                if (pending.IsAbandoned is false)
                {
                    pending.Completion.TrySetResult(table);
                }
            }
            catch (Exception exception)
            {
                pending.IsCompleted = true;
                RemovePending(rowType, pending);
                if (pending.IsAbandoned is false)
                {
                    pending.Completion.TrySetException(CreateLoadException(rowType, path, exception));
                }
            }
            finally
            {
                RemovePending(rowType, pending);
                pending.Cancellation.Dispose();
            }
        }

        private void RemovePending(Type rowType, PendingTableLoad pending)
        {
            if (IsCurrentPending(rowType, pending))
            {
                m_PendingLoads.Remove(rowType);
            }
        }

        /// <summary>
        /// 获取 Table。
        /// </summary>
        /// <typeparam name="TRow">泛型类型参数。</typeparam>
        public Table<TRow> GetTable<TRow>() where TRow : IConfig
        {
            var type = typeof(TRow);

            if (!m_Tables.TryGetValue(type, out var table))
            {
                throw new GameException($"Config table '{type.Name}' is not loaded.");
            }

            return (Table<TRow>)table.Table;
        }

        /// <summary>
        /// 尝试获取 Table。
        /// </summary>
        /// <typeparam name="TRow">泛型类型参数。</typeparam>
        public bool TryGetTable<TRow>(out Table<TRow> table) where TRow : IConfig
        {
            var type = typeof(TRow);

            if (m_Tables.TryGetValue(type, out var value))
            {
                table = (Table<TRow>)value.Table;
                return true;
            }

            table = default;
            return false;
        }

        /// <summary>
        /// 查找 member。
        /// </summary>
        /// <typeparam name="TRow">泛型类型参数。</typeparam>
        public TRow Find<TRow>(Func<TRow, bool> predicate) where TRow : IConfig
        {
            return FirstOrDefault(predicate);
        }

        /// <summary>
        /// 执行 Where。
        /// </summary>
        /// <typeparam name="TRow">泛型类型参数。</typeparam>
        public IEnumerable<TRow> Where<TRow>(Func<TRow, bool> predicate) where TRow : IConfig
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return GetTable<TRow>().Where(predicate);
        }

        /// <summary>
        /// 执行 First Or Default。
        /// </summary>
        /// <typeparam name="TRow">泛型类型参数。</typeparam>
        public TRow FirstOrDefault<TRow>() where TRow : IConfig
        {
            return GetTable<TRow>().FirstOrDefault();
        }

        /// <summary>
        /// 执行 First Or Default。
        /// </summary>
        /// <typeparam name="TRow">泛型类型参数。</typeparam>
        public TRow FirstOrDefault<TRow>(Func<TRow, bool> predicate) where TRow : IConfig
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return GetTable<TRow>().Find(predicate);
        }

        /// <summary>
        /// 卸载 member。
        /// </summary>
        /// <typeparam name="TRow">泛型类型参数。</typeparam>
        public void Unload<TRow>() where TRow : IConfig
        {
            m_Tables.Remove(typeof(TRow));
        }

        /// <summary>
        /// 清理 member。
        /// </summary>
        private void Clear()
        {
            foreach (var pending in m_PendingLoads.Values)
            {
                pending.Cancel();
            }

            m_PendingLoads.Clear();
            m_Tables.Clear();
            m_Tags = TagCatalog.Empty;
        }

        private string GetTablePath(Type tableType)
        {
            var option = tableType.GetCustomAttribute<TableOptionAttribute>();
            if (option == null)
            {
                throw new GameException($"Config table '{tableType.Name}' does not have a TableOptionAttribute.");
            }

            return option.Path;
        }

        private static async UniTask<string> LoadJsonTextAsync<TRow>(
            string path,
            CancellationToken cancellationToken) where TRow : IConfig
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsHttpUrl(path))
            {
                return await DownloadJsonTextAsync<TRow>(path, cancellationToken);
            }

            if (TryResolveLocalFilePath(path, out var localPath))
            {
                using (var stream = await App.File.OpenExternalReadAsync(localPath))
                using (var reader = new StreamReader(stream))
                {
                    var json = await reader.ReadToEndAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                    return json;
                }
            }

            return await LoadResourceJsonTextAsync<TRow>(path, cancellationToken);
        }

        private static async UniTask<string> DownloadJsonTextAsync<TRow>(
            string url,
            CancellationToken cancellationToken) where TRow : IConfig
        {
            DownloadHandler handle = null;
            CancellationTokenRegistration cancellationRegistration = default;
            try
            {
                handle = App.Download.DownloadAsync(url);
                cancellationRegistration = cancellationToken.Register(handle.SetCancel);
                await handle.WaitCompletionAsync();
                cancellationToken.ThrowIfCancellationRequested();
                if (handle.Status is not OperationStatus.Succeeded)
                {
                    throw new GameException($"Download failed: {url}", handle.Error);
                }

                using (var stream = await handle.OpenReadAsync())
                using (var reader = new StreamReader(stream))
                {
                    var json = await reader.ReadToEndAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                    return json;
                }
            }
            catch (Exception exception)
            {
                throw new GameException($"Failed to download config table '{typeof(TRow).Name}' from '{url}'.", exception);
            }
            finally
            {
                cancellationRegistration.Dispose();
                if (handle?.Status is OperationStatus.Succeeded)
                {
                    handle.ReleaseResult();
                }
            }
        }

        private static async UniTask<string> LoadResourceJsonTextAsync<TRow>(
            string path,
            CancellationToken cancellationToken) where TRow : IConfig
        {
            RawAssetHandle rawAsset = null;
            try
            {
                rawAsset = await App.Resource.LoadRawAssetAsync(path);
                cancellationToken.ThrowIfCancellationRequested();
                if (rawAsset == null || rawAsset.Status is not ResourceStatus.Succeeded)
                {
                    throw new GameException($"Resource config load failed: {path}", rawAsset?.Error);
                }

                var ownedRawAsset = rawAsset;
                rawAsset = null;
                return ReadAndReleaseRawAssetText(ownedRawAsset, path);
            }
            catch (Exception exception)
            {
                throw new GameException($"Failed to load config table '{typeof(TRow).Name}' resource '{path}'.", exception);
            }
            finally
            {
                rawAsset?.Release();
            }
        }

        internal static string ReadAndReleaseRawAssetText(RawAssetHandle rawAsset, string path)
        {
            try
            {
                if (rawAsset == null)
                {
                    throw new GameException($"Resource config load returned no handle: {path}");
                }

                if (rawAsset.Status is not ResourceStatus.Succeeded)
                {
                    throw new GameException($"Resource config load failed: {path}", rawAsset.Error);
                }

                return rawAsset.GetString();
            }
            finally
            {
                rawAsset?.Release();
            }
        }

        private static List<TRow> DeserializeRows<TRow>(string json, string path) where TRow : IConfig
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new GameException($"Config table '{typeof(TRow).Name}' JSON is empty: {path}");
            }

            try
            {
                var token = JToken.Parse(json);
                var rowsToken = token.Type switch
                {
                    JTokenType.Array => token,
                    JTokenType.Object => token["rows"],
                    _ => null,
                };

                if (rowsToken == null || rowsToken.Type != JTokenType.Array)
                {
                    throw new GameException($"Config table '{typeof(TRow).Name}' JSON must be an array or contain a rows array: {path}");
                }

                var rows = rowsToken.ToObject<List<TRow>>();
                if (rows == null)
                {
                    throw new GameException($"Config table '{typeof(TRow).Name}' JSON did not produce rows: {path}");
                }

                return rows;
            }
            catch (Exception exception) when (exception is not GameException)
            {
                throw new GameException($"Failed to parse config table '{typeof(TRow).Name}' JSON from '{path}'.", exception);
            }
        }

        private static bool IsHttpUrl(string path)
        {
            return Uri.TryCreate(path, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static bool TryResolveLocalFilePath(string path, out string localPath)
        {
            if (TryResolveExistingExternalPath(path, out localPath))
            {
                return true;
            }

#if UNITY_EDITOR
            var packageRelativePath = ResolveFrameworkPackageRelativePath(path);
            if (string.IsNullOrWhiteSpace(packageRelativePath))
            {
                localPath = null;
                return false;
            }

            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ConfigModule).Assembly);
            if (string.IsNullOrWhiteSpace(packageInfo?.resolvedPath) is false)
            {
                var resolvedPackageFile = System.IO.Path.Combine(packageInfo.resolvedPath, packageRelativePath);
                if (TryResolveExistingExternalPath(resolvedPackageFile, out localPath))
                {
                    return true;
                }
            }

            var packageFile = System.IO.Path.Combine("Packages/com.gamedeveloperkit.framework", packageRelativePath);
            if (TryResolveExistingExternalPath(packageFile, out localPath))
            {
                return true;
            }

            var assetFile = System.IO.Path.Combine("Assets/GameDeveloperKit", packageRelativePath);
            if (TryResolveExistingExternalPath(assetFile, out localPath))
            {
                return true;
            }
#endif

            localPath = null;
            return false;
        }

        private static bool TryResolveExistingExternalPath(string path, out string absolutePath)
        {
            try
            {
                absolutePath = Path.GetFullPath(path);
                return App.File.ExternalFileExists(absolutePath);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                absolutePath = null;
                return false;
            }
        }

#if UNITY_EDITOR
        private static string ResolveFrameworkPackageRelativePath(string path)
        {
            var normalized = path?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            const string assetsRoot = "Assets/GameDeveloperKit/";
            if (normalized.StartsWith(assetsRoot, StringComparison.Ordinal))
            {
                return normalized.Substring(assetsRoot.Length);
            }

            const string packageRoot = "Packages/com.gamedeveloperkit.framework/";
            if (normalized.StartsWith(packageRoot, StringComparison.Ordinal))
            {
                return normalized.Substring(packageRoot.Length);
            }

            return null;
        }
#endif

        private static void ValidatePath(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Config path cannot be empty.", nameof(path));
            }
        }

        private void ReleasePendingWaiter(Type rowType, PendingTableLoad pending)
        {
            if (pending.ReleaseWaiter() > 0 || pending.IsCompleted)
            {
                return;
            }

            pending.IsAbandoned = true;
            if (IsCurrentPending(rowType, pending))
            {
                m_PendingLoads.Remove(rowType);
            }

            pending.Cancel();
        }

        private bool IsCurrentPending(Type rowType, PendingTableLoad pending)
        {
            return m_PendingLoads.TryGetValue(rowType, out var current) &&
                   ReferenceEquals(current, pending);
        }

        private static void EnsureSourceMatches(Type rowType, string currentSource, string requestedSource, string state)
        {
            if (string.Equals(currentSource, requestedSource, StringComparison.Ordinal))
            {
                return;
            }

            throw new GameException(
                $"Config table '{rowType.Name}' is already {state} from source '{currentSource}' and cannot use source '{requestedSource}'.");
        }

        private static string NormalizeSource(string path)
        {
            var source = path.Trim();
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return uri.AbsoluteUri;
            }

            if (Path.IsPathRooted(source))
            {
                return Path.GetFullPath(source).Replace('\\', '/');
            }

            return source.Replace('\\', '/');
        }

        private static GameException CreateLoadException(Type rowType, string path, Exception exception)
        {
            return exception as GameException ?? new GameException(
                $"Failed to load config table '{rowType.Name}' from '{path}'. {exception.Message}",
                exception);
        }

        private sealed class LoadedTableEntry
        {
            public LoadedTableEntry(string source, object table)
            {
                Source = source;
                Table = table;
            }

            public string Source { get; }

            public object Table { get; }
        }

        private sealed class PendingTableLoad
        {
            public PendingTableLoad(string source)
            {
                Source = source;
            }

            public string Source { get; }

            public UniTaskCompletionSource<object> Completion { get; } = new UniTaskCompletionSource<object>();

            public CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();

            public int WaiterCount { get; private set; }

            public bool IsCompleted { get; set; }

            public bool IsAbandoned { get; set; }

            public void AddWaiter()
            {
                WaiterCount++;
            }

            public int ReleaseWaiter()
            {
                if (WaiterCount > 0)
                {
                    WaiterCount--;
                }

                return WaiterCount;
            }

            public void Cancel()
            {
                if (Cancellation.IsCancellationRequested is false)
                {
                    Cancellation.Cancel();
                }
            }
        }
    }
}
