using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorCloud;
using NUnit.Framework;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace GameDeveloperKit.Tests.Cloud
{
    public sealed class CloudServiceTests
    {
        private const string TempDirectory = "Library/GameDeveloperKit/Tests/Cloud";
        private string m_LocalFilePath;

        [SetUp]
        public void SetUp()
        {
            IODirectory.CreateDirectory(TempDirectory);
            m_LocalFilePath = IOPath.Combine(TempDirectory, "route.txt");
            IOFile.WriteAllText(m_LocalFilePath, "cloud-route");
        }

        [TearDown]
        public void TearDown()
        {
            if (IODirectory.Exists(TempDirectory))
            {
                IODirectory.Delete(TempDirectory, true);
            }
        }

        [Test]
        public void UploadObjectAsync_RoutesTencentAndAliyunIdsToIsolatedProviders()
        {
            var cos = new RecordingProvider(CloudProviderId.TencentCos, "cos.example.com");
            var oss = new RecordingProvider(CloudProviderId.AliyunOss, "oss.example.com");
            var registry = new CloudProviderRegistry()
                .Register(cos)
                .Register(oss);
            var transport = new RecordingTransport();
            var service = new CloudService(registry, transport);

            var cosResult = service.UploadObjectAsync(
                    CreateContext(CloudProviderId.TencentCos),
                    null,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            var ossResult = service.UploadObjectAsync(
                    CreateContext(CloudProviderId.AliyunOss),
                    null,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual(1, cos.CreateRequestCallCount);
            Assert.AreEqual(1, oss.CreateRequestCallCount);
            Assert.AreEqual(2, transport.Requests.Count);
            Assert.AreEqual("cos.example.com", transport.Requests[0].Uri.Host);
            Assert.AreEqual("oss.example.com", transport.Requests[1].Uri.Host);
            Assert.AreEqual(CloudProviderId.TencentCos, cosResult.ProviderId);
            Assert.AreEqual(CloudProviderId.AliyunOss, ossResult.ProviderId);
        }

        [Test]
        public void UploadObjectAsync_WhenProviderUnknown_FailsBeforeTransport()
        {
            var transport = new RecordingTransport();
            var service = new CloudService(new CloudProviderRegistry(), transport);

            var exception = Assert.Throws<CloudException>(() => service.UploadObjectAsync(
                    CreateContext("missing-provider"),
                    null,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult());

            Assert.AreEqual(CloudFailureKind.InvalidConfiguration, exception.Kind);
            Assert.AreEqual(0, transport.Requests.Count);
        }

        [TestCase("https://cdn.example.com/video.ts")]
        [TestCase("/video.ts")]
        [TestCase("video\\segment.ts")]
        [TestCase("video//segment.ts")]
        [TestCase("video/./segment.ts")]
        [TestCase("video/../segment.ts")]
        public void UploadObjectAsync_WhenObjectKeyInvalid_FailsBeforeTransport(string objectKey)
        {
            var transport = new RecordingTransport();
            var registry = new CloudProviderRegistry()
                .Register(new RecordingProvider(CloudProviderId.TencentCos, "cos.example.com"));
            var service = new CloudService(registry, transport);
            var context = CreateContext(CloudProviderId.TencentCos, objectKey);

            var exception = Assert.Throws<CloudException>(() => service.UploadObjectAsync(
                    context,
                    null,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult());

            Assert.AreEqual(CloudFailureKind.InvalidConfiguration, exception.Kind);
            Assert.AreEqual(0, transport.Requests.Count);
        }

        [Test]
        public void Registry_WhenProviderIdDuplicated_RejectsSecondRegistration()
        {
            var registry = new CloudProviderRegistry()
                .Register(new RecordingProvider(CloudProviderId.TencentCos, "first.example.com"));

            var exception = Assert.Throws<CloudException>(() => registry.Register(
                new RecordingProvider(CloudProviderId.TencentCos, "second.example.com")));

            Assert.AreEqual(CloudFailureKind.InvalidConfiguration, exception.Kind);
        }

        [Test]
        public void UploadObjectAsync_WhenServerReturns404_MessageContainsBucketRegionAndEndpoint()
        {
            var registry = new CloudProviderRegistry()
                .Register(new RecordingProvider(CloudProviderId.TencentCos, "cos.example.com"));
            var transport = new FailingTransport(404);
            var service = new CloudService(registry, transport);
            var context = new CloudPutObjectContext(
                CloudProviderId.TencentCos,
                "move-game-1",
                "cn-chengdu",
                "https://oss-cn-chengdu.aliyuncs.com",
                new CloudCredential("access-key", "secret-key"),
                new CloudObjectUploadRequest(m_LocalFilePath, "videos/1080P.meta", "application/octet-stream"));

            var exception = Assert.Throws<CloudException>(() => service.UploadObjectAsync(
                    context,
                    null,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult());

            Assert.AreEqual(CloudFailureKind.NotFound, exception.Kind);
            StringAssert.Contains("bucket:move-game-1", exception.Message);
            StringAssert.Contains("region:cn-chengdu", exception.Message);
            StringAssert.Contains("endpoint:https://oss-cn-chengdu.aliyuncs.com", exception.Message);
            StringAssert.Contains("PUT 404", exception.Message);
        }

        private CloudPutObjectContext CreateContext(string providerId, string objectKey = "videos/route.txt")
        {
            return new CloudPutObjectContext(
                providerId,
                "bucket",
                "region",
                string.Empty,
                new CloudCredential("access-key", "secret-key"),
                new CloudObjectUploadRequest(m_LocalFilePath, objectKey, "text/plain"));
        }

        private sealed class RecordingProvider : ICloudProvider
        {
            private readonly string m_Host;

            public RecordingProvider(string providerId, string host)
            {
                ProviderId = providerId;
                m_Host = host;
            }

            public string ProviderId { get; }

            public CloudProviderCapabilities Capabilities => CloudProviderCapabilities.PutObject;

            public int CreateRequestCallCount { get; private set; }

            public void Validate(CloudPutObjectContext context)
            {
            }

            public CloudHttpRequest CreatePutObjectRequest(CloudPutObjectContext context)
            {
                CreateRequestCallCount++;
                return new CloudHttpRequest(
                    new Uri($"https://{m_Host}/{context.Request.ObjectKey}"),
                    new Dictionary<string, string>(),
                    context.Request.ContentType);
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
                    response.GetHeader("x-request-id"));
            }
        }

        private sealed class FailingTransport : ICloudHttpTransport
        {
            private readonly int m_StatusCode;

            public FailingTransport(int statusCode)
            {
                m_StatusCode = statusCode;
            }

            public UniTask<CloudHttpResponse> SendAsync(
                CloudHttpRequest request,
                CloudObjectUploadRequest upload,
                IProgress<CloudUploadProgress> progress,
                CancellationToken cancellationToken)
            {
                return UniTask.FromResult(new CloudHttpResponse(
                    m_StatusCode,
                    new Dictionary<string, string>(),
                    string.Empty));
            }
        }

        private sealed class RecordingTransport : ICloudHttpTransport
        {
            public List<CloudHttpRequest> Requests { get; } = new List<CloudHttpRequest>();

            public UniTask<CloudHttpResponse> SendAsync(
                CloudHttpRequest request,
                CloudObjectUploadRequest upload,
                IProgress<CloudUploadProgress> progress,
                CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return UniTask.FromResult(new CloudHttpResponse(
                    200,
                    new Dictionary<string, string>
                    {
                        ["ETag"] = "etag",
                        ["x-request-id"] = "request-id"
                    },
                    string.Empty));
            }
        }
    }
}
