using System;

namespace GameDeveloperKit.Config
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ConfigKeyAttribute : Attribute
    {
    }
}
