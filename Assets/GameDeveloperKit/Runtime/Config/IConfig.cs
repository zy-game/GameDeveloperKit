namespace GameDeveloperKit.Config
{
    public sealed class Key
    {
        public Key(string name, object value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public object Value { get; }

        public bool Match(object value)
        {
            return Equals(Value, value);
        }
    }
    public interface IConfig
    {
        Key key { get; }
    }
}
