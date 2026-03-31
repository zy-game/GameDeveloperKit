using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDeveloperKit.Editor
{
    internal sealed partial class ResourceBuildPipeline : ISBP_Build
    {
        private readonly IReadOnlyList<ISbpBuildTask> _tasks;

        public ResourceBuildPipeline()
            : this(null)
        {
        }

        public ResourceBuildPipeline(IEnumerable<ISbpBuildTask> tasks)
        {
            _tasks = tasks == null ? CreateDefaultTasks() : tasks.ToArray();
        }

        public ResourceBuildPipelineReport Build(ResourceBuildPipelineRequest request)
        {
            var context = new ResourceBuildPipelineContext(request)
            {
                BuildStartUtc = DateTime.UtcNow
            };

            foreach (var task in _tasks)
            {
                context.Log($">>> Task: {task.TaskName}");
                var result = task.Run(context);
                if (result.Warnings.Count > 0)
                {
                    for (var i = 0; i < result.Warnings.Count; i++)
                    {
                        context.LogWarning(result.Warnings[i]);
                    }
                }

                if (!result.Success)
                {
                    context.BuildEndUtc = DateTime.UtcNow;
                    context.LogError($"Task failed: {task.TaskName}");
                    context.LogError(result.ErrorMessage);
                    WriteExecutionReport(context, false, result.ErrorMessage);
                    return new ResourceBuildPipelineReport
                    {
                        Success = false,
                        ErrorMessage = result.ErrorMessage,
                        BundleCount = context.BuiltBundles.Count,
                        EntryCount = ResolveEntryCount(context),
                        OutputRoot = context.BuildRoot
                    };
                }

                context.Log($"<<< Task done: {task.TaskName}");
            }

            context.BuildEndUtc = DateTime.UtcNow;
            return new ResourceBuildPipelineReport
            {
                Success = true,
                BundleCount = context.BuiltBundles.Count,
                EntryCount = ResolveEntryCount(context),
                OutputRoot = context.BuildRoot
            };
        }

        public ResourceBuildPipelineReport Run(ResourceBuildPipelineRequest request)
        {
            return Build(request);
        }

        private static IReadOnlyList<ISbpBuildTask> CreateDefaultTasks()
        {
            return new ISbpBuildTask[]
            {
                new TaskPrepare(),
                new TaskCollectEntries(),
                new TaskGetBuildMap(),
                new TaskBuildSbp(),
                new TaskRenameBundles(),
                new TaskUpdateBundleInfo(),
                new TaskCreateManifest(),
                new TaskUpdateGlobalManifest(),
                new TaskPublishBuiltin(),
                new TaskCreateReport()
            };
        }

        private static int ResolveEntryCount(ResourceBuildPipelineContext context)
        {
            if (context?.PackageManifest?.Packages != null && context.PackageManifest.Packages.Count > 0)
            {
                var package = context.PackageManifest.Packages[0];
                if (package?.Entries != null)
                {
                    return package.Entries.Count;
                }
            }

            return context?.CollectedEntries?.Count ?? 0;
        }
    }
}
