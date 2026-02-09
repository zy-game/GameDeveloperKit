using System;

namespace GameDeveloperKit.Editor.Config
{
    /// <summary>
    /// 整数类型转换器（支持 int 和 long）
    /// </summary>
    public class IntValueTransport : IValueTransport
    {
        public string TypeName => "int";

        public bool CanHandle(string typeName)
        {
            var lower = typeName?.ToLower();
            return lower == "int" || lower == "long" || lower == "int32" || lower == "int64" 
                || lower == "short" || lower == "int16" || lower == "byte" || lower == "sbyte"
                || lower == "uint" || lower == "uint32" || lower == "uint64" || lower == "uint16";
        }

        public string ToJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "0";
            }

            if (long.TryParse(value.Trim(), out var result))
            {
                return result.ToString();
            }

            return "0";
        }

        public string GetDefaultJson()
        {
            return "0";
        }

        public string GetCSharpType()
        {
            return "int";
        }
    }
}
