namespace GameDeveloperKit.Data.Internal
{
    /// <summary>
    /// 定义 Data Entry 类型。
    /// </summary>
    internal sealed class DataEntry
    {
        /// <summary>
        /// 初始化 Data Entry。
        /// </summary>
        /// <param name="data">data 参数。</param>
        /// <param name="currentVersion">current Version 参数。</param>
        public DataEntry(object data, string currentVersion = null)
        {
            Data = data;
            CurrentVersion = currentVersion;
        }

        public object Data { get; set; }

        public string CurrentVersion { get; set; }
    }
}
