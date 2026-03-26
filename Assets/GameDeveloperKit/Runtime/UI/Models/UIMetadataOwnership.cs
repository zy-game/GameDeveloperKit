namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// UI元数据所有权类，定义UI窗口属性的归属（代码还是预制体）。
    /// </summary>
    public static class UIMetadataOwnership
    {
        /// <summary>
        /// 获取由代码拥有的字段列表。
        /// </summary>
        public static readonly string[] CodeOwnedFields =
        {
            nameof(UIWindowAttribute.Layer),
            nameof(UIWindowAttribute.Mode),
            nameof(UIWindowAttribute.ToStack),
            nameof(UIWindowAttribute.SortingOrder)
        };

        /// <summary>
        /// 获取由预制体拥有的字段列表。
        /// </summary>
        public static readonly string[] PrefabOwnedFields =
        {
            nameof(UIDocument.Bindings),
            nameof(UIDocument.FullScreenBackground),
            nameof(UIDocument.Generation)
        };

        /// <summary>
        /// 获取代码拥有字段的摘要字符串。
        /// </summary>
        /// <returns>代码拥有字段的逗号分隔字符串。</returns>
        public static string GetCodeOwnedSummary()
        {
            return string.Join(", ", CodeOwnedFields);
        }

        /// <summary>
        /// 获取预制体拥有字段的摘要字符串。
        /// </summary>
        /// <returns>预制体拥有字段的逗号分隔字符串。</returns>
        public static string GetPrefabOwnedSummary()
        {
            return string.Join(", ", PrefabOwnedFields);
        }
    }
}
