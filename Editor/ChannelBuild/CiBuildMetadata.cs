namespace GameDeveloperKit.ChannelBuild
{
    public sealed class CiBuildMetadata
    {
        public CiBuildMetadata(
            string provider,
            string jobName,
            string buildId,
            string buildUrl,
            string revision)
        {
            Provider = ChannelBuildContext.RequireSafeSegment(provider, nameof(provider));
            JobName = ChannelBuildContext.RequireText(jobName, nameof(jobName));
            BuildId = ChannelBuildContext.RequireSafeSegment(buildId, nameof(buildId));
            BuildUrl = ChannelBuildContext.ValidateOptionalHttpUrl(buildUrl, nameof(buildUrl));
            Revision = ChannelBuildContext.RequireSafeSegment(revision, nameof(revision));
        }

        public string Provider { get; }

        public string JobName { get; }

        public string BuildId { get; }

        public string BuildUrl { get; }

        public string Revision { get; }
    }
}
