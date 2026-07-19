using COSXML;
using COSXML.Auth;
using COSXML.CosException;
using COSXML.Model;
using COSXML.Model.Object;

namespace GameDeveloperKit.ResourceRelease.Cos;

public sealed partial class CosReleaseProvider
{
    private sealed class CosXmlGateway : ICosGateway
    {
        private const long CredentialDurationSeconds = 600;

        private readonly CosXml m_Client;
        private readonly string m_Bucket;

        internal CosXmlGateway(string secretId, string secretKey, string region, string bucket)
        {
            m_Bucket = bucket;
            var config = new CosXmlConfig.Builder()
                .SetRegion(region)
                .SetDebugLog(false)
                .Build();
            var credential = new DefaultQCloudCredentialProvider(
                secretId,
                secretKey,
                CredentialDurationSeconds);
            m_Client = new CosXmlServer(config, credential);
        }

        public CosObjectResult Head(string key)
        {
            try
            {
                var result = m_Client.HeadObject(new HeadObjectRequest(m_Bucket, key));
                return Result(result.size, result.eTag, result.responseHeaders);
            }
            catch (Exception exception)
            {
                throw Normalize(exception);
            }
        }

        public CosObjectResult PutFile(
            string key,
            string path,
            IReadOnlyDictionary<string, string> headers)
        {
            try
            {
                var request = new PutObjectRequest(m_Bucket, key, path);
                SetHeaders(request, headers);
                m_Client.PutObject(request);
                return Head(key);
            }
            catch (CosGatewayException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Normalize(exception);
            }
        }

        public CosObjectResult PutBytes(
            string key,
            byte[] bytes,
            IReadOnlyDictionary<string, string> headers)
        {
            try
            {
                var request = new PutObjectRequest(m_Bucket, key, bytes);
                SetHeaders(request, headers);
                m_Client.PutObject(request);
                return Head(key);
            }
            catch (CosGatewayException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw Normalize(exception);
            }
        }

        public byte[] GetBytes(string key)
        {
            try
            {
                return m_Client.GetObject(new GetObjectBytesRequest(m_Bucket, key)).content ??
                    Array.Empty<byte>();
            }
            catch (Exception exception)
            {
                throw Normalize(exception);
            }
        }

        private static CosObjectResult Result(
            long size,
            string? eTag,
            Dictionary<string, List<string>>? headers)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (headers != null)
            {
                foreach (var pair in headers)
                {
                    if (pair.Value?.Count > 0)
                    {
                        values[pair.Key] = pair.Value[0];
                    }
                }
            }
            return new CosObjectResult(size, eTag, values);
        }

        private static void SetHeaders(
            CosRequest request,
            IReadOnlyDictionary<string, string> headers)
        {
            foreach (var pair in headers)
            {
                request.SetRequestHeader(pair.Key, pair.Value);
            }
        }

        private static CosGatewayException Normalize(Exception exception)
        {
            return exception switch
            {
                CosServerException server => new CosGatewayException(
                    server.statusCode,
                    "COS server request failed.",
                    server),
                CosClientException client => new CosGatewayException(
                    0,
                    "COS client request failed.",
                    client),
                _ => new CosGatewayException(0, "COS request failed.", exception)
            };
        }
    }
}
