using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Resource
{
    internal sealed class PackageSession
    {
        private readonly List<ProviderBase> m_Providers;

        public string PackageName { get; }
        public int ReferenceCount { get; private set; } = 1;
        public IReadOnlyList<ProviderBase> Providers => m_Providers;

        public PackageSession(string packageName, IEnumerable<ProviderBase> providers)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentException("Package name cannot be empty.", nameof(packageName));
            }

            PackageName = packageName;
            m_Providers = providers == null
                ? throw new ArgumentNullException(nameof(providers))
                : new List<ProviderBase>(providers);
        }

        public int Retain()
        {
            if (ReferenceCount <= 0)
            {
                throw new GameException($"Package session is already released: {PackageName}");
            }

            ReferenceCount++;
            return ReferenceCount;
        }

        public int ReleaseReference()
        {
            if (ReferenceCount <= 0)
            {
                return 0;
            }

            ReferenceCount--;
            return ReferenceCount;
        }
    }
}
