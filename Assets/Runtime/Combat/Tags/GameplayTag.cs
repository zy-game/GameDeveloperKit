using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 游戏标签，用于标记状态、类型等
    /// 支持层级结构，如 "State.Buff.Invincible"
    /// </summary>
    public readonly struct GameplayTag : IEquatable<GameplayTag>
    {
        public static readonly GameplayTag None = default;

        private static readonly Dictionary<string, GameplayTag> TagCache = new();
        private static readonly Dictionary<int, string> IdToName = new();
        private static int _nextId = 1;

        /// <summary>
        /// 标签唯一ID
        /// </summary>
        public readonly int Id;

        /// <summary>
        /// 标签名称
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// 标签是否有效
        /// </summary>
        public bool IsValid => Id > 0;

        private GameplayTag(int id, string name)
        {
            Id = id;
            Name = name;
        }

        /// <summary>
        /// 获取或创建标签
        /// </summary>
        public static GameplayTag Get(string tagName)
        {
            if (string.IsNullOrEmpty(tagName))
                return None;

            if (TagCache.TryGetValue(tagName, out var tag))
                return tag;

            tag = new GameplayTag(_nextId++, tagName);
            TagCache[tagName] = tag;
            IdToName[tag.Id] = tagName;
            return tag;
        }

        /// <summary>
        /// 尝试获取标签（不创建）
        /// </summary>
        public static bool TryGet(string tagName, out GameplayTag tag)
        {
            if (string.IsNullOrEmpty(tagName))
            {
                tag = None;
                return false;
            }
            return TagCache.TryGetValue(tagName, out tag);
        }

        /// <summary>
        /// 检查是否匹配（支持层级匹配）
        /// "State.Buff" 匹配 "State.Buff.Invincible"
        /// </summary>
        public bool Matches(GameplayTag other)
        {
            if (!IsValid || !other.IsValid)
                return false;

            if (Id == other.Id)
                return true;

            return other.Name.StartsWith(Name + ".", StringComparison.Ordinal);
        }

        /// <summary>
        /// 精确匹配
        /// </summary>
        public bool MatchesExact(GameplayTag other)
        {
            return Id == other.Id;
        }

        /// <summary>
        /// 获取父标签
        /// </summary>
        public GameplayTag GetParent()
        {
            if (!IsValid)
                return None;

            int lastDot = Name.LastIndexOf('.');
            if (lastDot <= 0)
                return None;

            return Get(Name.Substring(0, lastDot));
        }

        public bool Equals(GameplayTag other) => Id == other.Id;
        public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);
        public override int GetHashCode() => Id;
        public override string ToString() => Name ?? "None";

        public static bool operator ==(GameplayTag left, GameplayTag right) => left.Id == right.Id;
        public static bool operator !=(GameplayTag left, GameplayTag right) => left.Id != right.Id;

        public static implicit operator GameplayTag(string tagName) => Get(tagName);
    }
}
