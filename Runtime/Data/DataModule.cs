using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Data.Internal;
using GameDeveloperKit.Data.Serializers;

namespace GameDeveloperKit.Data
{
    public sealed partial class DataModule : GameModuleBase
    {
        private const int FormatVersion = 2;
        private const int MaxRetainedVersions = 10;
        private const string IndexVersion = "index";
        private const string JsonSerializerFormat = "json";

        private readonly Dictionary<Slot, Entry> m_Entries = new Dictionary<Slot, Entry>();
        private readonly Dictionary<string, SortedDictionary<int, IDataMigration>> m_Migrations = new Dictionary<string, SortedDictionary<int, IDataMigration>>(StringComparer.Ordinal);
        private readonly SemaphoreSlim m_PersistenceMutationGate = new SemaphoreSlim(1, 1);
        private IDataSerializer m_Serializer = new JsonDataSerializer();

        /// <summary>
        /// 启动 member。
        /// </summary>
        public override void Startup()
        {
            m_Entries.Clear();
            m_Migrations.Clear();
            m_Serializer = new JsonDataSerializer();
        }

        /// <summary>
        /// 关闭 member。
        /// </summary>
        public override void Shutdown()
        {
            m_Entries.Clear();
            m_Migrations.Clear();
            m_Serializer = new JsonDataSerializer();
        }

        /// <summary>
        /// 获取 Data。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public T GetData<T>()
        {
            return GetData<T>(DataConstants.DefaultKey);
        }

        /// <summary>
        /// 获取 Data。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public T GetData<T>(string key)
        {
            var slot = Slot.Create<T>(key);
            if (m_Entries.TryGetValue(slot, out var entry))
            {
                return GetEntryData<T>(slot, entry);
            }

            var data = CreateDefaultData<T>(slot);
            SetEntry(slot, new Entry(data));
            return data;
        }

        /// <summary>
        /// 尝试获取 Data。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public bool TryGetData<T>(out T data)
        {
            return TryGetData(DataConstants.DefaultKey, out data);
        }

        /// <summary>
        /// 尝试获取 Data。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public bool TryGetData<T>(string key, out T data)
        {
            var slot = Slot.Create<T>(key);
            if (m_Entries.TryGetValue(slot, out var entry))
            {
                data = GetEntryData<T>(slot, entry);
                return true;
            }

            data = default;
            return false;
        }

        /// <summary>
        /// 设置 Data。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public void SetData<T>(T data)
        {
            SetData(DataConstants.DefaultKey, data);
        }

        /// <summary>
        /// 设置 Data。
        /// </summary>
        /// <typeparam name="T">泛型类型参数。</typeparam>
        public void SetData<T>(string key, T data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var slot = Slot.Create<T>(key);
            SetEntry(slot, new Entry(data));
        }

        /// <summary>
        /// 设置 Serializer。
        /// </summary>
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

        public void RegisterMigration<T>(IDataMigration migration)
        {
            if (migration == null)
            {
                throw new ArgumentNullException(nameof(migration));
            }

            var slot = Slot.Create<T>(DataConstants.DefaultKey);
            ValidatePersistenceContract(slot);
            if (migration.FromVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(migration), "Data migration source version must be greater than zero.");
            }

            if (migration.ToVersion != migration.FromVersion + 1)
            {
                throw new ArgumentException("Data migrations must advance exactly one schema version.", nameof(migration));
            }

            if (migration.ToVersion > slot.SchemaVersion)
            {
                throw new ArgumentException($"Data migration target version '{migration.ToVersion}' exceeds schema '{slot.SchemaVersion}'.", nameof(migration));
            }

            if (!m_Migrations.TryGetValue(slot.TypeKey, out var migrations))
            {
                migrations = new SortedDictionary<int, IDataMigration>();
                m_Migrations.Add(slot.TypeKey, migrations);
            }

            if (migrations.ContainsKey(migration.FromVersion))
            {
                throw new ArgumentException($"A data migration from schema '{migration.FromVersion}' is already registered for '{slot.TypeKey}'.", nameof(migration));
            }

            migrations.Add(migration.FromVersion, migration);
        }

        private static void ValidatePersistenceContract(Slot slot)
        {
            if (!slot.HasStableTypeKey)
            {
                throw CreateException(slot, null, null, $"Persisted data type '{slot.Type.FullName}' must declare DataKeyAttribute.");
            }

            if (slot.SchemaVersion < 1)
            {
                throw CreateException(slot, null, null, $"Persisted data type '{slot.Type.FullName}' must declare DataSchemaAttribute.");
            }
        }
    }
}
