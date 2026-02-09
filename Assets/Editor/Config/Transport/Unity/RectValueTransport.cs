using System;
using System.Globalization;

namespace GameDeveloperKit.Editor.Config
{
    /// <summary>
    /// Rect类型转换器
    /// 格式: "x,y,width,height" 例如 "0,0,100,200"
    /// </summary>
    public class RectValueTransport : IValueTransport
    {
        public string TypeName => "Rect";
        
        public bool CanHandle(string typeName)
        {
            return typeName == "Rect" || typeName == "rect";
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
            float width = ParseFloat(parts[2].Trim());
            float height = ParseFloat(parts[3].Trim());
            
            return $"{{\"x\": {FormatFloat(x)}, \"y\": {FormatFloat(y)}, \"width\": {FormatFloat(width)}, \"height\": {FormatFloat(height)}}}";
        }
        
        public string GetDefaultJson()
        {
            return "{\"x\": 0, \"y\": 0, \"width\": 0, \"height\": 0}";
        }
        
        public string GetCSharpType()
        {
            return "UnityEngine.Rect";
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
