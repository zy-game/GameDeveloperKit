using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDeveloperKit.EditorCloud
{
    public static class CloudProviderId
    {
        public const string TencentCos = "tencent-cos";
        public const string AliyunOss = "aliyun-oss";
    }

    [Flags]
    public enum CloudProviderCapabilities
    {
        None = 0,
        PutObject = 1,
        GetObject = 2,
        ListObjects = 4,
        DeleteObject = 8,
        ConditionalPut = 16
    }

    public enum CloudFailureKind
    {
        InvalidConfiguration,
        CredentialsMissing,
        Authentication,
        Permission,
        Network,
        RateLimited,
        ProviderResponse,
        LocalFile,
        NotFound,
        PreconditionFailed,
        ResponseTooLarge
    }

    public sealed class CloudException : Exception
    {
        public CloudException(
            CloudFailureKind kind,
            string message,
            string providerId = null,
            int? statusCode = null,
            string requestId = null,
            Exception innerException = null)
            : base(message, innerException)
        {
            Kind = kind;
            ProviderId = providerId ?? string.Empty;
            StatusCode = statusCode;
            RequestId = requestId ?? string.Empty;
        }

        public CloudFailureKind Kind { get; }

        public string ProviderId { get; }

        public int? StatusCode { get; }

        public string RequestId { get; }
    }

    public sealed class CloudCredential
    {
        public CloudCredential(string accessKeyId, string secretAccessKey, string sessionToken = null)
        {
            AccessKeyId = accessKeyId?.Trim() ?? string.Empty;
            SecretAccessKey = secretAccessKey ?? string.Empty;
            SessionToken = sessionToken ?? string.Empty;
        }

        public string AccessKeyId { get; }

        public string SecretAccessKey { get; }

        public string SessionToken { get; }

        public override string ToString() => "CloudCredential(AccessKeyId=***, SecretAccessKey=***, SessionToken=***)";
    }

    public sealed class CloudObjectUploadRequest
    {
        public CloudObjectUploadRequest(
            string localFilePath,
            string objectKey,
            string contentType,
            CloudWriteCondition writeCondition = null,
            string cacheControl = null)
        {
            LocalFilePath = localFilePath ?? string.Empty;
            ObjectKey = objectKey ?? string.Empty;
            ContentType = contentType?.Trim() ?? string.Empty;
            WriteCondition = writeCondition ?? CloudWriteCondition.None;
            CacheControl = cacheControl?.Trim() ?? string.Empty;
        }

        public string LocalFilePath { get; }

        public string ObjectKey { get; }

        public string ContentType { get; }

        public CloudWriteCondition WriteCondition { get; }

        public string CacheControl { get; }
    }

    public enum CloudWriteConditionKind
    {
        None,
        IfAbsent,
        IfMatchETag
    }

    public sealed class CloudWriteCondition
    {
        private CloudWriteCondition(CloudWriteConditionKind kind, string etag)
        {
            Kind = kind;
            ETag = etag?.Trim() ?? string.Empty;
        }

        public static CloudWriteCondition None { get; } =
            new CloudWriteCondition(CloudWriteConditionKind.None, null);

        public static CloudWriteCondition IfAbsent { get; } =
            new CloudWriteCondition(CloudWriteConditionKind.IfAbsent, null);

        public CloudWriteConditionKind Kind { get; }

        public string ETag { get; }

        public static CloudWriteCondition IfMatch(string etag)
        {
            if (string.IsNullOrWhiteSpace(etag))
            {
                throw new ArgumentException("Conditional cloud write requires an ETag.", nameof(etag));
            }

            return new CloudWriteCondition(CloudWriteConditionKind.IfMatchETag, etag);
        }
    }

    public sealed class CloudObjectGetRequest
    {
        public CloudObjectGetRequest(string objectKey)
        {
            ObjectKey = objectKey ?? string.Empty;
        }

        public string ObjectKey { get; }
    }

    public sealed class CloudObjectGetResult
    {
        public CloudObjectGetResult(
            string providerId,
            string bucket,
            string objectKey,
            string etag,
            string requestId,
            string content)
        {
            ProviderId = providerId ?? string.Empty;
            Bucket = bucket ?? string.Empty;
            ObjectKey = objectKey ?? string.Empty;
            ETag = etag ?? string.Empty;
            RequestId = requestId ?? string.Empty;
            Content = content ?? string.Empty;
        }

        public string ProviderId { get; }
        public string Bucket { get; }
        public string ObjectKey { get; }
        public string ETag { get; }
        public string RequestId { get; }
        public string Content { get; }
    }

    public sealed class CloudObjectListRequest
    {
        public const int DefaultMaxKeys = 1000;
        public const int MaximumMaxKeys = 1000;

        public CloudObjectListRequest(
            string prefix,
            string continuationToken = null,
            int maxKeys = DefaultMaxKeys)
        {
            if (maxKeys < 1 || maxKeys > MaximumMaxKeys)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxKeys),
                    $"Cloud list page size must be between 1 and {MaximumMaxKeys}.");
            }

            Prefix = prefix ?? string.Empty;
            ContinuationToken = continuationToken?.Trim() ?? string.Empty;
            MaxKeys = maxKeys;
        }

        public string Prefix { get; }
        public string ContinuationToken { get; }
        public int MaxKeys { get; }
    }

    public sealed class CloudObjectInfo
    {
        public CloudObjectInfo(string objectKey, string etag, long size)
        {
            ObjectKey = objectKey ?? string.Empty;
            ETag = etag ?? string.Empty;
            Size = size;
        }

        public string ObjectKey { get; }
        public string ETag { get; }
        public long Size { get; }
    }

    public sealed class CloudObjectPage
    {
        public CloudObjectPage(
            IEnumerable<CloudObjectInfo> objects,
            bool isTruncated,
            string nextContinuationToken,
            string requestId)
        {
            Objects = (objects ?? Array.Empty<CloudObjectInfo>()).ToArray();
            IsTruncated = isTruncated;
            NextContinuationToken = nextContinuationToken?.Trim() ?? string.Empty;
            RequestId = requestId ?? string.Empty;
        }

        public IReadOnlyList<CloudObjectInfo> Objects { get; }
        public bool IsTruncated { get; }
        public string NextContinuationToken { get; }
        public string RequestId { get; }
    }

    public sealed class CloudObjectDeleteRequest
    {
        public CloudObjectDeleteRequest(string objectKey)
        {
            ObjectKey = objectKey ?? string.Empty;
        }

        public string ObjectKey { get; }
    }

    public sealed class CloudDeleteResult
    {
        public CloudDeleteResult(
            string providerId,
            string bucket,
            string objectKey,
            bool existed,
            string requestId)
        {
            ProviderId = providerId ?? string.Empty;
            Bucket = bucket ?? string.Empty;
            ObjectKey = objectKey ?? string.Empty;
            Existed = existed;
            RequestId = requestId ?? string.Empty;
        }

        public string ProviderId { get; }
        public string Bucket { get; }
        public string ObjectKey { get; }
        public bool Existed { get; }
        public string RequestId { get; }
    }

    public sealed class CloudGetObjectContext
    {
        public CloudGetObjectContext(
            string providerId,
            string bucket,
            string region,
            string endpoint,
            CloudCredential credential,
            CloudObjectGetRequest request)
        {
            ProviderId = providerId?.Trim() ?? string.Empty;
            Bucket = bucket?.Trim() ?? string.Empty;
            Region = region?.Trim() ?? string.Empty;
            Endpoint = endpoint?.Trim() ?? string.Empty;
            Credential = credential;
            Request = request;
        }

        public string ProviderId { get; }
        public string Bucket { get; }
        public string Region { get; }
        public string Endpoint { get; }
        public CloudCredential Credential { get; }
        public CloudObjectGetRequest Request { get; }
    }

    public sealed class CloudListObjectsContext
    {
        public CloudListObjectsContext(
            string providerId,
            string bucket,
            string region,
            string endpoint,
            CloudCredential credential,
            CloudObjectListRequest request)
        {
            ProviderId = providerId?.Trim() ?? string.Empty;
            Bucket = bucket?.Trim() ?? string.Empty;
            Region = region?.Trim() ?? string.Empty;
            Endpoint = endpoint?.Trim() ?? string.Empty;
            Credential = credential;
            Request = request;
        }

        public string ProviderId { get; }
        public string Bucket { get; }
        public string Region { get; }
        public string Endpoint { get; }
        public CloudCredential Credential { get; }
        public CloudObjectListRequest Request { get; }
    }

    public sealed class CloudDeleteObjectContext
    {
        public CloudDeleteObjectContext(
            string providerId,
            string bucket,
            string region,
            string endpoint,
            CloudCredential credential,
            CloudObjectDeleteRequest request)
        {
            ProviderId = providerId?.Trim() ?? string.Empty;
            Bucket = bucket?.Trim() ?? string.Empty;
            Region = region?.Trim() ?? string.Empty;
            Endpoint = endpoint?.Trim() ?? string.Empty;
            Credential = credential;
            Request = request;
        }

        public string ProviderId { get; }
        public string Bucket { get; }
        public string Region { get; }
        public string Endpoint { get; }
        public CloudCredential Credential { get; }
        public CloudObjectDeleteRequest Request { get; }
    }

    public sealed class CloudBatchUploadRequest
    {
        public const int DefaultMaxConcurrency = 4;
        public const int MaximumConcurrency = 8;

        public CloudBatchUploadRequest(
            IEnumerable<CloudObjectUploadRequest> objects,
            int maxConcurrency = DefaultMaxConcurrency)
        {
            Objects = (objects ?? throw new ArgumentNullException(nameof(objects))).ToArray();
            if (Objects.Any(item => item == null))
            {
                throw new ArgumentException("Batch upload requests cannot contain null items.", nameof(objects));
            }

            if (maxConcurrency < 1 || maxConcurrency > MaximumConcurrency)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxConcurrency),
                    $"Max concurrency must be between 1 and {MaximumConcurrency}.");
            }

            MaxConcurrency = maxConcurrency;
        }

        public IReadOnlyList<CloudObjectUploadRequest> Objects { get; }

        public int MaxConcurrency { get; }
    }

    public sealed class CloudUploadProgress
    {
        public CloudUploadProgress(
            string objectKey,
            long objectBytesSent,
            long objectTotalBytes,
            long totalBytesSent,
            long totalBytes)
        {
            ObjectKey = objectKey ?? string.Empty;
            ObjectBytesSent = objectBytesSent;
            ObjectTotalBytes = objectTotalBytes;
            TotalBytesSent = totalBytesSent;
            TotalBytes = totalBytes;
        }

        public string ObjectKey { get; }

        public long ObjectBytesSent { get; }

        public long ObjectTotalBytes { get; }

        public long TotalBytesSent { get; }

        public long TotalBytes { get; }
    }

    public sealed class CloudUploadResult
    {
        public CloudUploadResult(
            string providerId,
            string bucket,
            string objectKey,
            string etag,
            string requestId)
        {
            ProviderId = providerId ?? string.Empty;
            Bucket = bucket ?? string.Empty;
            ObjectKey = objectKey ?? string.Empty;
            ETag = etag ?? string.Empty;
            RequestId = requestId ?? string.Empty;
        }

        public string ProviderId { get; }

        public string Bucket { get; }

        public string ObjectKey { get; }

        public string ETag { get; }

        public string RequestId { get; }
    }

    public sealed class CloudBatchUploadResult
    {
        public CloudBatchUploadResult(
            IEnumerable<CloudUploadResult> succeeded,
            IReadOnlyDictionary<string, CloudException> failed)
        {
            Succeeded = (succeeded ?? Array.Empty<CloudUploadResult>()).ToArray();
            Failed = failed ?? new Dictionary<string, CloudException>(StringComparer.Ordinal);
        }

        public IReadOnlyList<CloudUploadResult> Succeeded { get; }

        public IReadOnlyDictionary<string, CloudException> Failed { get; }

        public bool IsSuccess => Failed.Count == 0;
    }

    public sealed class CloudPutObjectContext
    {
        public CloudPutObjectContext(
            string providerId,
            string bucket,
            string region,
            string endpoint,
            CloudCredential credential,
            CloudObjectUploadRequest request)
        {
            ProviderId = providerId?.Trim() ?? string.Empty;
            Bucket = bucket?.Trim() ?? string.Empty;
            Region = region?.Trim() ?? string.Empty;
            Endpoint = endpoint?.Trim() ?? string.Empty;
            Credential = credential;
            Request = request;
        }

        public string ProviderId { get; }

        public string Bucket { get; }

        public string Region { get; }

        public string Endpoint { get; }

        public CloudCredential Credential { get; }

        public CloudObjectUploadRequest Request { get; }
    }

    internal static class CloudEndpointContext
    {
        public static string Format(string bucket, string region, string endpoint)
        {
            return $" bucket:{bucket}, region:{region}, endpoint:{endpoint}";
        }
    }

    public enum CloudHttpMethod
    {
        Put,
        Get,
        Delete
    }

    public sealed class CloudHttpRequest
    {
        public CloudHttpRequest(
            Uri uri,
            IReadOnlyDictionary<string, string> headers,
            string contentType,
            CloudHttpMethod method = CloudHttpMethod.Put)
        {
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));
            Headers = new Dictionary<string, string>(
                headers ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase);
            ContentType = contentType?.Trim() ?? string.Empty;
            Method = method;
        }

        public Uri Uri { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public string ContentType { get; }

        public CloudHttpMethod Method { get; }

        public override string ToString() => $"{Method.ToString().ToUpperInvariant()} {Uri.GetLeftPart(UriPartial.Path)}";
    }

    public sealed class CloudHttpResponse
    {
        public CloudHttpResponse(
            int statusCode,
            IReadOnlyDictionary<string, string> headers,
            string body)
        {
            StatusCode = statusCode;
            Headers = new Dictionary<string, string>(
                headers ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase);
            Body = body ?? string.Empty;
        }

        public int StatusCode { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public string Body { get; }

        public bool IsSuccessStatusCode => StatusCode >= 200 && StatusCode <= 299;

        public string GetHeader(string name)
        {
            return Headers.TryGetValue(name, out var value) ? value : string.Empty;
        }
    }
}
