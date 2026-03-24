#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    public sealed class EditorSimulateResourceRuntime : ResourceRuntimeBase
    {
        public override Object LoadAsset(ResourcePackageContext context, ResourceEntry entry)
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
                    var loaded = AssetDatabase.LoadAssetAtPath<Object>(editorPath);
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
            }

            return base.LoadAsset(context, entry);
        }

        public override string ResolveScenePath(ResourcePackageContext context, ResourceEntry entry)
        {
            return ResolveEditorAssetPath(context, entry);
        }

        public override string ResolveFilePath(ResourcePackageContext context, ResourceEntry entry)
        {
            return ResolveEditorAssetPath(context, entry);
        }

        public override IReadOnlyList<ResourceEntry> BuildEntries(ResourcePackageContext context)
        {
            return base.BuildEntries(context);
        }

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
    }
}
#endif
