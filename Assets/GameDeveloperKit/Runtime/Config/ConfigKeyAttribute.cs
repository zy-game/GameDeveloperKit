using System;

namespace GameDeveloperKit.Config
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class TableOptionAttribute : Attribute
    {
        public TableOptionAttribute(string path)
        {
            Path = path;
        }

        public string Path { get; }
    }
}
