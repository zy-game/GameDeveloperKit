using System;
using System.Globalization;

namespace GameDeveloperKit.Editor.Config
{
    /// <summary>
    /// Vector3类型转换器
    /// 格式: "x,y,z" 例如 "100,200,300"
    /// </summary>
    public class Vector3ValueTransport : IValueTransport
    {
        public string TypeName => "Vector3";
        
        public bool CanHandle(string typeName)
        {
            return typeName == "Vector3" || typeName == "vector3";
        }
        
        public string ToJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return GetDefaultJson();
            }
            
            var parts = value.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                return GetDefaultJson();
            }
            
            float x = ParseFloat(parts[0].Trim());
            float y = ParseFloat(parts[1].Trim());
            float z = ParseFloat(parts[2].Trim());
            
            return $"{{\"x\": {FormatFloat(x)}, \"y\": {FormatFloat(y)}, \"z\": {FormatFloat(z)}}}";
        }
        
        public string GetDefaultJson()
        {
            return "{\"x\": 0, \"y\": 0, \"z\": 0}";
        }
        
        public string GetCSharpType()
        {
            return "UnityEngine.Vector3";
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
