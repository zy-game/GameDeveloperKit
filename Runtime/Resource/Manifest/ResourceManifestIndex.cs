using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDeveloperKit.Resource
{
    internal sealed class ResourceManifestIndex
    {
        private readonly Dictionary<string, PackageNode> m_Packages;
        private readonly Dictionary<string, BundleNode> m_Bundles;
        private readonly Dictionary<string, string> m_LocationOwners;
        private readonly Dictionary<string, IReadOnlyList<string>> m_LabelBundles;
        private readonly Dictionary<string, IReadOnlyList<string>> m_TypeBundles;
        private readonly HashSet<string> m_RemoteBundleNames;

        public ResourceManifestIndex(
            ManifestInfo manifest,
            IEnumerable<string> remoteBundleNames = null)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            Version = manifest.Version;
            m_Packages = new Dictionary<string, PackageNode>(StringComparer.Ordinal);
            m_Bundles = new Dictionary<string, BundleNode>(StringComparer.Ordinal);
            m_LocationOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            m_RemoteBundleNames = new HashSet<string>(
                remoteBundleNames ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal);
            var labelBundles = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var typeBundles = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var packages = new List<KeyValuePair<string, string[]>>();
            foreach (var package in manifest.Packages ?? Enumerable.Empty<PackageInfo>())
            {
                if (package == null)
                {
                    continue;
                }

                var bundleNames = new List<string>();
                foreach (var bundle in package.Bundles ?? Enumerable.Empty<BundleInfo>())
                {
                    if (bundle == null)
                    {
                        continue;
                    }

                    var node = new BundleNode(bundle);
                    m_Bundles[bundle.Name] = node;
                    bundleNames.Add(bundle.Name);
                    foreach (var asset in node.Assets)
                    {
                        m_LocationOwners[asset.Location] = node.Name;
                        AddLookup(typeBundles, asset.TypeName, node.Name);
                        foreach (var label in asset.Labels)
                        {
                            AddLookup(labelBundles, label, node.Name);
                        }
                    }
                }

                packages.Add(new KeyValuePair<string, string[]>(package.Name, bundleNames.ToArray()));
            }

            foreach (var package in packages)
            {
                m_Packages.Add(
                    package.Key,
                    new PackageNode(package.Key, CreatePackageClosure(package.Value)));
            }

            m_LabelBundles = FreezeLookups(labelBundles);
            m_TypeBundles = FreezeLookups(typeBundles);
            PackageCount = m_Packages.Count;
            BundleCount = m_Bundles.Count;
            AssetCount = m_LocationOwners.Count;
        }

        public string Version { get; }

        public int PackageCount { get; }

        public int BundleCount { get; }

        public int AssetCount { get; }

        public bool ContainsPackage(string name)
        {
            return string.IsNullOrWhiteSpace(name) is false && m_Packages.ContainsKey(name);
        }

        public bool ContainsBundle(string name)
        {
            return string.IsNullOrWhiteSpace(name) is false && m_Bundles.ContainsKey(name);
        }

        public bool IsRemoteBundle(string bundleName)
        {
            return string.IsNullOrWhiteSpace(bundleName) is false && m_RemoteBundleNames.Contains(bundleName);
        }

        public IReadOnlyList<BundleInfo> CreateRemoteBundleSnapshot()
        {
            return m_RemoteBundleNames
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => m_Bundles[name].CreateSnapshot())
                .ToList()
                .AsReadOnly();
        }

        public bool TryGetAssetLocation(string location, out string bundleName)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                bundleName = null;
                return false;
            }

            return m_LocationOwners.TryGetValue(location, out bundleName);
        }

        public IReadOnlyList<string> GetBundleNamesByLabel(string label)
        {
            return GetLookup(m_LabelBundles, label);
        }

        public IReadOnlyList<string> GetBundleNamesByType(string typeName)
        {
            return GetLookup(m_TypeBundles, typeName);
        }

        public IReadOnlyList<BundleInfo> CreatePackageBundleSnapshot(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName) ||
                m_Packages.TryGetValue(packageName, out var package) is false)
            {
                return null;
            }

            return package.BundleNames
                .Select(bundleName => m_Bundles[bundleName].CreateSnapshot())
                .ToList()
                .AsReadOnly();
        }

        public BundleInfo CreateBundleSnapshot(string bundleName)
        {
            return string.IsNullOrWhiteSpace(bundleName) is false &&
                   m_Bundles.TryGetValue(bundleName, out var bundle)
                ? bundle.CreateSnapshot()
                : null;
        }

        private static void AddLookup(
            IDictionary<string, List<string>> lookup,
            string key,
            string bundleName)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (lookup.TryGetValue(key, out var bundleNames) is false)
            {
                bundleNames = new List<string>();
                lookup.Add(key, bundleNames);
            }

            if (bundleNames.Contains(bundleName) is false)
            {
                bundleNames.Add(bundleName);
            }
        }

        private string[] CreatePackageClosure(IEnumerable<string> directBundleNames)
        {
            var result = new List<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var bundleName in directBundleNames)
            {
                AddBundleWithDependencies(bundleName, visited, result);
            }

            return result.ToArray();
        }

        private void AddBundleWithDependencies(
            string bundleName,
            ISet<string> visited,
            ICollection<string> result)
        {
            if (visited.Add(bundleName) is false)
            {
                return;
            }

            var bundle = m_Bundles[bundleName];
            foreach (var dependency in bundle.Dependencies)
            {
                AddBundleWithDependencies(dependency, visited, result);
            }

            result.Add(bundleName);
        }

        private static Dictionary<string, IReadOnlyList<string>> FreezeLookups(
            IDictionary<string, List<string>> lookup)
        {
            return lookup.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value.AsReadOnly(),
                StringComparer.Ordinal);
        }

        private static IReadOnlyList<string> GetLookup(
            IReadOnlyDictionary<string, IReadOnlyList<string>> lookup,
            string key)
        {
            return string.IsNullOrWhiteSpace(key) is false && lookup.TryGetValue(key, out var values)
                ? values
                : Array.Empty<string>();
        }

        private sealed class PackageNode
        {
            public PackageNode(string name, string[] bundleNames)
            {
                Name = name;
                BundleNames = bundleNames;
            }

            public string Name { get; }

            public string[] BundleNames { get; }
        }

        private sealed class BundleNode
        {
            public BundleNode(BundleInfo bundle)
            {
                Name = bundle.Name;
                Hash = bundle.Hash;
                Size = bundle.Size;
                Crc = bundle.Crc;
                ProviderId = bundle.ProviderId;
                Dependencies = (bundle.Dependencies ?? new List<string>()).ToArray();
                Assets = (bundle.Assets ?? new List<AssetInfo>())
                    .Where(asset => asset != null)
                    .Select(asset => new AssetNode(asset))
                    .ToArray();
            }

            public string Name { get; }

            public string Hash { get; }

            public long Size { get; }

            public uint Crc { get; }

            public string ProviderId { get; }

            public string[] Dependencies { get; }

            public AssetNode[] Assets { get; }

            public BundleInfo CreateSnapshot()
            {
                return new BundleInfo
                {
                    Name = Name,
                    Hash = Hash,
                    Size = Size,
                    Crc = Crc,
                    ProviderId = ProviderId,
                    Dependencies = new List<string>(Dependencies),
                    Assets = Assets.Select(asset => asset.CreateSnapshot()).ToList()
                };
            }
        }

        private sealed class AssetNode
        {
            public AssetNode(AssetInfo asset)
            {
                Location = asset.Location;
                AssetPath = asset.AssetPath;
                TypeName = asset.TypeName;
                Labels = (asset.Labels ?? new List<string>()).ToArray();
            }

            public string Location { get; }

            public string AssetPath { get; }

            public string TypeName { get; }

            public string[] Labels { get; }

            public AssetInfo CreateSnapshot()
            {
                return new AssetInfo
                {
                    Location = Location,
                    AssetPath = AssetPath,
                    TypeName = TypeName,
                    Labels = new List<string>(Labels)
                };
            }
        }
    }
}
