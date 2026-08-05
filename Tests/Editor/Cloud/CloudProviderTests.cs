using System;
using System.Collections.Generic;
using GameDeveloperKit.EditorCloud;
using NUnit.Framework;

namespace GameDeveloperKit.Tests.Cloud
{
    public sealed class CloudProviderTests
    {
        [Test]
        public void TencentCosProvider_CreatesExpectedEndpointHeadersAndSignature()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1557989753);
            var provider = new TencentCosProvider(() => now);
            var context = CreateContext(
                CloudProviderId.TencentCos,
                "examplebucket-1250000000",
                "ap-beijing",
                "folder/hello world.m3u8",
                "application/vnd.apple.mpegurl",
                new CloudCredential("AKIDEXAMPLE", "SecretKey", "session-token"));

            var request = provider.CreatePutObjectRequest(context);

            Assert.AreEqual(
                "https://examplebucket-1250000000.cos.ap-beijing.myqcloud.com/folder/hello%20world.m3u8",
                request.Uri.AbsoluteUri);
            Assert.AreEqual(
                "examplebucket-1250000000.cos.ap-beijing.myqcloud.com",
                request.Headers["Host"]);
            Assert.AreEqual("session-token", request.Headers["x-cos-security-token"]);
            Assert.AreEqual(
                "q-sign-algorithm=sha1&q-ak=AKIDEXAMPLE" +
                "&q-sign-time=1557989753;1557993353" +
                "&q-key-time=1557989753;1557993353" +
                "&q-header-list=content-type;host" +
                "&q-url-param-list=" +
                "&q-signature=e81a566784369cc4f4bae43cd697ac2505bfa9cf",
                request.Headers["Authorization"]);
        }

        [Test]
        public void AliyunOssProvider_MatchesOfficialV4AuthorizationVector()
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Date"] = "Sat, 16 Dec 2023 17:40:57 GMT",
                ["x-oss-head1"] = "value",
                ["abc"] = "value",
                ["ZAbc"] = "value",
                ["XYZ"] = "value",
                ["content-type"] = "text/plain",
                ["x-oss-content-sha256"] = "UNSIGNED-PAYLOAD"
            };
            var query = new Dictionary<string, string>
            {
                ["param1"] = "value1",
                ["+param1"] = "value3",
                ["|param1"] = "value4",
                ["+param2"] = string.Empty,
                ["|param2"] = string.Empty,
                ["param2"] = string.Empty
            };

            var authorization = AliyunOssProvider.CreateAuthorization(
                "bucket",
                "1234+-/123/1.txt",
                "cn-hangzhou",
                new CloudCredential("ak", "sk"),
                DateTimeOffset.FromUnixTimeSeconds(1702743657),
                headers,
                query);

            Assert.AreEqual(
                "OSS4-HMAC-SHA256 Credential=ak/20231216/cn-hangzhou/oss/aliyun_v4_request," +
                "Signature=e21d18daa82167720f9b1047ae7e7f1ce7cb77a31e8203a7d5f4624fa0284afe",
                authorization);
        }

        [Test]
        public void AliyunOssProvider_CreatesExpectedEndpointAndSignsSessionToken()
        {
            var provider = new AliyunOssProvider(() =>
                DateTimeOffset.FromUnixTimeSeconds(1702743657));
            var context = CreateContext(
                CloudProviderId.AliyunOss,
                "video-bucket",
                "cn-hangzhou",
                "folder/hello+world.ts",
                "video/mp2t",
                new CloudCredential("ak", "sk", "session-token"));

            var request = provider.CreatePutObjectRequest(context);

            Assert.AreEqual(
                "https://video-bucket.oss-cn-hangzhou.aliyuncs.com/folder/hello%2Bworld.ts",
                request.Uri.AbsoluteUri);
            Assert.AreEqual("UNSIGNED-PAYLOAD", request.Headers["x-oss-content-sha256"]);
            Assert.AreEqual("20231216T162057Z", request.Headers["x-oss-date"]);
            Assert.AreEqual("session-token", request.Headers["x-oss-security-token"]);
            StringAssert.StartsWith(
                "OSS4-HMAC-SHA256 Credential=ak/20231216/cn-hangzhou/oss/aliyun_v4_request,Signature=",
                request.Headers["Authorization"]);

            var unsignedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = "video/mp2t",
                ["x-oss-content-sha256"] = "UNSIGNED-PAYLOAD"
            };
            var authorizationWithoutToken = AliyunOssProvider.CreateAuthorization(
                context.Bucket,
                context.Request.ObjectKey,
                context.Region,
                new CloudCredential("ak", "sk"),
                DateTimeOffset.FromUnixTimeSeconds(1702743657),
                unsignedHeaders,
                new Dictionary<string, string>());
            Assert.AreNotEqual(authorizationWithoutToken, request.Headers["Authorization"]);
        }

        [Test]
        public void Providers_ParseEtagAndVendorRequestId()
        {
            var cosContext = CreateContext(
                CloudProviderId.TencentCos,
                "bucket-1250000000",
                "ap-chengdu",
                "videos/a.ts",
                "video/mp2t",
                new CloudCredential("ak", "sk"));
            var ossContext = CreateContext(
                CloudProviderId.AliyunOss,
                "bucket",
                "cn-hangzhou",
                "videos/a.ts",
                "video/mp2t",
                new CloudCredential("ak", "sk"));

            var cosResult = new TencentCosProvider().ParsePutObjectResponse(
                cosContext,
                new CloudHttpResponse(200, new Dictionary<string, string>
                {
                    ["ETag"] = "\"cos-etag\"",
                    ["x-cos-request-id"] = "cos-request"
                }, string.Empty));
            var ossResult = new AliyunOssProvider().ParsePutObjectResponse(
                ossContext,
                new CloudHttpResponse(200, new Dictionary<string, string>
                {
                    ["ETag"] = "\"oss-etag\"",
                    ["x-oss-request-id"] = "oss-request"
                }, string.Empty));

            Assert.AreEqual("\"cos-etag\"", cosResult.ETag);
            Assert.AreEqual("cos-request", cosResult.RequestId);
            Assert.AreEqual("\"oss-etag\"", ossResult.ETag);
            Assert.AreEqual("oss-request", ossResult.RequestId);
        }

        [Test]
        public void BuiltInRegistry_ContainsTencentCosAndAliyunOss()
        {
            var registry = CloudProviderRegistry.CreateBuiltIn();

            Assert.IsInstanceOf<TencentCosProvider>(registry.Resolve(CloudProviderId.TencentCos));
            Assert.IsInstanceOf<AliyunOssProvider>(registry.Resolve(CloudProviderId.AliyunOss));
        }

        [Test]
        public void TencentCosProvider_WhenCacheControlRequested_IncludesHeaderAndSignsIt()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1557989753);
            var provider = new TencentCosProvider(() => now);
            var context = CreateContext(
                CloudProviderId.TencentCos,
                "examplebucket-1250000000",
                "ap-beijing",
                "videos/catalog.json",
                "application/json",
                new CloudCredential("AKIDEXAMPLE", "SecretKey"),
                "no-cache");

            var request = provider.CreatePutObjectRequest(context);

            Assert.AreEqual("no-cache", request.Headers["Cache-Control"]);
            StringAssert.Contains("cache-control;", request.Headers["Authorization"]);
        }

        [Test]
        public void AliyunOssProvider_WhenCacheControlRequested_IncludesHeaderAndSignsIt()
        {
            var provider = new AliyunOssProvider(() =>
                DateTimeOffset.FromUnixTimeSeconds(1702743657));
            var context = CreateContext(
                CloudProviderId.AliyunOss,
                "video-bucket",
                "cn-hangzhou",
                "videos/catalog.json",
                "application/json",
                new CloudCredential("ak", "sk"),
                "no-cache");

            var request = provider.CreatePutObjectRequest(context);

            Assert.AreEqual("no-cache", request.Headers["Cache-Control"]);
            StringAssert.StartsWith(
                "OSS4-HMAC-SHA256 Credential=ak/20231216/cn-hangzhou/oss/aliyun_v4_request,Signature=",
                request.Headers["Authorization"]);
        }

        [TestCase("http://cos.ap-chengdu.myqcloud.com")]
        [TestCase("https://cos.ap-chengdu.myqcloud.com/videos")]
        public void TencentCosProvider_RejectsUnsafeCustomEndpoint(string endpoint)
        {
            var provider = new TencentCosProvider();
            var context = new CloudPutObjectContext(
                CloudProviderId.TencentCos,
                "bucket-1250000000",
                "ap-chengdu",
                endpoint,
                new CloudCredential("ak", "sk"),
                new CloudObjectUploadRequest("unused", "videos/a.ts", "video/mp2t"));

            var exception = Assert.Throws<CloudException>(() => provider.Validate(context));

            Assert.AreEqual(CloudFailureKind.InvalidConfiguration, exception.Kind);
        }

        [TestCase("https://oss-cn-chengdu.aliyuncs.com", "https://move-game-1.oss-cn-chengdu.aliyuncs.com/videos/1080P.meta")]
        [TestCase("https://move-game-1.oss-cn-chengdu.aliyuncs.com", "https://move-game-1.oss-cn-chengdu.aliyuncs.com/videos/1080P.meta")]
        [TestCase("https://media.example.com", "https://media.example.com/videos/1080P.meta")]
        public void AliyunOssProvider_WhenCustomEndpointProvided_BucketAlwaysAppearsInHost(
            string endpoint,
            string expectedUrl)
        {
            var provider = new AliyunOssProvider();
            var context = new CloudPutObjectContext(
                CloudProviderId.AliyunOss,
                "move-game-1",
                "cn-chengdu",
                endpoint,
                new CloudCredential("ak", "sk"),
                new CloudObjectUploadRequest("unused", "videos/1080P.meta", "application/octet-stream"));

            var request = provider.CreatePutObjectRequest(context);

            Assert.AreEqual(expectedUrl, request.Uri.AbsoluteUri);
        }

        [TestCase("https://cos.ap-chengdu.myqcloud.com", "https://move-game-1.cos.ap-chengdu.myqcloud.com/videos/1080P.meta")]
        [TestCase("https://move-game-1.cos.ap-chengdu.myqcloud.com", "https://move-game-1.cos.ap-chengdu.myqcloud.com/videos/1080P.meta")]
        [TestCase("https://media.example.com", "https://media.example.com/videos/1080P.meta")]
        public void TencentCosProvider_WhenCustomEndpointProvided_BucketAlwaysAppearsInHost(
            string endpoint,
            string expectedUrl)
        {
            var provider = new TencentCosProvider();
            var context = new CloudPutObjectContext(
                CloudProviderId.TencentCos,
                "move-game-1",
                "ap-chengdu",
                endpoint,
                new CloudCredential("ak", "sk"),
                new CloudObjectUploadRequest("unused", "videos/1080P.meta", "application/octet-stream"));

            var request = provider.CreatePutObjectRequest(context);

            Assert.AreEqual(expectedUrl, request.Uri.AbsoluteUri);
        }

        private static CloudPutObjectContext CreateContext(
            string providerId,
            string bucket,
            string region,
            string objectKey,
            string contentType,
            CloudCredential credential,
            string cacheControl = null)
        {
            return new CloudPutObjectContext(
                providerId,
                bucket,
                region,
                string.Empty,
                credential,
                new CloudObjectUploadRequest(
                    "unused",
                    objectKey,
                    contentType,
                    cacheControl: cacheControl));
        }
    }
}
