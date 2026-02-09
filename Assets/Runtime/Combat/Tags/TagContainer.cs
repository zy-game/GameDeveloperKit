using System;
using System.Collections;
using System.Collections.Generic;

namespace GameDeveloperKit.Combat
{
    /// <summary>
    /// 标签容器，管理一组标签
    /// </summary>
    public class TagContainer : IEnumerable<GameplayTag>
    {
        private readonly HashSet<GameplayTag> _tags = new();

        /// <summary>
        /// 标签数量
        /// </summary>
        public int Count => _tags.Count;

        /// <summary>
        /// 标签添加事件
        /// </summary>
        public event Action<GameplayTag> OnTagAdded;

        /// <summary>
        /// 标签移除事件
        /// </summary>
        public event Action<GameplayTag> OnTagRemoved;

        /// <summary>
        /// 添加标签
        /// </summary>
        public void AddTag(GameplayTag tag)
        {
            if (!tag.IsValid) return;
            if (_tags.Add(tag))
            {
                OnTagAdded?.Invoke(tag);
            }
        }

        /// <summary>
        /// 添加多个标签
        /// </summary>
        public void AddTags(params GameplayTag[] tags)
        {
            foreach (var tag in tags)
            {
                AddTag(tag);
            }
        }

        /// <summary>
        /// 合并另一容器的标签
        /// </summary>
        public void AddTags(TagContainer other)
        {
            foreach (var tag in other._tags)
            {
                AddTag(tag);
            }
        }

        /// <summary>
        /// 移除标签
        /// </summary>
        public bool RemoveTag(GameplayTag tag)
        {
            if (_tags.Remove(tag))
            {
                OnTagRemoved?.Invoke(tag);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 移除多个标签
        /// </summary>
        public void RemoveTags(params GameplayTag[] tags)
        {
            foreach (var tag in tags)
            {
                RemoveTag(tag);
            }
        }

        /// <summary>
        /// 清空所有标签
        /// </summary>
        public void Clear()
        {
            var oldTags = new List<GameplayTag>(_tags);
            _tags.Clear();
            foreach (var tag in oldTags)
            {
                OnTagRemoved?.Invoke(tag);
            }
        }

        /// <summary>
        /// 精确包含标签
        /// </summary>
        public bool HasTagExact(GameplayTag tag)
        {
            return _tags.Contains(tag);
        }

        /// <summary>
        /// 包含标签（支持层级匹配）
        /// </summary>
        public bool HasTag(GameplayTag tag)
        {
            if (!tag.IsValid) return false;

            foreach (var t in _tags)
            {
                if (tag.Matches(t))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 包含任意标签
        /// </summary>
        public bool HasAnyTag(params GameplayTag[] tags)
        {
            foreach (var tag in tags)
            {
                if (HasTag(tag))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 包含任意标签
        /// </summary>
        public bool HasAnyTag(TagContainer other)
        {
            foreach (var tag in other._tags)
            {
                if (HasTag(tag))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 包含所有标签
        /// </summary>
        public bool HasAllTags(params GameplayTag[] tags)
        {
            foreach (var tag in tags)
            {
                if (!HasTag(tag))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 包含所有标签
        /// </summary>
        public bool HasAllTags(TagContainer other)
        {
            foreach (var tag in other._tags)
            {
                if (!HasTag(tag))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 不包含任何指定标签
        /// </summary>
        public bool HasNoneTags(params GameplayTag[] tags)
        {
            return !HasAnyTag(tags);
        }

        /// <summary>
        /// 不包含任何指定标签
        /// </summary>
        public bool HasNoneTags(TagContainer other)
        {
            return !HasAnyTag(other);
        }

        /// <summary>
        /// 获取枚举器
        /// </summary>
        public IEnumerator<GameplayTag> GetEnumerator() => _tags.GetEnumerator();

        /// <summary>
        /// 获取枚举器
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
