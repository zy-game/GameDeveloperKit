using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源清单工具类，提供资源清单的加载、保存和比较功能。
    /// </summary>
    public static class ResourceManifestUtility
    {
        /// <summary>
        /// 从文件加载资源清单。
        /// </summary>
        /// <param name="filePath">文件路径。</param>
        /// <returns>加载的资源清单，如果文件不存在或内容无效则返回null。</returns>
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

            var manifest = JsonUtility.FromJson<ResourceManifest>(json);
            if (manifest == null)
            {
                return null;
            }

            manifest.Packages ??= new List<ResourceManifestPackage>();
            for (var i = 0; i < manifest.Packages.Count; i++)
            {
                manifest.Packages[i] ??= new ResourceManifestPackage();
                manifest.Packages[i].Entries ??= new List<ResourceManifestEntry>();
            }

            return manifest;
        }

        /// <summary>
        /// 将资源清单转换为资源条目列表。
        /// </summary>
        /// <param name="manifest">资源清单。</param>
        /// <returns>资源条目列表。</returns>
        public static List<ResourceEntry> ToEntries(ResourceManifest manifest, string packageName = null)
        {
            var entries = new List<ResourceEntry>();
            var manifestEntries = ResolveEntries(manifest, packageName);
            if (manifestEntries == null)
            {
                return entries;
            }

            for (var i = 0; i < manifestEntries.Count; i++)
            {
                var manifestEntry = manifestEntries[i];
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
                    BundleName = manifestEntry.BundleName
                });
            }

            return entries;
        }

        /// <summary>
        /// 比较本地和远程资源清单，返回差异结果。
        /// </summary>
        /// <param name="localManifest">本地资源清单。</param>
        /// <param name="remoteManifest">远程资源清单。</param>
        /// <returns>资源清单比较结果，包含新增、修改和删除的条目。</returns>
        public static ResourceManifestComparisonResult Compare(ResourceManifest localManifest, ResourceManifest remoteManifest, string packageName = null)
        {
            var addedOrModified = new List<ResourceManifestEntry>();
            var removed = new List<ResourceManifestEntry>();

            var localMap = BuildMap(localManifest, packageName);
            var remoteMap = BuildMap(remoteManifest, packageName);

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
                LocalVersion = ResolvePackageVersion(localManifest, packageName),
                RemoteVersion = ResolvePackageVersion(remoteManifest, packageName)
            };
        }

        public static IReadOnlyList<ResourceManifestEntry> ResolveEntries(ResourceManifest manifest, string packageName = null)
        {
            var entries = ResolveEntriesInternal(manifest, packageName);
            return entries == null ? Array.Empty<ResourceManifestEntry>() : new List<ResourceManifestEntry>(entries);
        }

        /// <summary>
        /// 将资源清单保存到文件。
        /// </summary>
        /// <param name="manifest">要保存的资源清单。</param>
        /// <param name="filePath">目标文件路径。</param>
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

        private static List<ResourceManifestEntry> ResolveEntriesInternal(ResourceManifest manifest, string packageName)
        {
            if (manifest == null)
            {
                return null;
            }

            if (manifest.Packages == null || manifest.Packages.Count == 0)
            {
                return new List<ResourceManifestEntry>();
            }

            if (!string.IsNullOrWhiteSpace(packageName))
            {
                for (var i = 0; i < manifest.Packages.Count; i++)
                {
                    var package = manifest.Packages[i];
                    if (package == null || !string.Equals(package.Name, packageName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    return package.Entries ?? new List<ResourceManifestEntry>();
                }

                return new List<ResourceManifestEntry>();
            }

            var allEntries = new List<ResourceManifestEntry>();
            for (var i = 0; i < manifest.Packages.Count; i++)
            {
                var package = manifest.Packages[i];
                if (package?.Entries == null || package.Entries.Count == 0)
                {
                    continue;
                }

                allEntries.AddRange(package.Entries);
            }

            return allEntries;
        }

        private static string ResolvePackageVersion(ResourceManifest manifest, string packageName)
        {
            if (manifest == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(packageName) && manifest.Packages != null)
            {
                for (var i = 0; i < manifest.Packages.Count; i++)
                {
                    var package = manifest.Packages[i];
                    if (package != null && string.Equals(package.Name, packageName, StringComparison.Ordinal))
                    {
                        return string.IsNullOrWhiteSpace(package.Version) ? manifest.Version : package.Version;
                    }
                }
            }

            return manifest.Version;
        }

        private static Dictionary<string, ResourceManifestEntry> BuildMap(ResourceManifest manifest, string packageName)
        {
            var map = new Dictionary<string, ResourceManifestEntry>(StringComparer.Ordinal);
            var entries = ResolveEntriesInternal(manifest, packageName);
            if (entries == null)
            {
                return map;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                var key = ResolveEntryKey(entry);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                map[key] = entry;
            }

            return map;
        }

        private static string ResolveEntryKey(ResourceManifestEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(entry.FullPath))
            {
                return NormalizePath(entry.FullPath);
            }

            if (!string.IsNullOrWhiteSpace(entry.Name))
            {
                return entry.Name.Trim();
            }

            return string.Empty;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/');
        }
    }
}
