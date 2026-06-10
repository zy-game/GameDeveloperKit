namespace GameDeveloperKit.Logger
{
    /// <summary>
    /// 定义 Debug Log Query 结构。
    /// </summary>
    public readonly struct DebugLogQuery
    {
        /// <summary>
        /// 初始化 Debug Log Query。
        /// </summary>
        /// <param name="level">level 参数。</param>
        /// <param name="category">category 参数。</param>
        public DebugLogQuery(LogLevel? level = null, string category = null)
        {
            Level = level;
            Category = category;
        }

        public LogLevel? Level { get; }

        public string Category { get; }

        /// <summary>
        /// 执行 Matches。
        /// </summary>
        /// <param name="entry">entry 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        public bool Matches(DebugLogRecord entry)
        {
            if (Level.HasValue && entry.Level != Level.Value)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(Category) && entry.Category != Category)
            {
                return false;
            }

            return true;
        }
    }
}
