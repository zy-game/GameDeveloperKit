using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameDeveloperKit.Resource;

namespace GameDeveloperKit.ResourceRelease;

public sealed class ResourceReleaseService
{
    private const int ProtocolVersion = ResourcePublishSigningContract.CurrentProtocolVersion;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IResourceReleaseProvider m_Provider;

    public ResourceReleaseService(IResourceReleaseProvider provider)
    {
        m_Provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public async Task<ReleaseResult> ExecuteAsync(
        ReleasePlan plan,
        ReadOnlyMemory<byte> privateKeyPem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (privateKeyPem.IsEmpty)
        {
            throw new ArgumentException("Private key PEM is required.", nameof(privateKeyPem));
        }

        var uploaded = 0;
        var reused = 0;
        foreach (var artifact in plan.Artifacts)
        {
            var item = new ReleaseObject(
                artifact.RemoteKey,
                artifact.LocalPath,
                artifact.Sha256,
                artifact.SizeBytes,
                ContentTypeFor(artifact.Kind));
            var result = await EnsureImmutableAsync(item, cancellationToken).ConfigureAwait(false);
            if (result.Uploaded)
            {
                uploaded++;
            }
            else
            {
                reused++;
            }
        }

        var manifest = plan.Artifacts.Single(artifact => artifact.Kind == "resource-manifest");
        var descriptorKey = string.Join('/', plan.Channel, plan.Platform, plan.Version, "channel-release.json");
        var descriptorBytes = Encoding.UTF8.GetBytes(CreateDescriptorJson(plan));
        var descriptorPath = Path.Combine(Path.GetTempPath(), "gdk-release-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            await File.WriteAllBytesAsync(descriptorPath, descriptorBytes, cancellationToken).ConfigureAwait(false);
            var descriptor = new ReleaseObject(
                descriptorKey,
                descriptorPath,
                Convert.ToHexString(SHA256.HashData(descriptorBytes)).ToLowerInvariant(),
                descriptorBytes.Length,
                "application/json");
            var descriptorResult = await EnsureImmutableAsync(descriptor, cancellationToken).ConfigureAwait(false);
            if (descriptorResult.Uploaded)
            {
                uploaded++;
            }
            else
            {
                reused++;
            }
        }
        finally
        {
            if (File.Exists(descriptorPath))
            {
                File.Delete(descriptorPath);
            }
        }

        var pointer = CreateSignedPointer(plan, manifest.Sha256, privateKeyPem.Span);
        var pointerJson = JsonSerializer.Serialize(pointer, JsonOptions) + "\n";
        var pointerInfo = await m_Provider.PutTextConditionalAsync(
            plan.PointerKey,
            pointerJson,
            plan.ExpectedPointerETag,
            cancellationToken).ConfigureAwait(false);
        if (pointerInfo.Exists is false || string.IsNullOrWhiteSpace(pointerInfo.ETag))
        {
            throw new InvalidDataException("Provider did not confirm the conditional pointer update.");
        }

        return new ReleaseResult(
            plan,
            descriptorKey,
            plan.PointerKey,
            pointerInfo.ETag,
            uploaded,
            reused);
    }

    public static PublishPointer CreateSignedPointer(
        ReleasePlan plan,
        string manifestSha256,
        ReadOnlySpan<byte> privateKeyPem)
    {
        ArgumentNullException.ThrowIfNull(plan);
        manifestSha256 = NormalizeSha256(manifestSha256);
        var payload = ResourcePublishSigningContract.BuildPayload(
            ProtocolVersion,
            plan.Channel,
            plan.Platform,
            plan.Version,
            manifestSha256,
            plan.MinimumClientBuild,
            plan.MaximumClientBuild);
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(Encoding.UTF8.GetString(privateKeyPem));
            var signature = rsa.SignData(
                payload,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            return new PublishPointer(
                ProtocolVersion,
                plan.Channel,
                plan.Platform,
                plan.Version,
                manifestSha256,
                plan.MinimumClientBuild,
                plan.MaximumClientBuild,
                plan.KeyId,
                Convert.ToBase64String(signature));
        }
        catch (CryptographicException exception)
        {
            throw new ArgumentException("Private key PEM is invalid.", nameof(privateKeyPem), exception);
        }
    }

    public static byte[] BuildSigningPayload(
        int protocolVersion,
        string channel,
        string platform,
        string version,
        string manifestSha256,
        long minimumClientBuild,
        long maximumClientBuild)
    {
        return ResourcePublishSigningContract.BuildPayload(
            protocolVersion,
            channel,
            platform,
            version,
            manifestSha256,
            minimumClientBuild,
            maximumClientBuild);
    }

    private async Task<ImmutableResult> EnsureImmutableAsync(
        ReleaseObject item,
        CancellationToken cancellationToken)
    {
        var existing = await m_Provider.HeadAsync(item.Key, cancellationToken).ConfigureAwait(false);
        if (existing.Exists)
        {
            VerifyRemote(item, existing);
            return new ImmutableResult(false);
        }

        var uploaded = await m_Provider.PutImmutableAsync(item, cancellationToken).ConfigureAwait(false);
        VerifyRemote(item, uploaded);
        var verified = await m_Provider.HeadAsync(item.Key, cancellationToken).ConfigureAwait(false);
        VerifyRemote(item, verified);
        return new ImmutableResult(true);
    }

    private static void VerifyRemote(ReleaseObject item, RemoteObjectInfo remote)
    {
        if (remote.Exists is false ||
            string.Equals(item.Key, remote.Key, StringComparison.Ordinal) is false ||
            string.Equals(item.Sha256, remote.Sha256, StringComparison.Ordinal) is false ||
            item.SizeBytes != remote.SizeBytes)
        {
            throw new InvalidDataException("Remote immutable object conflicts with release evidence.");
        }
    }

    private static string CreateDescriptorJson(ReleasePlan plan)
    {
        var descriptor = new
        {
            schemaVersion = 1,
            channel = plan.Channel,
            platform = plan.Platform,
            version = plan.Version,
            minimumClientBuild = plan.MinimumClientBuild,
            maximumClientBuild = plan.MaximumClientBuild,
            artifacts = plan.Artifacts.Select(artifact => new
            {
                artifact.Kind,
                key = artifact.RemoteKey,
                artifact.Sha256,
                artifact.SizeBytes
            }).ToArray()
        };
        return JsonSerializer.Serialize(descriptor, JsonOptions) + "\n";
    }

    private static string ContentTypeFor(string kind)
    {
        return kind == "resource-manifest" ? "application/json" : "application/octet-stream";
    }

    private static string NormalizeSha256(string value)
    {
        if (value is null || value.Length != 64 || value.Any(character => character is < '0' or > '9' and < 'a' or > 'f'))
        {
            throw new ArgumentException("Manifest SHA-256 must be lowercase hexadecimal.", nameof(value));
        }
        return value;
    }

    private readonly record struct ImmutableResult(bool Uploaded);
}
