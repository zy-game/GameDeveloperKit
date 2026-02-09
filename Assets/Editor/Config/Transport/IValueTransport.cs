namespace GameDeveloperKit.Editor.Config
{
    /// <summary>
    /// 值转换器接口 - 负责将字符串值转换为目标类型的JSON表示
    /// </summary>
    public interface IValueTransport
    {
        /// <summary>
        /// 支持的类型名称（如 "int", "Vector2", "CustomClass" 等）
        /// </summary>
        string TypeName { get; }
        
        /// <summary>
        /// 是否支持该类型
        /// </summary>
        bool CanHandle(string typeName);
        
        /// <summary>
        /// 转换字符串值为JSON格式
        /// </summary>
        string ToJson(string value);
        
        /// <summary>
        /// 获取该类型的默认JSON值
        /// </summary>
        string GetDefaultJson();
        
        /// <summary>
        /// 获取该类型的C#代码表示
        /// </summary>
        string GetCSharpType();
    }
}
