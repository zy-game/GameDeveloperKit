namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// HTTP请求方法枚举。
    /// </summary>
    public enum NetworkMethod
    {
        /// <summary>
        /// GET请求方法，用于获取资源。
        /// </summary>
        Get = 0,
        /// <summary>
        /// POST请求方法，用于创建资源。
        /// </summary>
        Post = 1,
        /// <summary>
        /// PUT请求方法，用于更新资源。
        /// </summary>
        Put = 2,
        /// <summary>
        /// DELETE请求方法，用于删除资源。
        /// </summary>
        Delete = 3,
        /// <summary>
        /// PATCH请求方法，用于部分更新资源。
        /// </summary>
        Patch = 4,
        /// <summary>
        /// HEAD请求方法，用于获取资源头部信息。
        /// </summary>
        Head = 5
    }
}
