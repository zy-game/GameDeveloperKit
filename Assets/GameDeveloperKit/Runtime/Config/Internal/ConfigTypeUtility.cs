using System;

namespace GameDeveloperKit.Config.Internal
{
    internal static class ConfigTypeUtility
    {
        public static Type ResolveRowType(ConfigSourceDefinition source)
        {
            var type = Type.GetType(source.RowTypeName);
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(source.RowTypeName);
                if (type != null)
                {
                    return type;
                }
            }

            throw new GameException(
                $"Config source '{source.Name}' row type '{source.RowTypeName}' cannot be resolved.");
        }
    }
}
