namespace GameDeveloperKit.Logger
{
    public readonly struct ProfileColumn
    {
        public ProfileColumn(string key, string name = null)
        {
            Key = key;
            Name = string.IsNullOrWhiteSpace(name) ? key : name;
        }

        public string Key { get; }

        public string Name { get; }
    }
}
