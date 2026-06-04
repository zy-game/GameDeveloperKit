using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDeveloperKit.ResourcePublisher
{
    public static class ObjectStorageProviderRegistry
    {
        private static readonly Dictionary<string, IObjectStorageProvider> s_Providers = new Dictionary<string, IObjectStorageProvider>(StringComparer.Ordinal);
        private static bool s_Initialized;

        public static IReadOnlyList<IObjectStorageProvider> Providers
        {
            get
            {
                EnsureInitialized();
                return s_Providers.Values
                    .OrderBy(x => x.DisplayName, StringComparer.Ordinal)
                    .ToList();
            }
        }

        public static void Register(IObjectStorageProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            var platformId = provider.PlatformId;
            if (string.IsNullOrWhiteSpace(platformId))
            {
                throw new ArgumentException("Provider platform id cannot be empty.", nameof(provider));
            }

            s_Providers[platformId] = provider;
        }

        public static bool TryGetProvider(string platformId, out IObjectStorageProvider provider)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(platformId))
            {
                provider = null;
                return false;
            }

            return s_Providers.TryGetValue(platformId, out provider);
        }

        public static IObjectStorageProvider GetProviderOrFallback(string platformId)
        {
            EnsureInitialized();
            if (TryGetProvider(platformId, out var provider))
            {
                return provider;
            }

            return new UnavailableObjectStorageProvider(platformId, platformId);
        }

        private static void EnsureInitialized()
        {
            if (s_Initialized)
            {
                return;
            }

            s_Initialized = true;
            Register(new CosObjectStorageProvider());
        }
    }
}
