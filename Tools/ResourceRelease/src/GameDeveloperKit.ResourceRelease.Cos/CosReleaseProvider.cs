using System.Text;

namespace GameDeveloperKit.ResourceRelease.Cos;

public sealed partial class CosReleaseProvider : IResourceReleaseProvider
{
    internal const string HashMetadataHeader = "x-cos-meta-gdk-sha256";
    internal const string IfMatchHeader = "If-Match";
    internal const string IfNoneMatchHeader = "If-None-Match";
    internal const string ContentTypeHeader = "Content-Type";

    private readonly ICosGateway m_Gateway;

    public CosReleaseProvider(
        string secretId,
        string secretKey,
        string region,
        string bucket)
        : this(new CosXmlGateway(
            RequireText(secretId, nameof(secretId)),
            RequireText(secretKey, nameof(secretKey)),
            RequireText(region, nameof(region)),
            RequireText(bucket, nameof(bucket))))
    {
    }

    internal CosReleaseProvider(ICosGateway gateway)
    {
        m_Gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public Task<RemoteObjectInfo> HeadAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        key = RequireKey(key);
        try
        {
            return Task.FromResult(ToRemote(key, m_Gateway.Head(key)));
        }
        catch (CosGatewayException exception) when (exception.StatusCode == 404)
        {
            return Task.FromResult(new RemoteObjectInfo(key, false, null, 0, null));
        }
    }

    public Task<RemoteObjectInfo> PutImmutableAsync(
        ReleaseObject item,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(item);
        RequireKey(item.Key);
        RequireSha256(item.Sha256);
        if (item.SizeBytes < 0 || File.Exists(item.LocalPath) is false)
        {
            throw new ArgumentException("Release object local evidence is invalid.", nameof(item));
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [IfNoneMatchHeader] = "*",
            [HashMetadataHeader] = item.Sha256,
            [ContentTypeHeader] = RequireText(item.ContentType, nameof(item.ContentType))
        };
        try
        {
            var result = m_Gateway.PutFile(item.Key, item.LocalPath, headers);
            return Task.FromResult(ToRemote(item.Key, result));
        }
        catch (CosGatewayException exception) when (exception.StatusCode == 412)
        {
            throw new InvalidOperationException("COS immutable object already exists.", exception);
        }
    }

    public Task<string?> ReadTextAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        key = RequireKey(key);
        try
        {
            return Task.FromResult<string?>(Encoding.UTF8.GetString(m_Gateway.GetBytes(key)));
        }
        catch (CosGatewayException exception) when (exception.StatusCode == 404)
        {
            return Task.FromResult<string?>(null);
        }
    }

    public Task<RemoteObjectInfo> PutTextConditionalAsync(
        string key,
        string content,
        string? expectedETag,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        key = RequireKey(key);
        ArgumentNullException.ThrowIfNull(content);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ContentTypeHeader] = "application/json",
            [expectedETag is null ? IfNoneMatchHeader : IfMatchHeader] = expectedETag ?? "*"
        };
        try
        {
            var result = m_Gateway.PutBytes(key, Encoding.UTF8.GetBytes(content), headers);
            return Task.FromResult(ToRemote(key, result));
        }
        catch (CosGatewayException exception) when (exception.StatusCode == 412)
        {
            throw new InvalidOperationException("COS conditional pointer update conflicted.", exception);
        }
    }

    private static RemoteObjectInfo ToRemote(string key, CosObjectResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.Headers.TryGetValue(HashMetadataHeader, out var hash);
        return new RemoteObjectInfo(
            key,
            true,
            string.IsNullOrWhiteSpace(hash) ? null : hash,
            result.SizeBytes,
            TrimQuotes(result.ETag));
    }

    private static string? TrimQuotes(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().Trim('"');
    }

    private static string RequireKey(string value)
    {
        value = RequireText(value, nameof(value));
        if (value.StartsWith('/') || value.Contains('\\') ||
            value.Split('/').Any(segment => string.IsNullOrEmpty(segment) || segment is "." or ".."))
        {
            throw new ArgumentException("COS object key is invalid.", nameof(value));
        }
        return value;
    }

    private static string RequireSha256(string value)
    {
        if (value is null || value.Length != 64 || value.Any(character => character is < '0' or > '9' and < 'a' or > 'f'))
        {
            throw new ArgumentException("SHA-256 is invalid.", nameof(value));
        }
        return value;
    }

    private static string RequireText(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(['\r', '\n']) >= 0)
        {
            throw new ArgumentException("Value must be non-empty single-line text.", name);
        }
        return value;
    }

    internal interface ICosGateway
    {
        CosObjectResult Head(string key);
        CosObjectResult PutFile(string key, string path, IReadOnlyDictionary<string, string> headers);
        CosObjectResult PutBytes(string key, byte[] bytes, IReadOnlyDictionary<string, string> headers);
        byte[] GetBytes(string key);
    }

    internal sealed record CosObjectResult(
        long SizeBytes,
        string? ETag,
        IReadOnlyDictionary<string, string> Headers);

    internal sealed class CosGatewayException : Exception
    {
        internal CosGatewayException(int statusCode, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
        }

        internal int StatusCode { get; }
    }
}
