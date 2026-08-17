using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GameDeveloperKit.EditorCloud
{
    public sealed class AliyunOssProvider :
        ICloudProvider,
        ICloudReadProvider,
        ICloudListProvider,
        ICloudDeleteProvider
    {
        private const string UnsignedPayload = "UNSIGNED-PAYLOAD";
        private readonly Func<DateTimeOffset> m_UtcNow;

        public AliyunOssProvider()
            : this(() => DateTimeOffset.UtcNow)
        {
        }

        internal AliyunOssProvider(Func<DateTimeOffset> utcNow)
        {
            m_UtcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        }

        public string ProviderId => CloudProviderId.AliyunOss;

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
            CloudRegionValidator.ValidateOrThrow(ProviderId, context.Region);
            ValidateEndpoint(context.Endpoint);
        }

        public CloudHttpRequest CreatePutObjectRequest(CloudPutObjectContext context)
        {
            Validate(context);
            var uri = BuildObjectUri(context);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(context.Request.ContentType) is false)
            {
                headers["Content-Type"] = context.Request.ContentType;
            }

            if (string.IsNullOrWhiteSpace(context.Request.CacheControl) is false)
            {
                headers["Cache-Control"] = context.Request.CacheControl;
            }

            headers["x-oss-content-sha256"] = UnsignedPayload;
            AddWriteCondition(headers, context.Request.WriteCondition);
            if (string.IsNullOrWhiteSpace(context.Credential.SessionToken) is false)
            {
                headers["x-oss-security-token"] = context.Credential.SessionToken;
            }

            headers["Authorization"] = CreateAuthorization(
                context.Bucket,
                context.Request.ObjectKey,
                context.Region,
                context.Credential,
                m_UtcNow(),
                headers,
                new Dictionary<string, string>(),
                GetOptionalSignedHeaderNames(headers));
            return new CloudHttpRequest(uri, headers, context.Request.ContentType);
        }

        public void Validate(CloudGetObjectContext context)
        {
            ValidateRequired(context.Bucket, "bucket");
            ValidateRequired(context.Region, "region");
            CloudRegionValidator.ValidateOrThrow(ProviderId, context.Region);
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
                ["x-oss-content-sha256"] = UnsignedPayload
            };
            if (string.IsNullOrWhiteSpace(context.Credential.SessionToken) is false)
            {
                headers["x-oss-security-token"] = context.Credential.SessionToken;
            }

            headers["Authorization"] = CreateAuthorization(
                "GET",
                context.Bucket,
                context.Request.ObjectKey,
                context.Region,
                context.Credential,
                m_UtcNow(),
                headers,
                new Dictionary<string, string>());
            return new CloudHttpRequest(uri, headers, string.Empty, CloudHttpMethod.Get);
        }

        public void Validate(CloudListObjectsContext context)
        {
            ValidateRequired(context.Bucket, "bucket");
            ValidateRequired(context.Region, "region");
            CloudRegionValidator.ValidateOrThrow(ProviderId, context.Region);
            ValidateEndpoint(context.Endpoint);
        }

        public void Validate(CloudDeleteObjectContext context)
        {
            ValidateRequired(context.Bucket, "bucket");
            ValidateRequired(context.Region, "region");
            CloudRegionValidator.ValidateOrThrow(ProviderId, context.Region);
            ValidateEndpoint(context.Endpoint);
        }

        public CloudHttpRequest CreateListObjectsRequest(CloudListObjectsContext context)
        {
            Validate(context);
            var query = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["list-type"] = "2",
                ["max-keys"] = context.Request.MaxKeys.ToString(CultureInfo.InvariantCulture),
                ["prefix"] = context.Request.Prefix
            };
            if (string.IsNullOrWhiteSpace(context.Request.ContinuationToken) is false)
            {
                query["continuation-token"] = context.Request.ContinuationToken;
            }

            var endpoint = ResolveEndpoint(
                context.Endpoint,
                context.Bucket,
                context.Region);
            var uri = new Uri(
                endpoint.GetLeftPart(UriPartial.Authority) + "/?" + BuildEncodedQuery(query));
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-oss-content-sha256"] = UnsignedPayload
            };
            if (string.IsNullOrWhiteSpace(context.Credential.SessionToken) is false)
            {
                headers["x-oss-security-token"] = context.Credential.SessionToken;
            }

            headers["Authorization"] = CreateAuthorization(
                "GET",
                context.Bucket,
                string.Empty,
                context.Region,
                context.Credential,
                m_UtcNow(),
                headers,
                query);
            return new CloudHttpRequest(uri, headers, string.Empty, CloudHttpMethod.Get);
        }

        public CloudObjectPage ParseListObjectsResponse(
            CloudListObjectsContext context,
            CloudHttpResponse response)
        {
            return CloudListResponseParser.Parse(
                response,
                response.GetHeader("x-oss-request-id"),
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
                ["x-oss-content-sha256"] = UnsignedPayload
            };
            if (string.IsNullOrWhiteSpace(context.Credential.SessionToken) is false)
            {
                headers["x-oss-security-token"] = context.Credential.SessionToken;
            }

            headers["Authorization"] = CreateAuthorization(
                "DELETE",
                context.Bucket,
                context.Request.ObjectKey,
                context.Region,
                context.Credential,
                m_UtcNow(),
                headers,
                new Dictionary<string, string>());
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
                response.GetHeader("x-oss-request-id"));
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
                response.GetHeader("x-oss-request-id"));
        }

        internal static string CreateAuthorization(
            string bucket,
            string objectKey,
            string region,
            CloudCredential credential,
            DateTimeOffset utcNow,
            IDictionary<string, string> headers,
            IReadOnlyDictionary<string, string> query,
            IReadOnlyCollection<string> additionalHeaderNames = null)
        {
            return CreateAuthorization(
                "PUT",
                bucket,
                objectKey,
                region,
                credential,
                utcNow,
                headers,
                query,
                additionalHeaderNames);
        }

        internal static string CreateAuthorization(
            string method,
            string bucket,
            string objectKey,
            string region,
            CloudCredential credential,
            DateTimeOffset utcNow,
            IDictionary<string, string> headers,
            IReadOnlyDictionary<string, string> query,
            IReadOnlyCollection<string> additionalHeaderNames = null)
        {
            var timestamp = utcNow.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
            var date = utcNow.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            headers["x-oss-content-sha256"] = UnsignedPayload;
            headers["x-oss-date"] = timestamp;

            var additionalHeaders = CanonicalizeAdditionalHeaderNames(headers, additionalHeaderNames);
            var scope = $"{date}/{region}/oss/aliyun_v4_request";
            var canonicalRequest = CreateCanonicalRequest(
                method,
                bucket,
                objectKey,
                headers,
                query,
                additionalHeaders);
            var stringToSign = string.Concat(
                "OSS4-HMAC-SHA256\n",
                timestamp, "\n",
                scope, "\n",
                Sha256Hex(canonicalRequest));
            var signature = CalculateSignature(
                credential.SecretAccessKey,
                date,
                region,
                stringToSign);
            var authorization = string.Concat(
                "OSS4-HMAC-SHA256 Credential=",
                credential.AccessKeyId,
                "/",
                scope);
            if (additionalHeaders.Length > 0)
            {
                authorization += ",AdditionalHeaders=" + additionalHeaders;
            }

            return authorization + ",Signature=" + signature;
        }

        internal static string CreateCanonicalRequest(
            string method,
            string bucket,
            string objectKey,
            IDictionary<string, string> headers,
            IReadOnlyDictionary<string, string> query,
            IReadOnlyCollection<string> additionalHeaderNames)
        {
            var additionalHeaders = CanonicalizeAdditionalHeaderNames(headers, additionalHeaderNames);
            return CreateCanonicalRequest(
                method,
                bucket,
                objectKey,
                headers,
                query,
                additionalHeaders);
        }

        private static string CreateCanonicalRequest(
            string method,
            string bucket,
            string objectKey,
            IDictionary<string, string> headers,
            IReadOnlyDictionary<string, string> query,
            string additionalHeaders)
        {
            return string.Concat(
                method.ToUpperInvariant(), "\n",
                EncodePath("/" + bucket + "/" + objectKey), "\n",
                CanonicalizeQuery(query), "\n",
                CanonicalizeHeaders(headers, additionalHeaders), "\n",
                additionalHeaders, "\n",
                UnsignedPayload);
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
            if (string.IsNullOrWhiteSpace(customEndpoint))
            {
                return new Uri($"https://{bucket}.oss-{region}.aliyuncs.com");
            }

            var endpoint = new Uri(customEndpoint, UriKind.Absolute);
            // OSS 标准服务域名（*.aliyuncs.com）必须使用虚拟托管风格：bucket 要出现在 host 中。
            // 否则请求会打到不带 bucket 的服务域名，OSS 返回 404 NoSuchBucket。
            // 自定义 CNAME 域名（如通过 OSS 自定义域名绑定的 CDN 域名）保持原样。
            if (endpoint.Host.StartsWith(bucket + ".", StringComparison.OrdinalIgnoreCase) ||
                endpoint.Host.EndsWith(".aliyuncs.com", StringComparison.OrdinalIgnoreCase) is false)
            {
                return endpoint;
            }

            return new UriBuilder(endpoint)
            {
                Host = bucket + "." + endpoint.Host
            }.Uri;
        }

        private static string BuildEncodedQuery(IReadOnlyDictionary<string, string> query)
        {
            return string.Join("&", query
                .Select(pair => new KeyValuePair<string, string>(
                    EncodeValue(pair.Key),
                    EncodeValue(pair.Value ?? string.Empty)))
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
                    headers["x-oss-forbid-overwrite"] = "true";
                    return;
                case CloudWriteConditionKind.IfMatchETag:
                    headers["If-Match"] = condition.ETag;
                    return;
                default:
                    throw new CloudException(
                        CloudFailureKind.InvalidConfiguration,
                        "Alibaba OSS write condition is unsupported.",
                        CloudProviderId.AliyunOss);
            }
        }

        private static string CanonicalizeQuery(IReadOnlyDictionary<string, string> query)
        {
            if (query == null || query.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("&", query
                .Select(pair => new KeyValuePair<string, string>(
                    EncodeValue(pair.Key),
                    EncodeValue(pair.Value ?? string.Empty)))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Value.Length == 0
                    ? pair.Key
                    : pair.Key + "=" + pair.Value));
        }

        private static string CanonicalizeHeaders(
            IDictionary<string, string> headers,
            string additionalHeaders)
        {
            var additionalHeaderNames = new HashSet<string>(
                (additionalHeaders ?? string.Empty)
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.Ordinal);
            return string.Concat(headers
                .Where(pair => pair.Value != null &&
                               (IsDefaultSignedHeader(pair.Key) ||
                                additionalHeaderNames.Contains(pair.Key.ToLowerInvariant())))
                .Select(pair => new KeyValuePair<string, string>(
                    pair.Key.ToLowerInvariant(),
                    pair.Value.Trim()))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + ":" + pair.Value + "\n"));
        }

        private static bool IsDefaultSignedHeader(string name)
        {
            var normalized = name.ToLowerInvariant();
            return normalized == "content-type" ||
                   normalized == "content-md5" ||
                   normalized.StartsWith("x-oss-", StringComparison.Ordinal);
        }

        private static string[] GetOptionalSignedHeaderNames(IDictionary<string, string> headers)
        {
            return headers
                .Where(pair => pair.Value != null && IsDefaultSignedHeader(pair.Key) is false)
                .Select(pair => pair.Key)
                .ToArray();
        }

        private static string CanonicalizeAdditionalHeaderNames(
            IDictionary<string, string> headers,
            IReadOnlyCollection<string> additionalHeaderNames)
        {
            if (additionalHeaderNames == null || additionalHeaderNames.Count == 0)
            {
                return string.Empty;
            }

            var availableHeaders = new HashSet<string>(
                headers
                    .Where(pair => pair.Value != null)
                    .Select(pair => pair.Key.ToLowerInvariant()),
                StringComparer.Ordinal);
            var names = additionalHeaderNames
                .Select(name => name?.Trim().ToLowerInvariant())
                .Where(name => string.IsNullOrEmpty(name) is false &&
                               IsDefaultSignedHeader(name) is false)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            foreach (var name in names)
            {
                if (availableHeaders.Contains(name) is false)
                {
                    throw new ArgumentException(
                        $"Additional signed header '{name}' is not present in the request headers.",
                        nameof(additionalHeaderNames));
                }
            }

            return string.Join(";", names);
        }

        private static string CalculateSignature(
            string secret,
            string date,
            string region,
            string stringToSign)
        {
            var dateKey = HmacSha256(Encoding.UTF8.GetBytes("aliyun_v4" + secret), date);
            var regionKey = HmacSha256(dateKey, region);
            var productKey = HmacSha256(regionKey, "oss");
            var signingKey = HmacSha256(productKey, "aliyun_v4_request");
            return ToHex(HmacSha256(signingKey, stringToSign));
        }

        private static byte[] HmacSha256(byte[] key, string value)
        {
            using var algorithm = new HMACSHA256(key);
            return algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
        }

        private static string Sha256Hex(string value)
        {
            using var algorithm = SHA256.Create();
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

        private static string EncodePath(string value)
        {
            const string allowed = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~/";
            return Encode(value, allowed);
        }

        private static string EncodeValue(string value)
        {
            const string allowed = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";
            return Encode(value, allowed);
        }

        private static string Encode(string value, string allowed)
        {
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

        private static void ValidateRequired(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.IndexOf('/') >= 0 ||
                value.IndexOf('\\') >= 0 ||
                value.Any(char.IsControl))
            {
                throw new CloudException(
                    CloudFailureKind.InvalidConfiguration,
                    $"Alibaba OSS {field} is invalid.",
                    CloudProviderId.AliyunOss);
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
                    "Alibaba OSS endpoint must be an HTTPS origin without a path, query, or fragment.",
                    CloudProviderId.AliyunOss);
            }
        }
    }
}
