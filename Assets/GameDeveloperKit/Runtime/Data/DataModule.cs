using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Data.Internal;
using GameDeveloperKit.Data.Serializers;

namespace GameDeveloperKit.Data
{
    public sealed partial class DataModule : GameModuleBase
    {
        private const int FormatVersion = 1;
        private const string IndexVersion = "index";
        private const string JsonSerializerFormat = "json";

        private readonly Dictionary<DataSlot, DataEntry> m_Entries = new Dictionary<DataSlot, DataEntry>();
        private IDataSerializer m_Serializer = new JsonDataSerializer();

        /// <summary>
        /// 启动 member。
        /// </summary>
        public override void Startup()
        {
            m_Entries.Clear();
            m_Serializer = new JsonDataSerializer();
        }

        /// <summary>
        /// 关闭 member。
        /// </summary>
        public override void Shutdown()
        {
            m_Entries.Clear();
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
            var slot = DataSlot.Create<T>(key);
            if (m_Entries.TryGetValue(slot, out var entry))
            {
                return GetEntryData<T>(slot, entry);
            }

            var data = CreateDefaultData<T>(slot);
            SetEntry(slot, new DataEntry(data));
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
            var slot = DataSlot.Create<T>(key);
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

            var slot = DataSlot.Create<T>(key);
            SetEntry(slot, new DataEntry(data));
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
    }
}
