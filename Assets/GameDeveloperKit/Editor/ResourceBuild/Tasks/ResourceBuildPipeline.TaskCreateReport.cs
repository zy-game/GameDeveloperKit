using System;

namespace GameDeveloperKit.Editor
{
    internal sealed partial class ResourceBuildPipeline
    {
        private sealed class TaskCreateReport : ISbpBuildTask
        {
            public string TaskName => "Create Build Report";

            public ResourceBuildTaskResult Run(ResourceBuildPipelineContext context)
            {
                if (context.BuildEndUtc == default)
                {
                    context.BuildEndUtc = DateTime.UtcNow;
                }

                WriteExecutionReport(context, true, string.Empty);
                return ResourceBuildTaskResult.Succeed();
            }
        }
    }
}
