using System;
using System.Globalization;

namespace GameDeveloperKit.Editor.Config
{
    /// <summary>
    /// 浮点数类型转换器（支持 float 和 double）
    /// </summary>
    public class FloatValueTransport : IValueTransport
    {
        public string TypeName => "float";
        
        public bool CanHandle(string typeName)
        {
            var lower = typeName?.ToLower();
            return lower == "float" || lower == "double" || lower == "single" || lower == "decimal";
        }
        
        public string ToJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "0";
            }
            
            if (double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                return result.ToString(CultureInfo.InvariantCulture);
            }
            
            return "0";
        }
        
        public string GetDefaultJson()
        {
            return "0";
        }
        
        public string GetCSharpType()
        {
            return "float";
        }
    }
}
