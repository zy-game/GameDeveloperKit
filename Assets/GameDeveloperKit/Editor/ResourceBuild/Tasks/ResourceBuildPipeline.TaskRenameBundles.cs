using System;
using System.Collections.Generic;
using System.IO;

namespace GameDeveloperKit.Editor
{
    internal sealed partial class ResourceBuildPipeline
    {
        private sealed class TaskRenameBundles : ISbpBuildTask
        {
            public string TaskName => "Rename Bundles";

            public ResourceBuildTaskResult Run(ResourceBuildPipelineContext context)
            {
                context.BundleOutputNameMap.Clear();
                var extension = context.Request.BundleExtension ?? string.Empty;
                var warnings = new List<string>();

                if (context.SbpBuildResults == null)
                {
                    return ResourceBuildTaskResult.Failed("SBP results are unavailable.");
                }

                foreach (var pair in context.SbpBuildResults.BundleInfos)
                {
                    var bundleName = pair.Key;
                    var sourceFileName = string.IsNullOrWhiteSpace(pair.Value.FileName)
                        ? bundleName
                        : Path.GetFileName(pair.Value.FileName);
                    var sourcePath = Path.Combine(context.BundleOutputRoot, sourceFileName);
                    if (!File.Exists(sourcePath))
                    {
                        var fallback = Path.Combine(context.BundleOutputRoot, bundleName);
                        if (File.Exists(fallback))
                        {
                            sourcePath = fallback;
                            sourceFileName = bundleName;
                        }
                    }

                    if (!File.Exists(sourcePath))
                    {
                        warnings.Add($"Bundle file not found for '{bundleName}'.");
                        continue;
                    }

                    var targetFileName = sourceFileName;
                    if (!string.IsNullOrWhiteSpace(extension) && !targetFileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    {
                        targetFileName += extension;
                    }

                    var targetPath = Path.Combine(context.BundleOutputRoot, targetFileName);
                    if (!string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(targetPath))
                        {
                            File.Delete(targetPath);
                        }

                        File.Move(sourcePath, targetPath);
                    }

                    context.BundleOutputNameMap[bundleName] = targetFileName;
                }

                if (context.BundleOutputNameMap.Count == 0)
                {
                    return ResourceBuildTaskResult.Failed("No bundle files were resolved after rename.", warnings);
                }

                context.Log($"Renamed/normalized bundles: {context.BundleOutputNameMap.Count}");
                return ResourceBuildTaskResult.Succeed(warnings);
            }
        }
    }
}
