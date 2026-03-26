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

            return JsonUtility.FromJson<ResourceManifest>(json);
        }

        /// <summary>
        /// 将资源清单转换为资源条目列表。
        /// </summary>
        /// <param name="manifest">资源清单。</param>
        /// <returns>资源条目列表。</returns>
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

        /// <summary>
        /// 比较本地和远程资源清单，返回差异结果。
        /// </summary>
        /// <param name="localManifest">本地资源清单。</param>
        /// <param name="remoteManifest">远程资源清单。</param>
        /// <returns>资源清单比较结果，包含新增、修改和删除的条目。</returns>
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
