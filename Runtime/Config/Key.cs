namespace GameDeveloperKit.Config
{
    public sealed class Key
    {
        /// <summary>
        /// 初始化 Key。
        /// </summary>
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
        public bool Match(object value)
        {
            return Equals(Value, value);
        }
    }
}
