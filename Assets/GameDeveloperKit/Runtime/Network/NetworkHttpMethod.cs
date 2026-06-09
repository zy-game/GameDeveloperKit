namespace GameDeveloperKit.Network
{
    /// <summary>
    /// HTTP 请求方法。
    /// </summary>
    public enum NetworkHttpMethod : byte
    {
        Get = 0,
        Post = 1,
        Put = 2,
        Patch = 3,
        Delete = 4,
        Head = 5,
    }
}
