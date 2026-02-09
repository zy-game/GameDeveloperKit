namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 标签查询条件
    /// </summary>
    public class TagQuery
    {
        /// <summary>
        /// 必须包含的标签集合
        /// </summary>
        public TagContainer RequireTags { get; } = new();

        /// <summary>
        /// 必须排除的标签集合
        /// </summary>
        public TagContainer IgnoreTags { get; } = new();

        /// <summary>
        /// 必须包含任意的标签集合
        /// </summary>
        public TagContainer RequireAnyTags { get; } = new();

        /// <summary>
        /// 必须包含所有标签
        /// </summary>
        public TagQuery Require(params GameplayTag[] tags)
        {
            RequireTags.AddTags(tags);
            return this;
        }

        /// <summary>
        /// 必须不包含任何标签
        /// </summary>
        public TagQuery Ignore(params GameplayTag[] tags)
        {
            IgnoreTags.AddTags(tags);
            return this;
        }

        /// <summary>
        /// 必须包含任意一个标签
        /// </summary>
        public TagQuery RequireAny(params GameplayTag[] tags)
        {
            RequireAnyTags.AddTags(tags);
            return this;
        }

        /// <summary>
        /// 检查是否满足查询条件
        /// </summary>
        public bool Matches(TagContainer container)
        {
            if (container == null)
                return RequireTags.Count == 0 && RequireAnyTags.Count == 0;

            if (RequireTags.Count > 0 && !container.HasAllTags(RequireTags))
                return false;

            if (IgnoreTags.Count > 0 && container.HasAnyTag(IgnoreTags))
                return false;

            if (RequireAnyTags.Count > 0 && !container.HasAnyTag(RequireAnyTags))
                return false;

            return true;
        }

        /// <summary>
        /// 创建空查询
        /// </summary>
        public static TagQuery Create() => new();

        /// <summary>
        /// 创建必须包含指定标签的查询
        /// </summary>
        public static TagQuery MustHave(params GameplayTag[] tags)
        {
            return new TagQuery().Require(tags);
        }

        /// <summary>
        /// 创建必须不包含指定标签的查询
        /// </summary>
        public static TagQuery MustNotHave(params GameplayTag[] tags)
        {
            return new TagQuery().Ignore(tags);
        }
    }
}
