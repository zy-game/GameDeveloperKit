namespace GameDeveloperKit.Debugger
{
    public readonly struct DebugLogQuery
    {
        /// <summary>
        /// 初始化 Debug Log Query。
        /// </summary>
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
