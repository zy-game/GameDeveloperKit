using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameDeveloperKit.Editor
{
    internal sealed partial class ResourceBuildPipeline
    {
        private sealed class TaskUpdateBundleInfo : ISbpBuildTask
        {
            public string TaskName => "Update Bundle Info";

            public ResourceBuildTaskResult Run(ResourceBuildPipelineContext context)
            {
                var warnings = new List<string>();
                context.BuiltBundles.Clear();
                if (context.SbpBuildResults == null)
                {
                    return ResourceBuildTaskResult.Failed("SBP results are unavailable.");
                }

                foreach (var pair in context.SbpBuildResults.BundleInfos)
                {
                    var bundleName = pair.Key;
                    if (!context.BundleOutputNameMap.TryGetValue(bundleName, out var outputName))
                    {
                        warnings.Add($"Missing output file map for bundle '{bundleName}'.");
                        continue;
                    }

                    var outputPath = Path.Combine(context.BundleOutputRoot, outputName);
                    if (!File.Exists(outputPath))
                    {
                        warnings.Add($"Bundle file missing: {outputName}");
                        continue;
                    }

                    var deps = new List<string>();
                    if (pair.Value.Dependencies != null)
                    {
                        for (var i = 0; i < pair.Value.Dependencies.Length; i++)
                        {
                            var dep = pair.Value.Dependencies[i];
                            if (context.BundleOutputNameMap.TryGetValue(dep, out var depOutput))
                            {
                                deps.Add(Path.GetFileName(depOutput));
                            }
                            else
                            {
                                var extension = context.Request.BundleExtension ?? string.Empty;
                                var depName = Path.GetFileName(dep);
                                deps.Add(string.IsNullOrWhiteSpace(extension) || depName.EndsWith(extension, System.StringComparison.OrdinalIgnoreCase)
                                    ? depName
                                    : depName + extension);
                            }
                        }
                    }

                    var assets = context.BundleAssetMap.TryGetValue(bundleName, out var bundleAssets)
                        ? new List<string>(bundleAssets)
                        : new List<string>();

                    context.BuiltBundles.Add(new ResourceBuiltBundleRecord
                    {
                        BundleName = bundleName,
                        FileName = outputName,
                        Hash = pair.Value.Hash.ToString(),
                        SizeBytes = new FileInfo(outputPath).Length,
                        Dependencies = deps,
                        AssetPaths = assets
                    });
                }

                if (context.BuiltBundles.Count == 0)
                {
                    return ResourceBuildTaskResult.Failed("No valid built bundles were collected.", warnings);
                }

                var totalSize = context.BuiltBundles.Sum(static item => item.SizeBytes);
                context.Log($"Built bundles: {context.BuiltBundles.Count}, total size: {totalSize} bytes");
                return ResourceBuildTaskResult.Succeed(warnings);
            }
        }
    }
}
