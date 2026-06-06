using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using GameDeveloperKit.Resource;
using Newtonsoft.Json.Linq;

namespace GameDeveloperKit.Config
{
    public sealed partial class ConfigModule : GameModuleBase
    {
        private readonly Dictionary<Type, object> m_Tables = new Dictionary<Type, object>();

        private readonly Dictionary<Type, UniTaskCompletionSource<object>> m_PendingLoads = new Dictionary<Type, UniTaskCompletionSource<object>>();

        public override async UniTask Startup()
        {
            Clear();
            LoadTagCatalog();
            await UniTask.CompletedTask;
        }

        public override UniTask Shutdown()
        {
            Clear();
            return UniTask.CompletedTask;
        }

        public UniTask<Table<TRow>> LoadTableAsync<TRow>() where TRow : IConfig
        {
            return LoadTableAsync<TRow>(GetTablePath(typeof(TRow)));
        }

        public async UniTask<Table<TRow>> LoadTableAsync<TRow>(string path) where TRow : IConfig
        {
            var rowType = typeof(TRow);
            ValidatePath(path);

            if (m_Tables.TryGetValue(rowType, out var cached))
            {
                return (Table<TRow>)cached;
            }

            if (m_PendingLoads.TryGetValue(rowType, out var pending))
            {
                return (Table<TRow>)await pending.Task;
            }

            var completionSource = new UniTaskCompletionSource<object>();
            m_PendingLoads.Add(rowType, completionSource);
            try
            {
                var json = await LoadJsonTextAsync<TRow>(path);
                var rows = DeserializeRows<TRow>(json, path);
                var table = new Table<TRow>(rows);
                m_Tables.Add(rowType, table);
                completionSource.TrySetResult(table);

                return table;
            }
            catch (Exception exception)
            {
                var loadException = exception is GameException
                    ? exception
                    : new GameException($"Failed to load config table '{rowType.Name}' from '{path}'. {exception.Message}", exception);

                completionSource.TrySetException(loadException);
                try
                {
                    await completionSource.Task;
                }
                catch
                {
                }

                throw loadException;
            }
            finally
            {
                m_PendingLoads.Remove(rowType);
            }
        }

        public Table<TRow> GetTable<TRow>() where TRow : IConfig
        {
            var type = typeof(TRow);

            if (!m_Tables.TryGetValue(type, out var table))
            {
                throw new GameException($"Config table '{type.Name}' is not loaded.");
            }

            return (Table<TRow>)table;
        }

        public bool TryGetTable<TRow>(out Table<TRow> table) where TRow : IConfig
        {
            var type = typeof(TRow);

            if (m_Tables.TryGetValue(type, out var value))
            {
                table = (Table<TRow>)value;
                return true;
            }

            table = default;
            return false;
        }

        public TRow Find<TRow>(Func<TRow, bool> predicate) where TRow : IConfig
        {
            return FirstOrDefault(predicate);
        }

        public IEnumerable<TRow> Where<TRow>(Func<TRow, bool> predicate) where TRow : IConfig
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return GetTable<TRow>().Where(predicate);
        }

        public TRow FirstOrDefault<TRow>() where TRow : IConfig
        {
            return GetTable<TRow>().FirstOrDefault();
        }

        public TRow FirstOrDefault<TRow>(Func<TRow, bool> predicate) where TRow : IConfig
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return GetTable<TRow>().Find(predicate);
        }

        public void Unload<TRow>() where TRow : IConfig
        {
            m_Tables.Remove(typeof(TRow));
        }

        private void Clear()
        {
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

        private static async UniTask<string> LoadJsonTextAsync<TRow>(string path) where TRow : IConfig
        {
            if (IsHttpUrl(path))
            {
                return await DownloadJsonTextAsync<TRow>(path);
            }

            if (System.IO.File.Exists(path))
            {
                await UniTask.Yield();
                return System.IO.File.ReadAllText(path);
            }

            return await LoadResourceJsonTextAsync<TRow>(path);
        }

        private static async UniTask<string> DownloadJsonTextAsync<TRow>(string url) where TRow : IConfig
        {
            try
            {
                var handle = App.Download.DownloadAsync(url);
                await handle.WaitCompletionAsync();
                if (handle.Status is not OperationStatus.Succeeded)
                {
                    throw new GameException($"Download failed: {url}", handle.Error);
                }

                if (string.IsNullOrEmpty(handle.TempPath) || !System.IO.File.Exists(handle.TempPath))
                {
                    throw new GameException($"Downloaded config file not found: {url}");
                }

                return System.IO.File.ReadAllText(handle.TempPath);
            }
            catch (Exception exception)
            {
                throw new GameException($"Failed to download config table '{typeof(TRow).Name}' from '{url}'.", exception);
            }
        }

        private static async UniTask<string> LoadResourceJsonTextAsync<TRow>(string path) where TRow : IConfig
        {
            try
            {
                var rawAsset = await App.Resource.LoadRawAssetAsync(path);
                if (rawAsset == null || rawAsset.Status is not ResourceStatus.Succeeded)
                {
                    throw new GameException($"Resource config load failed: {path}", rawAsset?.Error);
                }

                return rawAsset.GetString();
            }
            catch (Exception exception)
            {
                throw new GameException($"Failed to load config table '{typeof(TRow).Name}' resource '{path}'.", exception);
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
    }
}
