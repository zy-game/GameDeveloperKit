using System;
using Newtonsoft.Json;

namespace GameDeveloperKit.Story.Text
{
    public enum TextMode
    {
        Literal = 0,
        LocalizationKey = 1
    }

    public readonly struct TextReference
    {
        public TextReference(TextMode mode, string value)
        {
            if (Enum.IsDefined(typeof(TextMode), mode) is false)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Text reference value cannot be empty.", nameof(value));
            }

            Mode = mode;
            Value = value;
        }

        public TextMode Mode { get; }

        public string Value { get; }
    }

    public interface ITextResolver
    {
        string Resolve(TextReference reference);
    }

    public static class TextReferenceCodec
    {
        private const int CurrentVersion = 1;

        public static string Serialize(TextReference reference)
        {
            return JsonConvert.SerializeObject(new TextReferenceData
            {
                Version = CurrentVersion,
                Mode = reference.Mode == TextMode.Literal ? "literal" : "localization_key",
                Value = reference.Value
            });
        }

        public static bool TryDeserialize(string value, out TextReference reference, out bool legacy, out string error)
        {
            reference = default;
            legacy = false;
            error = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                error = "Text reference cannot be empty.";
                return false;
            }

            if (value.TrimStart().StartsWith("{", StringComparison.Ordinal) is false)
            {
                reference = new TextReference(TextMode.LocalizationKey, value);
                legacy = true;
                return true;
            }

            try
            {
                var data = JsonConvert.DeserializeObject<TextReferenceData>(value);
                if (data == null || data.Version != CurrentVersion)
                {
                    error = "Text reference version is invalid or unsupported.";
                    return false;
                }

                TextMode mode;
                if (string.Equals(data.Mode, "literal", StringComparison.Ordinal)) mode = TextMode.Literal;
                else if (string.Equals(data.Mode, "localization_key", StringComparison.Ordinal)) mode = TextMode.LocalizationKey;
                else
                {
                    error = "Text reference mode is invalid.";
                    return false;
                }

                reference = new TextReference(mode, data.Value);
                return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }

        public static TextReference DeserializeOrLegacy(string value)
        {
            if (TryDeserialize(value, out var reference, out _, out var error))
            {
                return reference;
            }

            throw new ArgumentException(error, nameof(value));
        }

        [Serializable]
        private sealed class TextReferenceData
        {
            [JsonProperty("version", Order = 0)] public int Version { get; set; }
            [JsonProperty("mode", Order = 1)] public string Mode { get; set; }
            [JsonProperty("value", Order = 2)] public string Value { get; set; }
        }
    }
}

namespace GameDeveloperKit.Story.Model
{
    /// <summary>
    /// 剧情表达式和变量所使用的基础值类型。
    /// </summary>
    public enum ValueKind
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
    public readonly struct Value : IEquatable<Value>
    {
        /// <summary>
        /// 初始化剧情值。
        /// </summary>
        /// <param name="kind">值类型。</param>
        /// <param name="booleanValue">布尔值。</param>
        /// <param name="numberValue">数字值。</param>
        /// <param name="stringValue">字符串值。</param>
        private Value(
            ValueKind kind,
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
        public static Value Null => default(Value);

        /// <summary>
        /// 值类型。
        /// </summary>
        public ValueKind Kind { get; }

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
        public bool IsNull => Kind == ValueKind.Null;

        /// <summary>
        /// 是否为布尔值。
        /// </summary>
        public bool IsBoolean => Kind == ValueKind.Boolean;

        /// <summary>
        /// 是否为数字值。
        /// </summary>
        public bool IsNumber => Kind == ValueKind.Number;

        /// <summary>
        /// 是否为字符串值。
        /// </summary>
        public bool IsString => Kind == ValueKind.String;

        /// <summary>
        /// 创建布尔值。
        /// </summary>
        /// <param name="value">布尔值。</param>
        /// <returns>剧情值。</returns>
        public static Value FromBoolean(bool value)
        {
            return new Value(ValueKind.Boolean, value, 0d, null);
        }

        /// <summary>
        /// 创建数字值。
        /// </summary>
        /// <param name="value">数字值。</param>
        /// <returns>剧情值。</returns>
        public static Value FromNumber(double value)
        {
            return new Value(ValueKind.Number, false, value, null);
        }

        /// <summary>
        /// 创建字符串值。
        /// </summary>
        /// <param name="value">字符串值。</param>
        /// <returns>剧情值。</returns>
        public static Value FromString(string value)
        {
            return new Value(ValueKind.String, false, 0d, value);
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
                case ValueKind.Boolean:
                    return BooleanValue ? "true" : "false";
                case ValueKind.Number:
                    return NumberValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case ValueKind.String:
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
        public bool Equals(Value other)
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
            return obj is Value other && Equals(other);
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
