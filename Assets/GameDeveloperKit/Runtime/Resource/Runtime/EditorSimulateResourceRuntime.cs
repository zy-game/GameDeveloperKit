#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 编辑器模拟资源运行时，在Unity编辑器中模拟资源加载。
    /// </summary>
    public sealed class EditorSimulateResourceRuntime : ResourceRuntimeBase
    {
        /// <summary>
        /// 从编辑器加载资源。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="entry">资源条目。</param>
        /// <returns>加载的Unity对象。</returns>
        public override UnityObject LoadAsset(ResourcePackageContext context, ResourceEntry entry)
        {
            var editorPath = ResolveEditorAssetPath(context, entry);
            if (!string.IsNullOrWhiteSpace(editorPath))
            {
                if (entry.AssetType != null)
                {
                    var loaded = AssetDatabase.LoadAssetAtPath(editorPath, entry.AssetType);
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
                else
                {
                    var loaded = AssetDatabase.LoadAssetAtPath<UnityObject>(editorPath);
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
            }

            return base.LoadAsset(context, entry);
        }

        /// <summary>
        /// 解析场景路径。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="entry">资源条目。</param>
        /// <returns>场景路径。</returns>
        public override string ResolveScenePath(ResourcePackageContext context, ResourceEntry entry)
        {
            return ResolveEditorAssetPath(context, entry);
        }

        /// <summary>
        /// 解析文件路径。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="entry">资源条目。</param>
        /// <returns>文件路径。</returns>
        public override string ResolveFilePath(ResourcePackageContext context, ResourceEntry entry)
        {
            return ResolveEditorAssetPath(context, entry);
        }

        /// <summary>
        /// 构建资源条目列表。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <returns>资源条目列表。</returns>
        public override IReadOnlyList<ResourceEntry> BuildEntries(ResourcePackageContext context)
        {
            var manifestEntries = LoadManifestEntries(context);
            if (manifestEntries.Count > 0)
            {
                return manifestEntries;
            }

            var results = new List<ResourceEntry>();
            var searchRoots = context?.Definition?.SimulateSearchRoots;
            if (searchRoots != null)
            {
                for (var i = 0; i < searchRoots.Count; i++)
                {
                    CollectEntriesFromRoot(searchRoots[i], results);
                }
            }

            if (results.Count > 0)
            {
                return results;
            }

            return base.BuildEntries(context);
        }

        /// <summary>
        /// 解析编辑器中的资源路径。
        /// </summary>
        /// <param name="context">资源包上下文。</param>
        /// <param name="entry">资源条目。</param>
        /// <returns>编辑器中的资源路径。</returns>
        private static string ResolveEditorAssetPath(ResourcePackageContext context, ResourceEntry entry)
        {
            var candidate = entry?.FullPath;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                candidate = entry?.Name;
            }

            if (string.IsNullOrWhiteSpace(candidate))
            {
                return string.Empty;
            }

            if (candidate.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                return candidate.Replace('\\', '/');
            }

            var streamingRoot = context?.StreamingAssetsRoot;
            if (!string.IsNullOrWhiteSpace(streamingRoot))
            {
                var combined = Path.Combine(streamingRoot, candidate).Replace('\\', '/');
                if (combined.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                {
                    return combined;
                }
            }

            return candidate.Replace('\\', '/');
        }

        /// <summary>
        /// 从指定根目录收集资源条目。
        /// </summary>
        /// <param name="root">搜索根目录。</param>
        /// <param name="results">结果列表。</param>
        private static void CollectEntriesFromRoot(string root, List<ResourceEntry> results)
        {
            if (results == null || string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            var normalizedRoot = root.Replace('\\', '/');
            var guids = AssetDatabase.FindAssets(string.Empty, new[] { normalizedRoot });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                var mainType = AssetDatabase.GetMainAssetTypeAtPath(path);
                results.Add(new ResourceEntry
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    FullPath = path,
                    AssetType = mainType,
                    Kind = ResolveKind(path, mainType)
                });
            }
        }

        /// <summary>
        /// 根据路径和类型解析资源条目类型。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <param name="mainType">资源类型。</param>
        /// <returns>资源条目类型。</returns>
        private static ResourceEntryKind ResolveKind(string path, Type mainType)
        {
            if (string.Equals(Path.GetExtension(path), ".unity", StringComparison.OrdinalIgnoreCase))
            {
                return ResourceEntryKind.Scene;
            }

            if (typeof(UnityObject).IsAssignableFrom(mainType))
            {
                return ResourceEntryKind.Asset;
            }

            return ResourceEntryKind.RawFile;
        }
    }
}
#endif
