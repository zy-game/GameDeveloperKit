using System;

namespace GameDeveloperKit.Event
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public sealed class BindingAttribute : Attribute
    {
        public BindingAttribute(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key cannot be empty.", nameof(key));
            }

            Key = key;
        }

        public string Key { get; }
    }
}
