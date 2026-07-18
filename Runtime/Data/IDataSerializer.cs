using System;

namespace GameDeveloperKit.Data
{
    public interface IDataSerializer
    {
        string Format { get; }

        byte[] Serialize<T>(T data);

        T Deserialize<T>(byte[] bytes);

        byte[] Serialize(Type type, object data);

        object Deserialize(Type type, byte[] bytes);
    }

    public sealed class DataMigrationPayload
    {
        private readonly byte[] m_Bytes;

        public DataMigrationPayload(string serializer, byte[] bytes)
        {
            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            if (string.IsNullOrWhiteSpace(serializer))
            {
                throw new ArgumentException("Data migration serializer cannot be empty.", nameof(serializer));
            }

            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            Serializer = serializer;
            m_Bytes = (byte[])bytes.Clone();
        }

        public string Serializer { get; }

        public byte[] Bytes => (byte[])m_Bytes.Clone();

        internal byte[] GetBytes()
        {
            return m_Bytes;
        }
    }

    public interface IDataMigration
    {
        int FromVersion { get; }

        int ToVersion { get; }

        DataMigrationPayload Migrate(DataMigrationPayload payload);
    }
}
