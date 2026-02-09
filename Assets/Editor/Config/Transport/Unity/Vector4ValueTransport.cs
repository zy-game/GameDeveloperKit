using System;
using System.Globalization;

namespace GameDeveloperKit.Editor.Config
{
    /// <summary>
    /// Vector4类型转换器
    /// 格式: "x,y,z,w" 例如 "1,2,3,4"
    /// </summary>
    public class Vector4ValueTransport : IValueTransport
    {
        public string TypeName => "Vector4";
        
        public bool CanHandle(string typeName)
        {
            return typeName == "Vector4" || typeName == "vector4";
        }
        
        public string ToJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return GetDefaultJson();
            }
            
            var parts = value.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
            {
                return GetDefaultJson();
            }
            
            float x = ParseFloat(parts[0].Trim());
            float y = ParseFloat(parts[1].Trim());
            float z = ParseFloat(parts[2].Trim());
            float w = ParseFloat(parts[3].Trim());
            
            return $"{{\"x\": {FormatFloat(x)}, \"y\": {FormatFloat(y)}, \"z\": {FormatFloat(z)}, \"w\": {FormatFloat(w)}}}";
        }
        
        public string GetDefaultJson()
        {
            return "{\"x\": 0, \"y\": 0, \"z\": 0, \"w\": 0}";
        }
        
        public string GetCSharpType()
        {
            return "UnityEngine.Vector4";
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
