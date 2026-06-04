using System.Collections.Generic;

namespace GameDeveloperKit.Config
{
    public sealed class TagGroup
    {
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
