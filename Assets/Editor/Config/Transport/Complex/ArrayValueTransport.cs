using System;
using System.Text;

namespace GameDeveloperKit.Editor.Config
{
    /// <summary>
    /// 数组类型转换器
    /// 委托给元素类型的Transport进行转换
    /// </summary>
    public class ArrayValueTransport : IValueTransport
    {
        private readonly IValueTransport _elementTransport;
        private readonly string _arrayTypeName;
        
        public string TypeName => _arrayTypeName;
        
        public ArrayValueTransport(IValueTransport elementTransport, string arrayTypeName)
        {
            _elementTransport = elementTransport ?? throw new ArgumentNullException(nameof(elementTransport));
            _arrayTypeName = arrayTypeName ?? throw new ArgumentNullException(nameof(arrayTypeName));
        }
        
        public bool CanHandle(string typeName)
        {
            return typeName == _arrayTypeName;
        }
        
        public string ToJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return GetDefaultJson();
            }
            
            // 支持多种分隔符
            var elements = value.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (elements.Length == 0)
            {
                return GetDefaultJson();
            }
            
            var sb = new StringBuilder("[");
            for (int i = 0; i < elements.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }
                
                var element = elements[i].Trim();
                sb.Append(_elementTransport.ToJson(element));
            }
            sb.Append("]");
            
            return sb.ToString();
        }
        
        public string GetDefaultJson()
        {
            return "[]";
        }
        
        public string GetCSharpType()
        {
            return $"{_elementTransport.GetCSharpType()}[]";
        }
    }
}
