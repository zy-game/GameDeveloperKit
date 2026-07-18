namespace GameDeveloperKit.Data.Internal
{
    internal sealed class Entry
    {
        /// <summary>
        /// 初始化 Data Entry。
        /// </summary>
        /// <param name="currentVersion">current Version 参数。</param>
        public Entry(object data, string currentVersion = null)
        {
            Data = data;
            CurrentVersion = currentVersion;
        }

        public object Data { get; set; }

        public string CurrentVersion { get; set; }
    }
}
