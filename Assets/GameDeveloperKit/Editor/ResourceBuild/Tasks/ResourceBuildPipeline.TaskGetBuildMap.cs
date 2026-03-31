using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameDeveloperKit.Editor
{
    internal sealed partial class ResourceBuildPipeline
    {
        private sealed class TaskGetBuildMap : ISbpBuildTask
        {
            public string TaskName => "Get Build Map";

            public ResourceBuildTaskResult Run(ResourceBuildPipelineContext context)
            {
                var package = context.Request.Package;
                var roots = package.CollectRoots == null || package.CollectRoots.Count == 0
                    ? new[] { "Assets" }
                    : package.CollectRoots.Where(static root => !string.IsNullOrWhiteSpace(root)).Select(NormalizeAssetPath).ToArray();

                var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                var warnings = new List<string>();
                for (var i = 0; i < context.CollectedEntries.Count; i++)
                {
                    var entry = context.CollectedEntries[i];
                    var assetPath = ResolveAssetPath(entry);
                    if (string.IsNullOrWhiteSpace(assetPath))
                    {
                        warnings.Add($"Skip non-asset entry at index {i}: {entry?.Name ?? "<null>"}");
                        continue;
                    }

                    var bundleName = ResolveBundleName(package, roots, entry, assetPath, i);
                    if (!groups.TryGetValue(bundleName, out var assets))
                    {
                        assets = new List<string>();
                        groups.Add(bundleName, assets);
                    }

                    if (!assets.Any(path => string.Equals(path, assetPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        assets.Add(assetPath);
                    }
                }

                if (groups.Count == 0)
                {
                    return ResourceBuildTaskResult.Failed("Build map is empty after grouping entries.", warnings);
                }

                context.BundleAssetMap.Clear();
                context.AssetBundleBuilds.Clear();
                foreach (var pair in groups.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                {
                    var assetNames = pair.Value.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase).ToArray();
                    context.BundleAssetMap[pair.Key] = new List<string>(assetNames);
                    context.AssetBundleBuilds.Add(new UnityEditor.AssetBundleBuild
                    {
                        assetBundleName = pair.Key,
                        assetNames = assetNames,
                        addressableNames = assetNames.Select(Path.GetFileNameWithoutExtension).ToArray()
                    });
                }

                context.Log($"Bundle groups: {context.AssetBundleBuilds.Count}");
                return ResourceBuildTaskResult.Succeed(warnings);
            }
        }
    }
}
