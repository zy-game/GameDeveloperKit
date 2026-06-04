namespace GameDeveloperKit.Logger
{
    public readonly struct CrashLogArtifact
    {
        public CrashLogArtifact(string name, string content, bool isAvailable = true)
        {
            Name = name;
            Content = content;
            IsAvailable = isAvailable;
        }

        public string Name { get; }

        public string Content { get; }

        public bool IsAvailable { get; }
    }
}
