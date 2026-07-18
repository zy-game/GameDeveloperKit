using System.Collections.ObjectModel;

namespace GameDeveloperKit.ResourceRelease;

public sealed record ReleaseArtifact(
    string Kind,
    string LocalPath,
    string RelativePath,
    string RemoteKey,
    string Sha256,
    long SizeBytes);

public sealed record ReleaseObject(
    string Key,
    string LocalPath,
    string Sha256,
    long SizeBytes,
    string ContentType);

public sealed record RemoteObjectInfo(
    string Key,
    bool Exists,
    string? Sha256,
    long SizeBytes,
    string? ETag);

public sealed class ReleasePlan
{
    public ReleasePlan(
        string channel,
        string platform,
        string version,
        string outputRoot,
        long minimumClientBuild,
        long maximumClientBuild,
        IReadOnlyList<ReleaseArtifact> artifacts)
    {
        Channel = channel;
        Platform = platform;
        Version = version;
        OutputRoot = outputRoot;
        MinimumClientBuild = minimumClientBuild;
        MaximumClientBuild = maximumClientBuild;
        Artifacts = new ReadOnlyCollection<ReleaseArtifact>(artifacts.ToArray());
    }

    public string Channel { get; }
    public string Platform { get; }
    public string Version { get; }
    public string OutputRoot { get; }
    public long MinimumClientBuild { get; }
    public long MaximumClientBuild { get; }
    public IReadOnlyList<ReleaseArtifact> Artifacts { get; }
}

public sealed record PublishPointer(
    int ProtocolVersion,
    string Channel,
    string Platform,
    string Version,
    string ManifestSha256,
    long MinimumClientBuild,
    long MaximumClientBuild,
    string KeyId,
    string Signature);

public sealed record StagedReleaseResult(
    ReleasePlan Plan,
    string DescriptorKey,
    string DescriptorSha256,
    int UploadedObjectCount,
    int ReusedObjectCount);
