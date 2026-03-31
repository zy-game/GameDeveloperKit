namespace GameDeveloperKit.Editor
{
    internal interface ISbpBuildTask
    {
        string TaskName { get; }

        ResourceBuildTaskResult Run(ResourceBuildPipelineContext context);
    }
}
