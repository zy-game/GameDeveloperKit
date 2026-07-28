using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorCloud;
using NUnit.Framework;

namespace GameDeveloperKit.Tests.Cloud
{
    public sealed class CloudObjectManagementTests
    {
        [TestCase(CloudProviderId.TencentCos)]
        [TestCase(CloudProviderId.AliyunOss)]
        public void Provider_ListRequestSignsEncodedV2PaginationQuery(string providerId)
        {
            var provider = CreateProvider(providerId);
            var request = ((ICloudListProvider)provider).CreateListObjectsRequest(
                CreateListContext(providerId, "videos/media-a/", "next+token"));

            Assert.AreEqual(CloudHttpMethod.Get, request.Method);
            StringAssert.Contains("list-type=2", request.Uri.Query);
            StringAssert.Contains("prefix=videos%2Fmedia-a%2F", request.Uri.AbsoluteUri);
            StringAssert.Contains("continuation-token=next%2Btoken", request.Uri.AbsoluteUri);
            Assert.IsTrue(request.Headers.ContainsKey("Authorization"));
        }

        [TestCase(CloudProviderId.TencentCos)]
        [TestCase(CloudProviderId.AliyunOss)]
        public void Provider_DeleteRequestUsesSignedDeleteMethod(string providerId)
        {
            var provider = CreateProvider(providerId);
            var context = new CloudDeleteObjectContext(
                providerId,
                Bucket(providerId),
                Region(providerId),
                string.Empty,
                new CloudCredential("access", "secret", "session"),
                new CloudObjectDeleteRequest("videos/media-a/master.m3u8"));

            var request = ((ICloudDeleteProvider)provider).CreateDeleteObjectRequest(context);

            Assert.AreEqual(CloudHttpMethod.Delete, request.Method);
            StringAssert.EndsWith("/videos/media-a/master.m3u8", request.Uri.AbsolutePath);
            Assert.IsTrue(request.Headers.ContainsKey("Authorization"));
        }

        [TestCase(CloudProviderId.TencentCos)]
        [TestCase(CloudProviderId.AliyunOss)]
        public void Provider_ParsesNamespacedListResponseAndContinuation(string providerId)
        {
            var provider = CreateProvider(providerId);
            var context = CreateListContext(providerId, "videos/media-a/", string.Empty);
            var response = new CloudHttpResponse(
                200,
                new Dictionary<string, string>(),
                "<ListBucketResult xmlns=\"http://doc.example.com\">" +
                "<IsTruncated>true</IsTruncated>" +
                "<NextContinuationToken>next-token</NextContinuationToken>" +
                "<Contents><Key>videos/media-a/master.m3u8</Key><ETag>etag</ETag><Size>12</Size></Contents>" +
                "</ListBucketResult>");

            var page = ((ICloudListProvider)provider).ParseListObjectsResponse(context, response);

            Assert.IsTrue(page.IsTruncated);
            Assert.AreEqual("next-token", page.NextContinuationToken);
            Assert.AreEqual(1, page.Objects.Count);
            Assert.AreEqual("videos/media-a/master.m3u8", page.Objects[0].ObjectKey);
            Assert.AreEqual(12, page.Objects[0].Size);
        }

        [Test]
        public void CloudService_DeleteTreatsNotFoundAsIdempotentSuccess()
        {
            var transport = new RecordingObjectTransport(
                new CloudHttpResponse(404, new Dictionary<string, string>(), string.Empty));
            var service = new CloudService(
                new CloudProviderRegistry().Register(new TencentCosProvider()),
                transport,
                (_, _) => UniTask.CompletedTask);
            var context = new CloudDeleteObjectContext(
                CloudProviderId.TencentCos,
                Bucket(CloudProviderId.TencentCos),
                Region(CloudProviderId.TencentCos),
                string.Empty,
                new CloudCredential("access", "secret"),
                new CloudObjectDeleteRequest("videos/media-a/master.m3u8"));

            var result = service.DeleteObjectAsync(context, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.IsFalse(result.Existed);
            Assert.AreEqual(CloudHttpMethod.Delete, transport.LastRequest.Method);
            Assert.AreEqual(1, transport.CallCount);
        }

        [Test]
        public void CloudService_ListRejectsObjectOutsideRequestedPrefix()
        {
            var transport = new RecordingObjectTransport(
                new CloudHttpResponse(
                    200,
                    new Dictionary<string, string>(),
                    "<ListBucketResult><IsTruncated>false</IsTruncated>" +
                    "<Contents><Key>videos/other/master.m3u8</Key><ETag>etag</ETag><Size>12</Size></Contents>" +
                    "</ListBucketResult>"));
            var service = new CloudService(
                new CloudProviderRegistry().Register(new TencentCosProvider()),
                transport,
                (_, _) => UniTask.CompletedTask);

            var exception = Assert.Throws<CloudException>(() => service.ListObjectsAsync(
                    CreateListContext(CloudProviderId.TencentCos, "videos/media-a/", string.Empty),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult());

            Assert.AreEqual(CloudFailureKind.ProviderResponse, exception.Kind);
        }

        [TestCase("")]
        [TestCase("/")]
        public void CloudService_ListRejectsBucketRootPrefix(string prefix)
        {
            var transport = new RecordingObjectTransport(
                new CloudHttpResponse(200, new Dictionary<string, string>(), "<ListBucketResult />"));
            var service = new CloudService(
                new CloudProviderRegistry().Register(new TencentCosProvider()),
                transport,
                (_, _) => UniTask.CompletedTask);

            var exception = Assert.Throws<CloudException>(() => service.ListObjectsAsync(
                    CreateListContext(CloudProviderId.TencentCos, prefix, string.Empty),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult());

            Assert.AreEqual(CloudFailureKind.InvalidConfiguration, exception.Kind);
            Assert.AreEqual(0, transport.CallCount);
        }

        private static ICloudProvider CreateProvider(string providerId)
        {
            return providerId == CloudProviderId.TencentCos
                ? new TencentCosProvider()
                : new AliyunOssProvider();
        }

        private static CloudListObjectsContext CreateListContext(
            string providerId,
            string prefix,
            string continuationToken)
        {
            return new CloudListObjectsContext(
                providerId,
                Bucket(providerId),
                Region(providerId),
                string.Empty,
                new CloudCredential("access", "secret", "session"),
                new CloudObjectListRequest(prefix, continuationToken, 100));
        }

        private static string Bucket(string providerId)
        {
            return providerId == CloudProviderId.TencentCos
                ? "bucket-1250000000"
                : "bucket";
        }

        private static string Region(string providerId)
        {
            return providerId == CloudProviderId.TencentCos
                ? "ap-chengdu"
                : "cn-hangzhou";
        }

        private sealed class RecordingObjectTransport : ICloudHttpTransport, ICloudHttpReadTransport
        {
            private readonly CloudHttpResponse m_Response;

            public RecordingObjectTransport(CloudHttpResponse response)
            {
                m_Response = response;
            }

            public int CallCount { get; private set; }
            public CloudHttpRequest LastRequest { get; private set; }

            public UniTask<CloudHttpResponse> SendAsync(
                CloudHttpRequest request,
                CancellationToken cancellationToken)
            {
                CallCount++;
                LastRequest = request;
                return UniTask.FromResult(m_Response);
            }

            public UniTask<CloudHttpResponse> SendAsync(
                CloudHttpRequest request,
                CloudObjectUploadRequest upload,
                IProgress<CloudUploadProgress> progress,
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("Upload is not expected in this test.");
            }
        }
    }
}
