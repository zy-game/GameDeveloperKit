namespace GameDeveloperKit.Config
{
    /// <summary>
    /// 定义 Key 类型。
    /// </summary>
    public sealed class Key
    {
        /// <summary>
        /// 初始化 Key。
        /// </summary>
        /// <param name="name">name 参数。</param>
        /// <param name="value">value 参数。</param>
        public Key(string name, object value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public object Value { get; }

        /// <summary>
        /// 执行 Match。
        /// </summary>
        /// <param name="value">value 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        public bool Match(object value)
        {
            return Equals(Value, value);
        }
    }
    /// <summary>
    /// 定义 I Config 接口。
    /// </summary>
    public interface IConfig
    {
        Key key { get; }
    }
}
