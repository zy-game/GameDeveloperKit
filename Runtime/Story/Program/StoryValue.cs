using System;

namespace GameDeveloperKit.Story
{
    /// <summary>
    /// 剧情表达式和变量所使用的基础值类型。
    /// </summary>
    public enum StoryValueKind
    {
        /// <summary>
        /// 空值。
        /// </summary>
        Null = 0,

        /// <summary>
        /// 布尔值。
        /// </summary>
        Boolean = 1,

        /// <summary>
        /// 数字值。
        /// </summary>
        Number = 2,

        /// <summary>
        /// 字符串值。
        /// </summary>
        String = 3
    }

    /// <summary>
    /// 剧情运行时值。
    /// </summary>
    public readonly struct StoryValue : IEquatable<StoryValue>
    {
        /// <summary>
        /// 初始化剧情值。
        /// </summary>
        /// <param name="kind">值类型。</param>
        /// <param name="booleanValue">布尔值。</param>
        /// <param name="numberValue">数字值。</param>
        /// <param name="stringValue">字符串值。</param>
        private StoryValue(
            StoryValueKind kind,
            bool booleanValue,
            double numberValue,
            string stringValue)
        {
            Kind = kind;
            BooleanValue = booleanValue;
            NumberValue = numberValue;
            StringValue = stringValue;
        }

        /// <summary>
        /// 空值。
        /// </summary>
        public static StoryValue Null => default(StoryValue);

        /// <summary>
        /// 值类型。
        /// </summary>
        public StoryValueKind Kind { get; }

        /// <summary>
        /// 布尔值。
        /// </summary>
        public bool BooleanValue { get; }

        /// <summary>
        /// 数字值。
        /// </summary>
        public double NumberValue { get; }

        /// <summary>
        /// 字符串值。
        /// </summary>
        public string StringValue { get; }

        /// <summary>
        /// 是否为空值。
        /// </summary>
        public bool IsNull => Kind == StoryValueKind.Null;

        /// <summary>
        /// 是否为布尔值。
        /// </summary>
        public bool IsBoolean => Kind == StoryValueKind.Boolean;

        /// <summary>
        /// 是否为数字值。
        /// </summary>
        public bool IsNumber => Kind == StoryValueKind.Number;

        /// <summary>
        /// 是否为字符串值。
        /// </summary>
        public bool IsString => Kind == StoryValueKind.String;

        /// <summary>
        /// 创建布尔值。
        /// </summary>
        /// <param name="value">布尔值。</param>
        /// <returns>剧情值。</returns>
        public static StoryValue FromBoolean(bool value)
        {
            return new StoryValue(StoryValueKind.Boolean, value, 0d, null);
        }

        /// <summary>
        /// 创建数字值。
        /// </summary>
        /// <param name="value">数字值。</param>
        /// <returns>剧情值。</returns>
        public static StoryValue FromNumber(double value)
        {
            return new StoryValue(StoryValueKind.Number, false, value, null);
        }

        /// <summary>
        /// 创建字符串值。
        /// </summary>
        /// <param name="value">字符串值。</param>
        /// <returns>剧情值。</returns>
        public static StoryValue FromString(string value)
        {
            return new StoryValue(StoryValueKind.String, false, 0d, value);
        }

        /// <summary>
        /// 尝试读取布尔值。
        /// </summary>
        /// <param name="value">输出布尔值。</param>
        /// <returns>成功时返回 true。</returns>
        public bool TryGetBoolean(out bool value)
        {
            if (IsBoolean)
            {
                value = BooleanValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// 尝试读取数字值。
        /// </summary>
        /// <param name="value">输出数字值。</param>
        /// <returns>成功时返回 true。</returns>
        public bool TryGetNumber(out double value)
        {
            if (IsNumber)
            {
                value = NumberValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// 尝试读取字符串值。
        /// </summary>
        /// <param name="value">输出字符串值。</param>
        /// <returns>成功时返回 true。</returns>
        public bool TryGetString(out string value)
        {
            if (IsString)
            {
                value = StringValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// 转为字符串。
        /// </summary>
        /// <returns>字符串表示。</returns>
        public override string ToString()
        {
            switch (Kind)
            {
                case StoryValueKind.Boolean:
                    return BooleanValue ? "true" : "false";
                case StoryValueKind.Number:
                    return NumberValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case StoryValueKind.String:
                    return StringValue ?? string.Empty;
                default:
                    return "null";
            }
        }

        /// <summary>
        /// 判断是否相等。
        /// </summary>
        /// <param name="other">另一个剧情值。</param>
        /// <returns>相等时返回 true。</returns>
        public bool Equals(StoryValue other)
        {
            return Kind == other.Kind &&
                   BooleanValue == other.BooleanValue &&
                   NumberValue.Equals(other.NumberValue) &&
                   string.Equals(StringValue, other.StringValue, StringComparison.Ordinal);
        }

        /// <summary>
        /// 判断是否相等。
        /// </summary>
        /// <param name="obj">对象。</param>
        /// <returns>相等时返回 true。</returns>
        public override bool Equals(object obj)
        {
            return obj is StoryValue other && Equals(other);
        }

        /// <summary>
        /// 获取哈希码。
        /// </summary>
        /// <returns>哈希码。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ BooleanValue.GetHashCode();
                hash = (hash * 397) ^ NumberValue.GetHashCode();
                hash = (hash * 397) ^ (StringValue != null ? StringValue.GetHashCode() : 0);
                return hash;
            }
        }
    }
}
