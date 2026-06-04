namespace GameDeveloperKit.Data.Internal
{
    internal sealed class DataEntry
    {
        public DataEntry(object data, string currentVersion = null)
        {
            Data = data;
            CurrentVersion = currentVersion;
        }

        public object Data { get; set; }

        public string CurrentVersion { get; set; }
    }
}
