using System;

namespace GameDeveloperKit.Editor.Config
{
    /// <summary>
    /// 布尔类型转换器
    /// </summary>
    public class BoolValueTransport : IValueTransport
    {
        public string TypeName => "bool";
        
        public bool CanHandle(string typeName)
        {
            var lower = typeName?.ToLower();
            return lower == "bool" || lower == "boolean";
        }
        
        public string ToJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "false";
            }
            
            var trimmed = value.Trim().ToLower();
            return trimmed == "true" || trimmed == "1" ? "true" : "false";
        }
        
        public string GetDefaultJson()
        {
            return "false";
        }
        
        public string GetCSharpType()
        {
            return "bool";
        }
    }
}
