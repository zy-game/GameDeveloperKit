using System;

namespace GameDeveloperKit.Editor.Config
{
    /// <summary>
    /// JSON对象类型转换器
    /// 用于处理自定义类，Excel中直接填写JSON字符串
    /// </summary>
    public class JsonObjectTransport : IValueTransport
    {
        private readonly string _typeName;
        
        public string TypeName => _typeName ?? "object";
        
        public JsonObjectTransport(string typeName = null)
        {
            _typeName = typeName;
        }
        
        public bool CanHandle(string typeName)
        {
            return _typeName == null || typeName == _typeName;
        }
        
        public string ToJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return GetDefaultJson();
            }
            
            var trimmed = value.Trim();
            
            // 如果已经是JSON格式（以{或[开头），直接返回
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            {
                return trimmed;
            }
            
            // 否则作为字符串处理
            return $"\"{EscapeJson(trimmed)}\"";
        }
        
        public string GetDefaultJson()
        {
            return "{}";
        }
        
        public string GetCSharpType()
        {
            return _typeName ?? "object";
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
