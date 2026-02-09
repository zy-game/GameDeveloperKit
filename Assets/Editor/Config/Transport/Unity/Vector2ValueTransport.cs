using System;
using System.Globalization;

namespace GameDeveloperKit.Editor.Config
{
    /// <summary>
    /// Vector2类型转换器
    /// 格式: "x,y" 例如 "100,200"
    /// </summary>
    public class Vector2ValueTransport : IValueTransport
    {
        public string TypeName => "Vector2";
        
        public bool CanHandle(string typeName)
        {
            return typeName == "Vector2" || typeName == "vector2";
        }
        
        public string ToJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return GetDefaultJson();
            }
            
            var parts = value.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return GetDefaultJson();
            }
            
            float x = ParseFloat(parts[0].Trim());
            float y = ParseFloat(parts[1].Trim());
            
            return $"{{\"x\": {FormatFloat(x)}, \"y\": {FormatFloat(y)}}}";
        }
        
        public string GetDefaultJson()
        {
            return "{\"x\": 0, \"y\": 0}";
        }
        
        public string GetCSharpType()
        {
            return "UnityEngine.Vector2";
        }
        
        private static float ParseFloat(string value)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            return 0f;
        }
        
        private static string FormatFloat(float value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
