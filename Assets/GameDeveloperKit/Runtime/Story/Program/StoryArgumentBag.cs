using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情命令参数袋。
    /// </summary>
    public sealed class StoryArgumentBag
    {
        /// <summary>
        /// 初始化空参数袋。
        /// </summary>
        public StoryArgumentBag()
            : this(null)
        {
        }

        /// <summary>
        /// 初始化参数袋。
        /// </summary>
        /// <param name="values">参数值。</param>
        public StoryArgumentBag(IReadOnlyDictionary<string, StoryValue> values)
        {
            Values = CopyValues(values);
        }

        /// <summary>
        /// 参数键值。
        /// </summary>
        public IReadOnlyDictionary<string, StoryValue> Values { get; }

        /// <summary>
        /// 尝试读取指定参数。
        /// </summary>
        /// <param name="key">参数键。</param>
        /// <param name="value">参数值。</param>
        /// <returns>找到时返回 true。</returns>
        public bool TryGetValue(string key, out StoryValue value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = default;
                return false;
            }

            return Values.TryGetValue(key, out value);
        }

        /// <summary>
        /// 获取指定参数，缺失时返回 fallback。
        /// </summary>
        /// <param name="key">参数键。</param>
        /// <param name="fallback">默认值。</param>
        /// <returns>参数值。</returns>
        public StoryValue GetValue(string key, StoryValue fallback = default(StoryValue))
        {
            return TryGetValue(key, out var value) ? value : fallback;
        }

        /// <summary>
        /// 获取字符串参数。
        /// </summary>
        /// <param name="key">参数键。</param>
        /// <param name="fallback">默认值。</param>
        /// <returns>字符串值。</returns>
        public string GetString(string key, string fallback = null)
        {
            if (TryGetValue(key, out var value) && value.TryGetString(out var stringValue))
            {
                return stringValue;
            }

            return fallback;
        }

        /// <summary>
        /// 获取布尔参数。
        /// </summary>
        /// <param name="key">参数键。</param>
        /// <param name="fallback">默认值。</param>
        /// <returns>布尔值。</returns>
        public bool GetBoolean(string key, bool fallback = false)
        {
            if (TryGetValue(key, out var value) && value.TryGetBoolean(out var booleanValue))
            {
                return booleanValue;
            }

            return fallback;
        }

        /// <summary>
        /// 获取数字参数。
        /// </summary>
        /// <param name="key">参数键。</param>
        /// <param name="fallback">默认值。</param>
        /// <returns>数字值。</returns>
        public double GetNumber(string key, double fallback = 0d)
        {
            if (TryGetValue(key, out var value) && value.TryGetNumber(out var numberValue))
            {
                return numberValue;
            }

            return fallback;
        }

        private static IReadOnlyDictionary<string, StoryValue> CopyValues(IReadOnlyDictionary<string, StoryValue> values)
        {
            if (values == null || values.Count == 0)
            {
                return new Dictionary<string, StoryValue>(0, StringComparer.Ordinal);
            }

            var copy = new Dictionary<string, StoryValue>(StringComparer.Ordinal);
            foreach (var pair in values)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                copy[pair.Key] = pair.Value;
            }

            return copy;
        }
    }
}
