using System;
using System.Reflection;

namespace GameDeveloperKit.Runtime
{
    internal static class EventBindingScanner
    {
        internal static void ScanAndRegister(EventModule module)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            var providerType = typeof(IEventBindingProvider);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var types = GetAssemblyTypes(assemblies[i]);
                for (var j = 0; j < types.Length; j++)
                {
                    var type = types[j];
                    if (type == null || type.IsAbstract || type.IsInterface || !providerType.IsAssignableFrom(type))
                    {
                        continue;
                    }

                    if (Activator.CreateInstance(type) is IEventBindingProvider provider)
                    {
                        provider.Register(module);
                    }
                }
            }
        }

        private static Type[] GetAssemblyTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types ?? Array.Empty<Type>();
            }
        }
    }
}
