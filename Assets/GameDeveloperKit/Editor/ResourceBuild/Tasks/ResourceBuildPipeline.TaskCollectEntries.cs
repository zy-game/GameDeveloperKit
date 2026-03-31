using System.Collections.Generic;
using System.Linq;
using GameDeveloperKit.Runtime;

namespace GameDeveloperKit.Editor
{
    internal sealed partial class ResourceBuildPipeline
    {
        private sealed class TaskCollectEntries : ISbpBuildTask
        {
            public string TaskName => "Collect Entries";

            public ResourceBuildTaskResult Run(ResourceBuildPipelineContext context)
            {
                var package = context.Request.Package;
                List<ResourceEntry> entries;
                if (package.CollectionStrategy == ResourcePackageCollectionStrategy.ManualEntries)
                {
                    entries = package.Entries == null
                        ? new List<ResourceEntry>()
                        : package.Entries.Where(static item => item != null).ToList();
                }
                else
                {
                    entries = ResourceCollectionService.BuildCollectedEntries(package);
                }

                if (entries.Count == 0)
                {
                    return ResourceBuildTaskResult.Failed($"Package '{package.PackageName}' collected zero entries.");
                }

                context.CollectedEntries = entries;
                package.Entries = new List<ResourceEntry>(entries);
                context.Log($"Collected entries: {entries.Count}");
                return ResourceBuildTaskResult.Succeed();
            }
        }
    }
}
