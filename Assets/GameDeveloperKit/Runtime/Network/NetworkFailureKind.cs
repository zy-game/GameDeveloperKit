namespace GameDeveloperKit.Network
{
    /// <summary>
    /// 网络失败分类。
    /// </summary>
    public enum NetworkFailureKind : byte
    {
        None = 0,
        Connection = 1,
        Send = 2,
        Receive = 3,
        Timeout = 4,
        Decode = 5,
        HttpStatus = 6,
        InvalidResponse = 7,
        Canceled = 8,
    }
}
