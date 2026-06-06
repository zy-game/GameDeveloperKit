using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Data.Internal;
using GameDeveloperKit.Data.Serializers;
using GameDeveloperKit.File;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameDeveloperKit.Data
{
    public sealed class DataModule : GameModuleBase
    {
        private const int FormatVersion = 1;
        private const string IndexVersion = "index";
        private const string JsonSerializerFormat = "json";

        private readonly Dictionary<DataSlot, DataEntry> m_Entries = new Dictionary<DataSlot, DataEntry>();
        private IDataSerializer m_Serializer = new JsonDataSerializer();

        public override UniTask Startup()
        {
            m_Entries.Clear();
            m_Serializer = new JsonDataSerializer();
            return UniTask.CompletedTask;
        }

        public override UniTask Shutdown()
        {
            m_Entries.Clear();
            m_Serializer = new JsonDataSerializer();
            return UniTask.CompletedTask;
        }

        public T GetData<T>()
        {
            return GetData<T>(DataConstants.DefaultKey);
        }

        public T GetData<T>(string key)
        {
            var slot = DataSlot.Create<T>(key);
            if (m_Entries.TryGetValue(slot, out var entry))
            {
                return GetEntryData<T>(slot, entry);
            }

            var data = CreateDefaultData<T>(slot);
            SetEntry(slot, new DataEntry(data));
            return data;
        }

        public bool TryGetData<T>(out T data)
        {
            return TryGetData(DataConstants.DefaultKey, out data);
        }

        public bool TryGetData<T>(string key, out T data)
        {
            var slot = DataSlot.Create<T>(key);
            if (m_Entries.TryGetValue(slot, out var entry))
            {
                data = GetEntryData<T>(slot, entry);
                return true;
            }

            data = default;
            return false;
        }

        public void SetData<T>(T data)
        {
            SetData(DataConstants.DefaultKey, data);
        }

        public void SetData<T>(string key, T data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var slot = DataSlot.Create<T>(key);
            SetEntry(slot, new DataEntry(data));
        }

        public UniTask<T> LoadDataAsync<T>()
        {
            return LoadDataAsync<T>(DataConstants.DefaultKey);
        }

        public async UniTask<T> LoadDataAsync<T>(string key)
        {
            var slot = DataSlot.Create<T>(key);
            var fileModule = GetFileModule(slot, null, DataPathUtility.GetIndexPath(slot));
            var indexPath = DataPathUtility.GetIndexPath(slot);
            var index = await ReadIndexAsync(fileModule, slot, indexPath);
            if (index == null || string.IsNullOrEmpty(index.CurrentVersion))
            {
                var defaultData = CreateDefaultData<T>(slot);
                SetEntry(slot, new DataEntry(defaultData));
                return defaultData;
            }

            var versionPath = DataPathUtility.GetVersionPath(slot, index.CurrentVersion);
            var data = await ReadDocumentDataAsync<T>(fileModule, slot, index.CurrentVersion, versionPath);
            SetEntry(slot, new DataEntry(data, index.CurrentVersion));
            return data;
        }

        public UniTask<T> LoadVersionAsync<T>(string version)
        {
            return LoadVersionAsync<T>(DataConstants.DefaultKey, version);
        }

        public async UniTask<T> LoadVersionAsync<T>(string key, string version)
        {
            ValidateVersion(version);
            var slot = DataSlot.Create<T>(key);
            var versionPath = DataPathUtility.GetVersionPath(slot, version);
            var fileModule = GetFileModule(slot, version, versionPath);
            var data = await ReadDocumentDataAsync<T>(fileModule, slot, version, versionPath);
            SetEntry(slot, new DataEntry(data, version));
            return data;
        }

        public UniTask<DataVersionInfo> SaveDataAsync<T>()
        {
            return SaveDataAsync<T>(DataConstants.DefaultKey);
        }

        public UniTask<DataVersionInfo> SaveDataAsync<T>(string key)
        {
            var slot = DataSlot.Create<T>(key);
            if (!m_Entries.TryGetValue(slot, out var entry))
            {
                throw CreateException(slot, null, DataPathUtility.GetIndexPath(slot), "Data slot is not cached.");
            }

            return SaveSlotAsync(slot, entry, null);
        }

        public UniTask<DataVersionInfo> SaveDataAsync<T>(string key, string version)
        {
            ValidateVersion(version);
            var slot = DataSlot.Create<T>(key);
            if (!m_Entries.TryGetValue(slot, out var entry))
            {
                throw CreateException(slot, version, DataPathUtility.GetVersionPath(slot, version), "Data slot is not cached.");
            }

            return SaveSlotAsync(slot, entry, version);
        }

        public async UniTask SaveAllAsync()
        {
            var entries = new List<KeyValuePair<DataSlot, DataEntry>>(m_Entries);
            foreach (var pair in entries)
            {
                await SaveSlotAsync(pair.Key, pair.Value, null);
            }
        }

        public UniTask<T> RollbackDataAsync<T>(string version)
        {
            return RollbackDataAsync<T>(DataConstants.DefaultKey, version);
        }

        public async UniTask<T> RollbackDataAsync<T>(string key, string version)
        {
            ValidateVersion(version);
            var slot = DataSlot.Create<T>(key);
            var indexPath = DataPathUtility.GetIndexPath(slot);
            var versionPath = DataPathUtility.GetVersionPath(slot, version);
            var fileModule = GetFileModule(slot, version, versionPath);
            var index = await ReadRequiredIndexAsync(fileModule, slot, version, indexPath);
            if (!index.Versions.Any(info => info.Version == version))
            {
                throw CreateException(slot, version, versionPath, "Data version is not recorded in version index.");
            }

            var data = await ReadDocumentDataAsync<T>(fileModule, slot, version, versionPath);
            index.CurrentVersion = version;
            await WriteIndexAsync(fileModule, slot, index, indexPath);
            SetEntry(slot, new DataEntry(data, version));
            return data;
        }

        public UniTask<IReadOnlyList<DataVersionInfo>> GetVersionsAsync<T>()
        {
            return GetVersionsAsync<T>(DataConstants.DefaultKey);
        }

        public async UniTask<IReadOnlyList<DataVersionInfo>> GetVersionsAsync<T>(string key)
        {
            var slot = DataSlot.Create<T>(key);
            var indexPath = DataPathUtility.GetIndexPath(slot);
            var fileModule = GetFileModule(slot, null, indexPath);
            var index = await ReadIndexAsync(fileModule, slot, indexPath);
            if (index == null)
            {
                return Array.Empty<DataVersionInfo>();
            }

            return index.Versions
                .Select(info => new DataVersionInfo(info.Version, info.SavedAtUtc, info.Version == index.CurrentVersion))
                .ToArray();
        }

        public UniTask DeleteDataAsync<T>()
        {
            return DeleteDataAsync<T>(DataConstants.DefaultKey);
        }

        public async UniTask DeleteDataAsync<T>(string key)
        {
            var slot = DataSlot.Create<T>(key);
            m_Entries.Remove(slot);

            var indexPath = DataPathUtility.GetIndexPath(slot);
            var fileModule = GetFileModule(slot, null, indexPath);
            var index = await ReadIndexAsync(fileModule, slot, indexPath);
            if (index != null)
            {
                foreach (var info in index.Versions)
                {
                    await fileModule.DeleteAsync(DataPathUtility.GetVersionPath(slot, info.Version));
                }
            }

            await fileModule.DeleteAsync(indexPath);
        }

        public void SetSerializer(IDataSerializer serializer)
        {
            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            if (string.IsNullOrWhiteSpace(serializer.Format))
            {
                throw new ArgumentException("Data serializer format cannot be empty.", nameof(serializer));
            }

            m_Serializer = serializer;
        }

        private async UniTask<DataVersionInfo> SaveSlotAsync(DataSlot slot, DataEntry entry, string version)
        {
            var indexPath = DataPathUtility.GetIndexPath(slot);
            var fileModule = GetFileModule(slot, version, indexPath);
            var index = await ReadIndexAsync(fileModule, slot, indexPath) ?? CreateIndex(slot);
            var dataVersion = string.IsNullOrEmpty(version) ? GenerateVersion(index) : version;
            var versionPath = DataPathUtility.GetVersionPath(slot, dataVersion);
            if (index.Versions.Any(info => info.Version == dataVersion) || fileModule.Exists(versionPath))
            {
                throw CreateException(slot, dataVersion, versionPath, "Data version already exists.");
            }

            var savedAtUtc = DateTimeOffset.UtcNow;
            var documentBytes = CreateDocumentBytes(slot, entry.Data, dataVersion, savedAtUtc, versionPath);
            await fileModule.WriteAsync(versionPath, dataVersion, documentBytes);

            index.CurrentVersion = dataVersion;
            index.Versions.Add(new DataVersionInfo(dataVersion, savedAtUtc, true));
            await WriteIndexAsync(fileModule, slot, index, indexPath);

            entry.CurrentVersion = dataVersion;
            return new DataVersionInfo(dataVersion, savedAtUtc, true);
        }

        private static T CreateDefaultData<T>(DataSlot slot)
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

        private static DataVersionIndex CreateIndex(DataSlot slot)
        {
            return new DataVersionIndex
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

        private static FileModule GetFileModule(DataSlot slot, string version, string path)
        {
            if (App.TryGetRegistered<FileModule>(out var fileModule))
            {
                return fileModule;
            }

            throw CreateException(slot, version, path, "Data persistence requires registered FileModule.");
        }

        private static async UniTask<DataVersionIndex> ReadRequiredIndexAsync(FileModule fileModule, DataSlot slot, string version, string path)
        {
            var index = await ReadIndexAsync(fileModule, slot, path);
            if (index == null)
            {
                throw CreateException(slot, version, path, "Data version index does not exist.");
            }

            return index;
        }

        private static async UniTask<DataVersionIndex> ReadIndexAsync(FileModule fileModule, DataSlot slot, string path)
        {
            var bytes = await fileModule.ReadAsync(path);
            if (bytes == null)
            {
                return null;
            }

            try
            {
                var json = Encoding.UTF8.GetString(bytes);
                var index = JsonConvert.DeserializeObject<DataVersionIndex>(json);
                ValidateIndex(slot, index, path);
                return index;
            }
            catch (Exception exception) when (exception is not GameException)
            {
                throw CreateException(slot, null, path, "Failed to read data version index.", exception);
            }
        }

        private static void ValidateIndex(DataSlot slot, DataVersionIndex index, string path)
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
                index.Versions = new List<DataVersionInfo>();
            }
        }

        private static async UniTask WriteIndexAsync(FileModule fileModule, DataSlot slot, DataVersionIndex index, string path)
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

        private async UniTask<T> ReadDocumentDataAsync<T>(FileModule fileModule, DataSlot slot, string version, string path)
        {
            var bytes = await fileModule.ReadAsync(path);
            if (bytes == null)
            {
                throw CreateException(slot, version, path, "Data version document does not exist.");
            }

            try
            {
                var json = Encoding.UTF8.GetString(bytes);
                var document = JsonConvert.DeserializeObject<DataDocument>(json);
                ValidateDocument(slot, version, path, document);
                var payloadBytes = GetPayloadBytes(document, slot, version, path);
                var data = m_Serializer.Deserialize<T>(payloadBytes);
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

        private byte[] CreateDocumentBytes(DataSlot slot, object data, string version, DateTimeOffset savedAtUtc, string path)
        {
            try
            {
                var payloadBytes = SerializePayload(slot, data);
                var document = new DataDocument
                {
                    FormatVersion = FormatVersion,
                    Serializer = m_Serializer.Format,
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

        private byte[] SerializePayload(DataSlot slot, object data)
        {
            var method = typeof(IDataSerializer)
                .GetMethod(nameof(IDataSerializer.Serialize))
                .MakeGenericMethod(slot.Type);

            try
            {
                return (byte[])method.Invoke(m_Serializer, new[] { data });
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw exception.InnerException;
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

        private byte[] GetPayloadBytes(DataDocument document, DataSlot slot, string version, string path)
        {
            if (document.Payload == null)
            {
                throw CreateException(slot, version, path, "Data version document payload is missing.");
            }

            if (m_Serializer.Format == JsonSerializerFormat)
            {
                return Encoding.UTF8.GetBytes(document.Payload.ToString(Formatting.None));
            }

            if (document.Payload.Type != JTokenType.String)
            {
                throw CreateException(slot, version, path, "Data version document payload is not encoded for the active serializer.");
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

        private void ValidateDocument(DataSlot slot, string version, string path, DataDocument document)
        {
            if (document == null)
            {
                throw CreateException(slot, version, path, "Data version document is empty.");
            }

            if (document.FormatVersion != FormatVersion)
            {
                throw CreateException(slot, version, path, "Unsupported data version document format.");
            }

            if (document.Serializer != m_Serializer.Format)
            {
                throw CreateException(slot, version, path, "Data version document serializer does not match active serializer.");
            }

            if (document.TypeKey != slot.TypeKey || document.Key != slot.Key || document.DataVersion != version)
            {
                throw CreateException(slot, version, path, "Data version document type key, data key or version does not match requested slot.");
            }
        }

        private static string GenerateVersion(DataVersionIndex index)
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

        private static T GetEntryData<T>(DataSlot slot, DataEntry entry)
        {
            if (entry.Data is T data)
            {
                return data;
            }

            var cachedType = entry.Data == null ? "<null>" : entry.Data.GetType().FullName;
            throw CreateException(slot, entry.CurrentVersion, null, $"Cached data type '{cachedType}' does not match requested type '{typeof(T).FullName}'.");
        }

        private void SetEntry(DataSlot slot, DataEntry entry)
        {
            m_Entries.Remove(slot);
            m_Entries.Add(slot, entry);
        }

        private static GameException CreateException(DataSlot slot, string version, string path, string message, Exception innerException = null)
        {
            return new GameException($"{message} TypeKey='{slot.TypeKey}', DataKey='{slot.Key}', Version='{version ?? "<none>"}', Path='{path ?? "<none>"}'.", innerException);
        }
    }
}
