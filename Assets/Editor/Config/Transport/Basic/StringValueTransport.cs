using System;

namespace GameDeveloperKit.Editor.Config
{
    /// <summary>
    /// 字符串类型转换器
    /// </summary>
    public class StringValueTransport : IValueTransport
    {
        public string TypeName => "string";
        
        public bool CanHandle(string typeName)
        {
            var lower = typeName?.ToLower();
            return lower == "string" || lower == "text";
        }
        
        public string ToJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }
            
            return $"\"{EscapeJson(value)}\"";
        }
        
        public string GetDefaultJson()
        {
            return "\"\"";
        }
        
        public string GetCSharpType()
        {
            return "string";
        }
        
        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
