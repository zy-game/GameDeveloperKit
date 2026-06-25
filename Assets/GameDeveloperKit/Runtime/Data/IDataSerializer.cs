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
}
