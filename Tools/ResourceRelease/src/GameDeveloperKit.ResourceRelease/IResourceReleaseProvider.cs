namespace GameDeveloperKit.ResourceRelease;

public interface IResourceReleaseProvider
{
    Task<RemoteObjectInfo> HeadAsync(string key, CancellationToken cancellationToken);

    Task<RemoteObjectInfo> PutImmutableAsync(
        ReleaseObject item,
        CancellationToken cancellationToken);

    Task<string?> ReadTextAsync(string key, CancellationToken cancellationToken);

    Task<RemoteObjectInfo> PutTextConditionalAsync(
        string key,
        string content,
        string? expectedETag,
        CancellationToken cancellationToken);
}
