using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Data.Internal;
using GameDeveloperKit.Data.Serializers;
using GameDeveloperKit.File;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameDeveloperKit.Data
{
    public sealed partial class DataModule : GameModuleBase
    {
        /// <summary>
        /// 加载 Data Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public UniTask<T> LoadDataAsync<T>()
        {
            return LoadDataAsync<T>(DataConstants.DefaultKey);
        }

        /// <summary>
        /// 加载 Data Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public async UniTask<T> LoadDataAsync<T>(string key)
        {
            var slot = Slot.Create<T>(key);
            var fileModule = GetFileModule(slot, null, PathUtility.GetIndexPath(slot));
            var indexPath = PathUtility.GetIndexPath(slot);
            await m_PersistenceMutationGate.WaitAsync();
            try
            {
                var index = await ReadReconciledIndexAsync(fileModule, slot, indexPath);
                if (index == null)
                {
                    var defaultData = CreateDefaultData<T>(slot);
                    SetEntry(slot, new Entry(defaultData));
                    return defaultData;
                }

                var versionPath = PathUtility.GetVersionPath(slot, index.CurrentVersion);
                var data = await ReadDocumentDataAsync<T>(fileModule, slot, index.CurrentVersion, versionPath);
                SetEntry(slot, new Entry(data, index.CurrentVersion));
                return data;
            }
            finally
            {
                m_PersistenceMutationGate.Release();
            }
        }

        /// <summary>
        /// 加载 Version Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public UniTask<T> LoadVersionAsync<T>(string version)
        {
            return LoadVersionAsync<T>(DataConstants.DefaultKey, version);
        }

        /// <summary>
        /// 加载 Version Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public async UniTask<T> LoadVersionAsync<T>(string key, string version)
        {
            ValidateVersion(version);
            var slot = Slot.Create<T>(key);
            var indexPath = PathUtility.GetIndexPath(slot);
            var versionPath = PathUtility.GetVersionPath(slot, version);
            var fileModule = GetFileModule(slot, version, versionPath);
            await m_PersistenceMutationGate.WaitAsync();
            try
            {
                var index = await ReadRequiredIndexAsync(fileModule, slot, version, indexPath);
                if (!index.Versions.Any(info => info.Version == version))
                {
                    throw CreateException(slot, version, versionPath, "Data version is not recorded in version index.");
                }

                var data = await ReadDocumentDataAsync<T>(fileModule, slot, version, versionPath);
                SetEntry(slot, new Entry(data, version));
                return data;
            }
            finally
            {
                m_PersistenceMutationGate.Release();
            }
        }

        /// <summary>
        /// 保存 Data Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public UniTask<DataVersionInfo> SaveDataAsync<T>()
        {
            return SaveDataAsync<T>(DataConstants.DefaultKey);
        }

        /// <summary>
        /// 保存 Data Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public UniTask<DataVersionInfo> SaveDataAsync<T>(string key)
        {
            var slot = Slot.Create<T>(key);
            if (!m_Entries.ContainsKey(slot))
            {
                throw CreateException(slot, null, PathUtility.GetIndexPath(slot), "Data slot is not cached.");
            }

            return SaveSlotAsync(slot, null);
        }

        /// <summary>
        /// 保存 Data Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public UniTask<DataVersionInfo> SaveDataAsync<T>(string key, string version)
        {
            ValidateVersion(version);
            var slot = Slot.Create<T>(key);
            if (!m_Entries.ContainsKey(slot))
            {
                throw CreateException(slot, version, PathUtility.GetVersionPath(slot, version), "Data slot is not cached.");
            }

            return SaveSlotAsync(slot, version);
        }

        /// <summary>
        /// 保存 All Async。
        /// </summary>
        public async UniTask SaveAllAsync()
        {
            var slots = new List<Slot>(m_Entries.Keys);
            foreach (var slot in slots)
            {
                await SaveSlotAsync(slot, null);
            }
        }

        /// <summary>
        /// 执行 Rollback Data Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public UniTask<T> RollbackDataAsync<T>(string version)
        {
            return RollbackDataAsync<T>(DataConstants.DefaultKey, version);
        }

        /// <summary>
        /// 执行 Rollback Data Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public async UniTask<T> RollbackDataAsync<T>(string key, string version)
        {
            ValidateVersion(version);
            var slot = Slot.Create<T>(key);
            var indexPath = PathUtility.GetIndexPath(slot);
            var versionPath = PathUtility.GetVersionPath(slot, version);
            var fileModule = GetFileModule(slot, version, versionPath);
            await m_PersistenceMutationGate.WaitAsync();
            try
            {
                var index = await ReadRequiredIndexAsync(fileModule, slot, version, indexPath);
                if (!index.Versions.Any(info => info.Version == version))
                {
                    throw CreateException(slot, version, versionPath, "Data version is not recorded in version index.");
                }

                var data = await ReadDocumentDataAsync<T>(fileModule, slot, version, versionPath);
                var committedIndex = CreateCommittedIndex(index, version, null, out var retiredVersions);
                await WriteIndexAsync(fileModule, slot, committedIndex, indexPath);
                SetEntry(slot, new Entry(data, version));
                await DeleteRetiredVersionsAsync(fileModule, slot, version, retiredVersions);
                return data;
            }
            finally
            {
                m_PersistenceMutationGate.Release();
            }
        }

        /// <summary>
        /// 获取 Versions Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public UniTask<IReadOnlyList<DataVersionInfo>> GetVersionsAsync<T>()
        {
            return GetVersionsAsync<T>(DataConstants.DefaultKey);
        }

        /// <summary>
        /// 获取 Versions Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public async UniTask<IReadOnlyList<DataVersionInfo>> GetVersionsAsync<T>(string key)
        {
            var slot = Slot.Create<T>(key);
            var indexPath = PathUtility.GetIndexPath(slot);
            var fileModule = GetFileModule(slot, null, indexPath);
            await m_PersistenceMutationGate.WaitAsync();
            try
            {
                var index = await ReadReconciledIndexAsync(fileModule, slot, indexPath);
                if (index == null)
                {
                    return Array.Empty<DataVersionInfo>();
                }

                return index.Versions
                    .Select(info => new DataVersionInfo(info.Version, info.SavedAtUtc, info.Version == index.CurrentVersion))
                    .ToArray();
            }
            finally
            {
                m_PersistenceMutationGate.Release();
            }
        }

        /// <summary>
        /// 执行 Delete Data Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public UniTask DeleteDataAsync<T>()
        {
            return DeleteDataAsync<T>(DataConstants.DefaultKey);
        }

        /// <summary>
        /// 执行 Delete Data Async。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public async UniTask DeleteDataAsync<T>(string key)
        {
            var slot = Slot.Create<T>(key);
            var indexPath = PathUtility.GetIndexPath(slot);
            var fileModule = GetFileModule(slot, null, indexPath);
            await m_PersistenceMutationGate.WaitAsync();
            try
            {
                var index = await ReadReconciledIndexAsync(fileModule, slot, indexPath);
                await fileModule.DeleteAsync(indexPath);
                m_Entries.Remove(slot);
                if (index != null)
                {
                    await DeleteRetiredVersionsAsync(fileModule, slot, null, index.Versions);
                }
            }
            finally
            {
                m_PersistenceMutationGate.Release();
            }
        }

        private async UniTask<DataVersionInfo> SaveSlotAsync(Slot slot, string version)
        {
            var indexPath = PathUtility.GetIndexPath(slot);
            var fileModule = GetFileModule(slot, version, indexPath);
            await m_PersistenceMutationGate.WaitAsync();
            try
            {
                if (!m_Entries.TryGetValue(slot, out var entry))
                {
                    throw CreateException(slot, version, indexPath, "Data slot is not cached.");
                }

                var index = await ReadReconciledIndexAsync(fileModule, slot, indexPath) ?? CreateIndex(slot);
                var dataVersion = string.IsNullOrEmpty(version) ? GenerateVersion(index) : version;
                var versionPath = PathUtility.GetVersionPath(slot, dataVersion);
                if (index.Versions.Any(info => info.Version == dataVersion) || fileModule.Exists(versionPath))
                {
                    throw CreateException(slot, dataVersion, versionPath, "Data version already exists.");
                }

                var savedAtUtc = DateTimeOffset.UtcNow;
                var documentBytes = CreateDocumentBytes(slot, entry.Data, dataVersion, savedAtUtc, versionPath);
                await fileModule.WriteAsync(versionPath, dataVersion, documentBytes);

                var versionInfo = new DataVersionInfo(dataVersion, savedAtUtc, true);
                var committedIndex = CreateCommittedIndex(index, dataVersion, versionInfo, out var retiredVersions);
                try
                {
                    await WriteIndexAsync(fileModule, slot, committedIndex, indexPath);
                }
                catch (Exception commitException)
                {
                    try
                    {
                        await fileModule.DeleteAsync(versionPath);
                    }
                    catch (Exception cleanupException)
                    {
                        throw new AggregateException(
                            $"Data version '{dataVersion}' failed to commit and orphan cleanup also failed.",
                            commitException,
                            cleanupException);
                    }

                    throw;
                }

                entry.CurrentVersion = dataVersion;
                await DeleteRetiredVersionsAsync(fileModule, slot, dataVersion, retiredVersions);
                return versionInfo;
            }
            finally
            {
                m_PersistenceMutationGate.Release();
            }
        }

        private static T CreateDefaultData<T>(Slot slot)
        {
            try
            {
                return (T)Activator.CreateInstance(typeof(T), true);
            }
            catch (Exception exception)
            {
                throw CreateException(slot, null, null, $"Cannot create default data for '{typeof(T).FullName}'. Use SetData before reading this slot.", exception);
            }
        }

        private static VersionIndex CreateIndex(Slot slot)
        {
            return new VersionIndex
            {
                FormatVersion = FormatVersion,
                TypeKey = slot.TypeKey,
                Key = slot.Key,
            };
        }

        private static void ValidateVersion(string version)
        {
            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("Data version cannot be empty.", nameof(version));
            }
        }

        private static FileModule GetFileModule(Slot slot, string version, string path)
        {
            ValidatePersistenceContract(slot);
            if (App.TryGetRegistered<FileModule>(out var fileModule))
            {
                return fileModule;
            }

            throw CreateException(slot, version, path, "Data persistence requires registered FileModule.");
        }

        private static async UniTask<VersionIndex> ReadRequiredIndexAsync(FileModule fileModule, Slot slot, string version, string path)
        {
            var index = await ReadReconciledIndexAsync(fileModule, slot, path);
            if (index == null)
            {
                throw CreateException(slot, version, path, "Data version index does not exist.");
            }

            return index;
        }

        private static async UniTask<VersionIndex> ReadReconciledIndexAsync(FileModule fileModule, Slot slot, string path)
        {
            var bytes = await fileModule.ReadAsync(path);
            if (bytes == null)
            {
                await DeleteOrphanVersionsAsync(fileModule, slot, null);
                return null;
            }

            try
            {
                var json = Encoding.UTF8.GetString(bytes);
                var index = JsonConvert.DeserializeObject<VersionIndex>(json);
                ValidateIndex(slot, index, path);
                ValidateIndexedVersions(fileModule, slot, index, path);
                await DeleteOrphanVersionsAsync(fileModule, slot, index);
                return index;
            }
            catch (Exception exception) when (exception is not GameException)
            {
                throw CreateException(slot, null, path, "Failed to read data version index.", exception);
            }
        }

        private static void ValidateIndex(Slot slot, VersionIndex index, string path)
        {
            if (index == null)
            {
                throw CreateException(slot, null, path, "Data version index is empty.");
            }

            if (index.FormatVersion != FormatVersion)
            {
                throw CreateException(slot, index.CurrentVersion, path, "Unsupported data version index format.");
            }

            if (index.TypeKey != slot.TypeKey || index.Key != slot.Key)
            {
                throw CreateException(slot, index.CurrentVersion, path, "Data version index type key or data key does not match requested slot.");
            }

            if (index.Versions == null)
            {
                throw CreateException(slot, index.CurrentVersion, path, "Data version index versions are missing.");
            }

            if (index.Versions.Count == 0 || string.IsNullOrWhiteSpace(index.CurrentVersion))
            {
                throw CreateException(slot, index.CurrentVersion, path, "Data version index must contain a current version.");
            }

            var versions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var info in index.Versions)
            {
                if (string.IsNullOrWhiteSpace(info.Version) || !versions.Add(info.Version))
                {
                    throw CreateException(slot, info.Version, path, "Data version index contains an empty or duplicate version.");
                }
            }

            if (!versions.Contains(index.CurrentVersion))
            {
                throw CreateException(slot, index.CurrentVersion, path, "Data version index current version is not recorded.");
            }
        }

        private static void ValidateIndexedVersions(FileModule fileModule, Slot slot, VersionIndex index, string path)
        {
            foreach (var info in index.Versions)
            {
                var versionPath = PathUtility.GetVersionPath(slot, info.Version);
                if (!fileModule.Exists(versionPath))
                {
                    throw CreateException(slot, info.Version, path, $"Indexed data version document does not exist: {versionPath}");
                }
            }
        }

        private static async UniTask DeleteOrphanVersionsAsync(FileModule fileModule, Slot slot, VersionIndex index)
        {
            var referencedPaths = index == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(
                    index.Versions.Select(info => PathUtility.GetVersionPath(slot, info.Version)),
                    StringComparer.Ordinal);
            var versionsPrefix = PathUtility.GetVersionsPrefix(slot);
            var orphanPaths = fileModule.ListFiles()
                .Select(info => info.FilePath)
                .Where(path => path.StartsWith(versionsPrefix, StringComparison.Ordinal) && !referencedPaths.Contains(path))
                .ToArray();

            foreach (var orphanPath in orphanPaths)
            {
                await fileModule.DeleteAsync(orphanPath);
            }
        }

        private static VersionIndex CreateCommittedIndex(
            VersionIndex index,
            string currentVersion,
            DataVersionInfo? appendedVersion,
            out List<DataVersionInfo> retiredVersions)
        {
            var versions = new List<DataVersionInfo>(index.Versions);
            if (appendedVersion.HasValue)
            {
                versions.Add(appendedVersion.Value);
            }

            var retainedVersionNames = new HashSet<string>(StringComparer.Ordinal)
            {
                currentVersion
            };
            for (var versionIndex = versions.Count - 1;
                 versionIndex >= 0 && retainedVersionNames.Count < MaxRetainedVersions;
                 versionIndex--)
            {
                retainedVersionNames.Add(versions[versionIndex].Version);
            }

            var retainedVersions = versions
                .Where(info => retainedVersionNames.Contains(info.Version))
                .ToList();
            retiredVersions = versions
                .Where(info => !retainedVersionNames.Contains(info.Version))
                .ToList();

            return new VersionIndex
            {
                FormatVersion = index.FormatVersion,
                TypeKey = index.TypeKey,
                Key = index.Key,
                CurrentVersion = currentVersion,
                Versions = retainedVersions,
            };
        }

        private static async UniTask DeleteRetiredVersionsAsync(
            FileModule fileModule,
            Slot slot,
            string committedVersion,
            IEnumerable<DataVersionInfo> retiredVersions)
        {
            try
            {
                foreach (var info in retiredVersions)
                {
                    await fileModule.DeleteAsync(PathUtility.GetVersionPath(slot, info.Version));
                }
            }
            catch (Exception exception)
            {
                throw CreateException(
                    slot,
                    committedVersion,
                    PathUtility.GetIndexPath(slot),
                    "Data index was committed, but retired version cleanup failed.",
                    exception);
            }
        }

        private static async UniTask WriteIndexAsync(FileModule fileModule, Slot slot, VersionIndex index, string path)
        {
            try
            {
                var json = JsonConvert.SerializeObject(index, Formatting.Indented);
                await fileModule.WriteAsync(path, IndexVersion, Encoding.UTF8.GetBytes(json));
            }
            catch (Exception exception)
            {
                throw CreateException(slot, index.CurrentVersion, path, "Failed to write data version index.", exception);
            }
        }

        private async UniTask<T> ReadDocumentDataAsync<T>(FileModule fileModule, Slot slot, string version, string path)
        {
            var bytes = await fileModule.ReadAsync(path);
            if (bytes == null)
            {
                throw CreateException(slot, version, path, "Data version document does not exist.");
            }

            try
            {
                var json = Encoding.UTF8.GetString(bytes);
                var document = JsonConvert.DeserializeObject<Document>(json);
                ValidateDocument(slot, version, path, document);
                var payloadBytes = GetPayloadBytes(document, slot, version, path);
                var payload = MigratePayload(slot, document.SchemaVersion, new DataMigrationPayload(document.Serializer, payloadBytes), version, path);
                if (payload.Serializer != m_Serializer.Format)
                {
                    throw CreateException(slot, version, path, $"Data migration ended with serializer '{payload.Serializer}', expected '{m_Serializer.Format}'.");
                }

                var data = m_Serializer.Deserialize<T>(payload.GetBytes());
                if (data == null)
                {
                    throw CreateException(slot, version, path, "Data version document payload produced null data.");
                }

                return data;
            }
            catch (Exception exception) when (exception is not GameException)
            {
                throw CreateException(slot, version, path, "Failed to read data version document.", exception);
            }
        }

        private byte[] CreateDocumentBytes(Slot slot, object data, string version, DateTimeOffset savedAtUtc, string path)
        {
            try
            {
                var payloadBytes = SerializePayload(slot, data);
                var document = new Document
                {
                    FormatVersion = FormatVersion,
                    Serializer = m_Serializer.Format,
                    SchemaVersion = slot.SchemaVersion,
                    TypeKey = slot.TypeKey,
                    Key = slot.Key,
                    DataVersion = version,
                    TypeName = slot.Type.AssemblyQualifiedName,
                    SavedAtUtc = savedAtUtc,
                    Payload = CreatePayloadToken(payloadBytes),
                };
                var json = JsonConvert.SerializeObject(document, Formatting.Indented);
                return Encoding.UTF8.GetBytes(json);
            }
            catch (Exception exception)
            {
                throw CreateException(slot, version, path, "Failed to serialize data version document.", exception);
            }
        }

        private byte[] SerializePayload(Slot slot, object data)
        {
            try
            {
                return m_Serializer.Serialize(slot.Type, data);
            }
            catch (Exception exception)
            {
                throw CreateException(slot, null, null, "Failed to serialize data payload.", exception);
            }
        }

        private JToken CreatePayloadToken(byte[] payloadBytes)
        {
            if (m_Serializer.Format == JsonSerializerFormat)
            {
                var json = Encoding.UTF8.GetString(payloadBytes);
                return JToken.Parse(json);
            }

            return new JValue(Convert.ToBase64String(payloadBytes));
        }

        private byte[] GetPayloadBytes(Document document, Slot slot, string version, string path)
        {
            if (document.Payload == null)
            {
                throw CreateException(slot, version, path, "Data version document payload is missing.");
            }

            if (document.Serializer == JsonSerializerFormat)
            {
                return Encoding.UTF8.GetBytes(document.Payload.ToString(Formatting.None));
            }

            if (document.Payload.Type != JTokenType.String)
            {
                throw CreateException(slot, version, path, "Data version document payload is not encoded for its serializer.");
            }

            try
            {
                return Convert.FromBase64String(document.Payload.Value<string>());
            }
            catch (Exception exception)
            {
                throw CreateException(slot, version, path, "Failed to decode data version document payload.", exception);
            }
        }

        private void ValidateDocument(Slot slot, string version, string path, Document document)
        {
            if (document == null)
            {
                throw CreateException(slot, version, path, "Data version document is empty.");
            }

            if (document.FormatVersion != FormatVersion)
            {
                throw CreateException(slot, version, path, "Unsupported data version document format.");
            }

            if (string.IsNullOrWhiteSpace(document.Serializer))
            {
                throw CreateException(slot, version, path, "Data version document serializer is missing.");
            }

            if (document.SchemaVersion < 1)
            {
                throw CreateException(slot, version, path, "Data version document schema is missing or invalid.");
            }

            if (document.SchemaVersion > slot.SchemaVersion)
            {
                throw CreateException(slot, version, path, $"Data version document schema '{document.SchemaVersion}' is newer than supported schema '{slot.SchemaVersion}'.");
            }

            if (document.TypeKey != slot.TypeKey || document.Key != slot.Key || document.DataVersion != version)
            {
                throw CreateException(slot, version, path, "Data version document type key, data key or version does not match requested slot.");
            }
        }

        private DataMigrationPayload MigratePayload(Slot slot, int schemaVersion, DataMigrationPayload payload, string version, string path)
        {
            var currentVersion = schemaVersion;
            while (currentVersion < slot.SchemaVersion)
            {
                if (!m_Migrations.TryGetValue(slot.TypeKey, out var migrations)
                    || !migrations.TryGetValue(currentVersion, out var migration))
                {
                    throw CreateException(slot, version, path, $"Data migration '{currentVersion}->{currentVersion + 1}' is not registered.");
                }

                if (migration.FromVersion != currentVersion || migration.ToVersion != currentVersion + 1)
                {
                    throw CreateException(slot, version, path, $"Data migration registered for schema '{currentVersion}' no longer declares a consecutive version step.");
                }

                try
                {
                    payload = migration.Migrate(payload);
                }
                catch (Exception exception)
                {
                    throw CreateException(slot, version, path, $"Data migration '{currentVersion}->{currentVersion + 1}' failed.", exception);
                }

                if (payload == null)
                {
                    throw CreateException(slot, version, path, $"Data migration '{currentVersion}->{currentVersion + 1}' returned no payload.");
                }

                currentVersion++;
            }

            return payload;
        }

        private static string GenerateVersion(VersionIndex index)
        {
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss.fffffffZ");
            if (!index.Versions.Any(info => info.Version == timestamp))
            {
                return timestamp;
            }

            var counter = 1;
            while (true)
            {
                var version = $"{timestamp}-{counter}";
                if (!index.Versions.Any(info => info.Version == version))
                {
                    return version;
                }

                counter++;
            }
        }

        private static T GetEntryData<T>(Slot slot, Entry entry)
        {
            if (entry.Data is T data)
            {
                return data;
            }

            var cachedType = entry.Data == null ? "<null>" : entry.Data.GetType().FullName;
            throw CreateException(slot, entry.CurrentVersion, null, $"Cached data type '{cachedType}' does not match requested type '{typeof(T).FullName}'.");
        }

        private void SetEntry(Slot slot, Entry entry)
        {
            m_Entries.Remove(slot);
            m_Entries.Add(slot, entry);
        }

        private static GameException CreateException(Slot slot, string version, string path, string message, Exception innerException = null)
        {
            return new GameException($"{message} TypeKey='{slot.TypeKey}', DataKey='{slot.Key}', Version='{version ?? "<none>"}', Path='{path ?? "<none>"}'.", innerException);
        }
    }
}
