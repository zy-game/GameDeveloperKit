using System;
using System.Globalization;

namespace GameDeveloperKit.Editor.Config
{
    /// <summary>
    /// Color类型转换器
    /// 支持两种格式:
    /// 1. "r,g,b,a" 格式（0-1范围）例如 "1,0,0,1"
    /// 2. "#RRGGBBAA" 格式（十六进制）例如 "#FF0000FF"
    /// </summary>
    public class ColorValueTransport : IValueTransport
    {
        public string TypeName => "Color";
        
        public bool CanHandle(string typeName)
        {
            return typeName == "Color" || typeName == "color";
        }
        
        public string ToJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return GetDefaultJson();
            }
            
            var trimmed = value.Trim();
            
            // 十六进制格式
            if (trimmed.StartsWith("#"))
            {
                return ParseHexColor(trimmed);
            }
            
            // RGBA格式
            return ParseRgbaColor(trimmed);
        }
        
        public string GetDefaultJson()
        {
            return "{\"r\": 1, \"g\": 1, \"b\": 1, \"a\": 1}";
        }
        
        public string GetCSharpType()
        {
            return "UnityEngine.Color";
        }
        
        private static string ParseRgbaColor(string value)
        {
            var parts = value.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            
            float r = parts.Length > 0 ? ParseFloat(parts[0].Trim()) : 1f;
            float g = parts.Length > 1 ? ParseFloat(parts[1].Trim()) : 1f;
            float b = parts.Length > 2 ? ParseFloat(parts[2].Trim()) : 1f;
            float a = parts.Length > 3 ? ParseFloat(parts[3].Trim()) : 1f;
            
            // 如果值大于1，假设是0-255范围，需要转换为0-1
            if (r > 1f || g > 1f || b > 1f || a > 1f)
            {
                r = r / 255f;
                g = g / 255f;
                b = b / 255f;
                a = a / 255f;
            }
            
            return $"{{\"r\": {FormatFloat(r)}, \"g\": {FormatFloat(g)}, \"b\": {FormatFloat(b)}, \"a\": {FormatFloat(a)}}}";
        }
        
        private static string ParseHexColor(string hex)
        {
            // 移除#号
            hex = hex.TrimStart('#');
            
            // 补全为8位（RRGGBBAA）
            if (hex.Length == 6)
            {
                hex += "FF"; // 默认alpha=255
            }
            else if (hex.Length == 3)
            {
                // RGB -> RRGGBB
                hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}FF";
            }
            
            if (hex.Length != 8)
            {
                return "{\"r\": 1, \"g\": 1, \"b\": 1, \"a\": 1}";
            }
            
            try
            {
                int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                int a = Convert.ToInt32(hex.Substring(6, 2), 16);
                
                float rf = r / 255f;
                float gf = g / 255f;
                float bf = b / 255f;
                float af = a / 255f;
                
                return $"{{\"r\": {FormatFloat(rf)}, \"g\": {FormatFloat(gf)}, \"b\": {FormatFloat(bf)}, \"a\": {FormatFloat(af)}}}";
            }
            catch
            {
                return "{\"r\": 1, \"g\": 1, \"b\": 1, \"a\": 1}";
            }
        }
        
        private static float ParseFloat(string value)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            return 1f;
        }
        
        private static string FormatFloat(float value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
