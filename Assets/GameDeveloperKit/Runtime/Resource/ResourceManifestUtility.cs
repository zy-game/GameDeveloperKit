using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    public static class ResourceManifestUtility
    {
        public static ResourceManifest LoadFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonUtility.FromJson<ResourceManifest>(json);
        }

        public static List<ResourceEntry> ToEntries(ResourceManifest manifest)
        {
            var entries = new List<ResourceEntry>();
            if (manifest?.Entries == null)
            {
                return entries;
            }

            for (var i = 0; i < manifest.Entries.Count; i++)
            {
                var manifestEntry = manifest.Entries[i];
                if (manifestEntry == null)
                {
                    continue;
                }

                entries.Add(new ResourceEntry
                {
                    Name = manifestEntry.Name,
                    Version = manifestEntry.Version,
                    Hash = manifestEntry.Hash,
                    SizeBytes = manifestEntry.SizeBytes,
                    AssetType = ResolveType(manifestEntry.AssetType),
                    Labels = manifestEntry.Labels == null ? null : new List<string>(manifestEntry.Labels),
                    Dependencies = manifestEntry.Dependencies == null ? null : new List<string>(manifestEntry.Dependencies),
                    FullPath = manifestEntry.FullPath,
                    Kind = manifestEntry.Kind
                });
            }

            return entries;
        }

        public static ResourceManifestComparisonResult Compare(ResourceManifest localManifest, ResourceManifest remoteManifest)
        {
            var addedOrModified = new List<ResourceManifestEntry>();
            var removed = new List<ResourceManifestEntry>();

            var localMap = BuildMap(localManifest);
            var remoteMap = BuildMap(remoteManifest);

            foreach (var pair in remoteMap)
            {
                if (!localMap.TryGetValue(pair.Key, out var localEntry))
                {
                    addedOrModified.Add(pair.Value);
                    continue;
                }

                if (!string.Equals(localEntry.Hash, pair.Value.Hash, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(localEntry.Version, pair.Value.Version, StringComparison.OrdinalIgnoreCase) ||
                    localEntry.SizeBytes != pair.Value.SizeBytes)
                {
                    addedOrModified.Add(pair.Value);
                }
            }

            foreach (var pair in localMap)
            {
                if (!remoteMap.ContainsKey(pair.Key))
                {
                    removed.Add(pair.Value);
                }
            }

            return new ResourceManifestComparisonResult
            {
                IsChanged = addedOrModified.Count > 0 || removed.Count > 0,
                AddedOrModifiedEntries = addedOrModified,
                RemovedEntries = removed,
                LocalVersion = localManifest?.Version,
                RemoteVersion = remoteManifest?.Version
            };
        }

        public static void SaveToFile(ResourceManifest manifest, string filePath)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, JsonUtility.ToJson(manifest, true));
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            return Type.GetType(typeName, false);
        }

        private static Dictionary<string, ResourceManifestEntry> BuildMap(ResourceManifest manifest)
        {
            var map = new Dictionary<string, ResourceManifestEntry>(StringComparer.Ordinal);
            if (manifest?.Entries == null)
            {
                return map;
            }

            for (var i = 0; i < manifest.Entries.Count; i++)
            {
                var entry = manifest.Entries[i];
                if (entry == null)
                {
                    continue;
                }

                var key = entry.Name ?? entry.FullPath ?? string.Empty;
                map[key] = entry;
            }

            return map;
        }
    }
}
