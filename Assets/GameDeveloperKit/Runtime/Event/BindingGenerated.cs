using System;

namespace GameDeveloperKit.Event
{
    internal static partial class BindingGenerated
    {
        internal static void RegisterAll(EventModule module)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            RegisterAllGenerated(module);
        }

        static partial void RegisterAllGenerated(EventModule module);
    }
}
