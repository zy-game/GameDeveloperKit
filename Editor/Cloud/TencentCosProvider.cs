using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GameDeveloperKit.EditorCloud
{
    public sealed class TencentCosProvider :
        ICloudProvider,
        ICloudReadProvider,
        ICloudListProvider,
        ICloudDeleteProvider
    {
        private const int SignatureLifetimeSeconds = 3600;
        private readonly Func<DateTimeOffset> m_UtcNow;

        public TencentCosProvider()
            : this(() => DateTimeOffset.UtcNow)
        {
        }

        internal TencentCosProvider(Func<DateTimeOffset> utcNow)
        {
            m_UtcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        }

        public string ProviderId => CloudProviderId.TencentCos;

        public CloudProviderCapabilities Capabilities =>
            CloudProviderCapabilities.PutObject |
            CloudProviderCapabilities.GetObject |
            CloudProviderCapabilities.ListObjects |
            CloudProviderCapabilities.DeleteObject |
            CloudProviderCapabilities.ConditionalPut;

        public void Validate(CloudPutObjectContext context)
        {
            ValidateRequired(context.Bucket, "bucket");
            ValidateRequired(context.Region, "region");
            ValidateEndpoint(context.Endpoint);
        }

        public CloudHttpRequest CreatePutObjectRequest(CloudPutObjectContext context)
        {
            Validate(context);
            var uri = BuildObjectUri(context);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Host"] = uri.Authority
            };
            if (string.IsNullOrWhiteSpace(context.Request.ContentType) is false)
            {
                headers["Content-Type"] = context.Request.ContentType;
            }

            AddWriteCondition(headers, context.Request.WriteCondition);

            headers["Authorization"] = CreateAuthorization(
                "put",
                "/" + EncodePath(context.Request.ObjectKey),
                context.Credential,
                m_UtcNow(),
                headers,
                new Dictionary<string, string>());
            if (string.IsNullOrWhiteSpace(context.Credential.SessionToken) is false)
            {
                headers["x-cos-security-token"] = context.Credential.SessionToken;
            }

            return new CloudHttpRequest(uri, headers, context.Request.ContentType);
        }

        public void Validate(CloudGetObjectContext context)
        {
            ValidateRequired(context.Bucket, "bucket");
            ValidateRequired(context.Region, "region");
            ValidateEndpoint(context.Endpoint);
        }

        public CloudHttpRequest CreateGetObjectRequest(CloudGetObjectContext context)
        {
            Validate(context);
            var uri = BuildObjectUri(
                context.Endpoint,
                context.Bucket,
                context.Region,
                context.Request.ObjectKey);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Host"] = uri.Authority
            };
            headers["Authorization"] = CreateAuthorization(
                "get",
                "/" + EncodePath(context.Request.ObjectKey),
                context.Credential,
                m_UtcNow(),
                headers,
                new Dictionary<string, string>());
            if (string.IsNullOrWhiteSpace(context.Credential.SessionToken) is false)
            {
                headers["x-cos-security-token"] = context.Credential.SessionToken;
            }

            return new CloudHttpRequest(uri, headers, string.Empty, CloudHttpMethod.Get);
        }

        public void Validate(CloudListObjectsContext context)
        {
            ValidateRequired(context.Bucket, "bucket");
            ValidateRequired(context.Region, "region");
            ValidateEndpoint(context.Endpoint);
        }

        public void Validate(CloudDeleteObjectContext context)
        {
            ValidateRequired(context.Bucket, "bucket");
            ValidateRequired(context.Region, "region");
            ValidateEndpoint(context.Endpoint);
        }

        public CloudHttpRequest CreateListObjectsRequest(CloudListObjectsContext context)
        {
            Validate(context);
            var endpoint = ResolveEndpoint(
                context.Endpoint,
                context.Bucket,
                context.Region);
            var query = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["list-type"] = "2",
                ["max-keys"] = context.Request.MaxKeys.ToString(CultureInfo.InvariantCulture),
                ["prefix"] = EncodeValue(context.Request.Prefix)
            };
            if (string.IsNullOrWhiteSpace(context.Request.ContinuationToken) is false)
            {
                query["continuation-token"] = EncodeValue(context.Request.ContinuationToken);
            }

            var uri = new Uri(
                endpoint.GetLeftPart(UriPartial.Authority) + "/?" + BuildEncodedQuery(query));
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Host"] = uri.Authority
            };
            headers["Authorization"] = CreateAuthorization(
                "get",
                "/",
                context.Credential,
                m_UtcNow(),
                headers,
                query);
            if (string.IsNullOrWhiteSpace(context.Credential.SessionToken) is false)
            {
                headers["x-cos-security-token"] = context.Credential.SessionToken;
            }

            return new CloudHttpRequest(uri, headers, string.Empty, CloudHttpMethod.Get);
        }

        public CloudObjectPage ParseListObjectsResponse(
            CloudListObjectsContext context,
            CloudHttpResponse response)
        {
            return CloudListResponseParser.Parse(
                response,
                response.GetHeader("x-cos-request-id"),
                ProviderId);
        }

        public CloudHttpRequest CreateDeleteObjectRequest(CloudDeleteObjectContext context)
        {
            Validate(context);
            var uri = BuildObjectUri(
                context.Endpoint,
                context.Bucket,
                context.Region,
                context.Request.ObjectKey);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Host"] = uri.Authority
            };
            headers["Authorization"] = CreateAuthorization(
                "delete",
                "/" + EncodePath(context.Request.ObjectKey),
                context.Credential,
                m_UtcNow(),
                headers,
                new Dictionary<string, string>());
            if (string.IsNullOrWhiteSpace(context.Credential.SessionToken) is false)
            {
                headers["x-cos-security-token"] = context.Credential.SessionToken;
            }

            return new CloudHttpRequest(uri, headers, string.Empty, CloudHttpMethod.Delete);
        }

        public CloudDeleteResult ParseDeleteObjectResponse(
            CloudDeleteObjectContext context,
            CloudHttpResponse response,
            bool existed)
        {
            return new CloudDeleteResult(
                ProviderId,
                context.Bucket,
                context.Request.ObjectKey,
                existed,
                response.GetHeader("x-cos-request-id"));
        }

        public CloudUploadResult ParsePutObjectResponse(
            CloudPutObjectContext context,
            CloudHttpResponse response)
        {
            return new CloudUploadResult(
                ProviderId,
                context.Bucket,
                context.Request.ObjectKey,
                response.GetHeader("ETag"),
                response.GetHeader("x-cos-request-id"));
        }

        internal static string CreateAuthorization(
            string method,
            string canonicalPath,
            CloudCredential credential,
            DateTimeOffset utcNow,
            IReadOnlyDictionary<string, string> headers,
            IReadOnlyDictionary<string, string> query)
        {
            var start = utcNow.ToUnixTimeSeconds();
            var keyTime = string.Format(
                CultureInfo.InvariantCulture,
                "{0};{1}",
                start,
                start + SignatureLifetimeSeconds);
            var canonicalHeaders = Canonicalize(headers, true, out var headerList);
            var canonicalQuery = Canonicalize(query, false, out var queryList);
            var httpString = string.Concat(
                method.ToLowerInvariant(), "\n",
                canonicalPath, "\n",
                canonicalQuery, "\n",
                canonicalHeaders, "\n");
            var stringToSign = string.Concat(
                "sha1\n",
                keyTime, "\n",
                Sha1Hex(httpString), "\n");
            var signKey = HmacSha1Hex(credential.SecretAccessKey, keyTime);
            var signature = HmacSha1Hex(signKey, stringToSign);

            return string.Concat(
                "q-sign-algorithm=sha1",
                "&q-ak=", credential.AccessKeyId,
                "&q-sign-time=", keyTime,
                "&q-key-time=", keyTime,
                "&q-header-list=", headerList,
                "&q-url-param-list=", queryList,
                "&q-signature=", signature);
        }

        private static Uri BuildObjectUri(CloudPutObjectContext context)
        {
            return BuildObjectUri(
                context.Endpoint,
                context.Bucket,
                context.Region,
                context.Request.ObjectKey);
        }

        private static Uri BuildObjectUri(
            string customEndpoint,
            string bucket,
            string region,
            string objectKey)
        {
            var endpoint = ResolveEndpoint(customEndpoint, bucket, region);
            return new Uri(endpoint.GetLeftPart(UriPartial.Authority) + "/" + EncodePath(objectKey));
        }

        private static Uri ResolveEndpoint(string customEndpoint, string bucket, string region)
        {
            return string.IsNullOrWhiteSpace(customEndpoint)
                ? new Uri($"https://{bucket}.cos.{region}.myqcloud.com")
                : new Uri(customEndpoint, UriKind.Absolute);
        }

        private static string BuildEncodedQuery(IReadOnlyDictionary<string, string> query)
        {
            return string.Join("&", query
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + "=" + pair.Value));
        }

        private static void AddWriteCondition(
            IDictionary<string, string> headers,
            CloudWriteCondition condition)
        {
            switch ((condition ?? CloudWriteCondition.None).Kind)
            {
                case CloudWriteConditionKind.None:
                    return;
                case CloudWriteConditionKind.IfAbsent:
                    headers["x-cos-forbid-overwrite"] = "true";
                    return;
                case CloudWriteConditionKind.IfMatchETag:
                    headers["If-Match"] = condition.ETag;
                    return;
                default:
                    throw new CloudException(
                        CloudFailureKind.InvalidConfiguration,
                        "Tencent COS write condition is unsupported.",
                        CloudProviderId.TencentCos);
            }
        }

        private static string Canonicalize(
            IReadOnlyDictionary<string, string> values,
            bool encodeValues,
            out string keyList)
        {
            if (values == null || values.Count == 0)
            {
                keyList = string.Empty;
                return string.Empty;
            }

            var normalized = values
                .Where(pair => pair.Value != null)
                .Select(pair => new KeyValuePair<string, string>(
                    pair.Key.ToLowerInvariant(),
                    pair.Value))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToArray();
            keyList = string.Join(";", normalized.Select(pair => pair.Key));
            return string.Join("&", normalized.Select(pair =>
                pair.Key + "=" + (encodeValues ? EncodeValue(pair.Value) : pair.Value)));
        }

        private static string EncodePath(string value)
        {
            return string.Join("/", value.Split('/').Select(EncodeValue));
        }

        private static string EncodeValue(string value)
        {
            const string allowed = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";
            var result = new StringBuilder(value.Length * 2);
            foreach (var item in Encoding.UTF8.GetBytes(value))
            {
                var character = (char)item;
                if (allowed.IndexOf(character) >= 0)
                {
                    result.Append(character);
                }
                else
                {
                    result.Append('%').Append(item.ToString("X2", CultureInfo.InvariantCulture));
                }
            }

            return result.ToString();
        }

        private static string Sha1Hex(string value)
        {
            using var algorithm = SHA1.Create();
            return ToHex(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        private static string HmacSha1Hex(string key, string value)
        {
            using var algorithm = new HMACSHA1(Encoding.UTF8.GetBytes(key));
            return ToHex(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        private static string ToHex(byte[] value)
        {
            var result = new StringBuilder(value.Length * 2);
            foreach (var item in value)
            {
                result.Append(item.ToString("x2", CultureInfo.InvariantCulture));
            }

            return result.ToString();
        }

        private static void ValidateRequired(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.IndexOf('/') >= 0 ||
                value.IndexOf('\\') >= 0 ||
                value.Any(char.IsControl))
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    $"Tencent COS {field} is invalid.",
                    CloudProviderId.TencentCos);
            }
        }

        private static void ValidateEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return;
            }

            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) is false ||
                string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) is false ||
                string.IsNullOrWhiteSpace(uri.Host) ||
                (uri.AbsolutePath.Length > 0 && uri.AbsolutePath != "/") ||
                string.IsNullOrEmpty(uri.Query) is false ||
                string.IsNullOrEmpty(uri.Fragment) is false)
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    "Tencent COS endpoint must be an HTTPS origin without a path, query, or fragment.",
                    CloudProviderId.TencentCos);
            }
        }
    }
}
