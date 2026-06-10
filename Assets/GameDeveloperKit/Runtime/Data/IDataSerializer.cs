namespace GameDeveloperKit.Data
{
    /// <summary>
    /// 定义 I Data Serializer 接口。
    /// </summary>
    public interface IDataSerializer
    {
        string Format { get; }

        byte[] Serialize<T>(T data);

        T Deserialize<T>(byte[] bytes);
    }
}
