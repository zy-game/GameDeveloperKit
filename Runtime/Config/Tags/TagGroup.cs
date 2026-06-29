using System.Collections.Generic;

namespace GameDeveloperKit.Config
{
    public sealed class TagGroup
    {
        /// <summary>
        /// 初始化 Tag Group。
        /// </summary>
        /// <param name="displayName">display Name 参数。</param>
        /// <param name="isFixed">is Fixed 参数。</param>
        public TagGroup(string key, string displayName, bool isFixed, IReadOnlyList<TagDefinition> tags)
        {
            Key = key;
            DisplayName = displayName;
            Fixed = isFixed;
            Tags = tags ?? new List<TagDefinition>().AsReadOnly();
        }

        public string Key { get; }

        public string DisplayName { get; }

        public bool Fixed { get; }

        public IReadOnlyList<TagDefinition> Tags { get; }
    }
}
