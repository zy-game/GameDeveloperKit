using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorCloud;
using GameDeveloperKit.EditorConfiguration;
using NUnit.Framework;
using UnityEngine.TestTools;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace GameDeveloperKit.Tests.Cloud
{
    public sealed class CloudTransportTests
    {
        private const string TempDirectory = "Library/GameDeveloperKit/Tests/CloudTransport";
        private string m_LocalFilePath;

        [SetUp]
        public void SetUp()
        {
            if (IODirectory.Exists(TempDirectory))
            {
                IODirectory.Delete(TempDirectory, true);
            }

            IODirectory.CreateDirectory(TempDirectory);
            m_LocalFilePath = IOPath.Combine(TempDirectory, "payload.bin");
            IOFile.WriteAllBytes(m_LocalFilePath, Enumerable.Range(0, 180000)
                .Select(index => (byte)(index % 251))
                .ToArray());
        }

        [TearDown]
        public void TearDown()
        {
            if (IODirectory.Exists(TempDirectory))
            {
                IODirectory.Delete(TempDirectory, true);
            }
        }

        [UnityTest]
        public IEnumerator HttpTransport_SendAsyncStreamsFileReportsProgressAndCapturesResponse()
        {
            var handler = new RecordingHttpHandler(async (request, cancellationToken) =>
            {
                await Task.Yield();
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("response-body")
                };
                response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"etag-value\"");
                response.Headers.TryAddWithoutValidation("x-request-id", "request-value");
                return response;
            });
            using var transport = new CloudHttpTransport(handler, TimeSpan.FromSeconds(5));
            var progress = new RecordingProgress();
            var upload = new CloudObjectUploadRequest(
                m_LocalFilePath,
                "videos/payload.bin",
                "application/octet-stream");

            var task = transport.SendAsync(
                    new CloudHttpRequest(
                        new Uri("https://storage.example.com/videos/payload.bin"),
                        new Dictionary<string, string> { ["x-test"] = "value" },
                        upload.ContentType),
                    upload,
                    progress,
                    CancellationToken.None)
                .AsTask();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            var response = task.GetAwaiter().GetResult();

            CollectionAssert.AreEqual(IOFile.ReadAllBytes(m_LocalFilePath), handler.Body);
            Assert.AreEqual("value", handler.HeaderValue);
            Assert.AreEqual(200, response.StatusCode);
            Assert.AreEqual("\"etag-value\"", response.GetHeader("ETag"));
            Assert.AreEqual("request-value", response.GetHeader("x-request-id"));
            Assert.AreEqual("response-body", response.Body);
            Assert.That(progress.Values.Count, Is.GreaterThan(1));
            Assert.AreEqual(handler.Body.Length, progress.Values.Last().ObjectBytesSent);
            Assert.IsTrue(progress.Values.Zip(
                progress.Values.Skip(1),
                (left, right) => right.ObjectBytesSent >= left.ObjectBytesSent).All(value => value));
        }

        [UnityTest]
        public IEnumerator HttpTransport_SendAsyncWhenCancelledThrowsOperationCanceledException()
        {
            var handler = new RecordingHttpHandler(async (request, cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
            using var transport = new CloudHttpTransport(handler, TimeSpan.FromSeconds(5));
            using var cancellation = new CancellationTokenSource();
            cancellation.CancelAfter(20);

            var task = transport.SendAsync(
                    new CloudHttpRequest(
                        new Uri("https://storage.example.com/videos/payload.bin"),
                        new Dictionary<string, string>(),
                        "application/octet-stream"),
                    new CloudObjectUploadRequest(
                        m_LocalFilePath,
                        "videos/payload.bin",
                        "application/octet-stream"),
                    null,
                    cancellation.Token)
                .AsTask();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            Assert.Catch<OperationCanceledException>(() => task.GetAwaiter().GetResult());
        }

        [Test]
        public void CloudService_RetriesNetworkAndRetryableStatusWithFreshSignedRequest()
        {
            var provider = new RecordingProvider();
            var transport = new SequenceTransport(
                new CloudException(CloudFailureKind.Network, "network"),
                new CloudHttpResponse(429, new Dictionary<string, string>(), string.Empty),
                new CloudHttpResponse(200, new Dictionary<string, string>(), string.Empty));
            var service = new CloudService(
                new CloudProviderRegistry().Register(provider),
                transport,
                (_, _) => UniTask.CompletedTask);

            var result = service.UploadObjectAsync(
                    CreateContext("videos/retry.bin"),
                    null,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual(3, transport.CallCount);
            Assert.AreEqual(3, provider.CreateRequestCallCount);
            Assert.AreEqual("videos/retry.bin", result.ObjectKey);
        }

        [TestCase(401, CloudFailureKind.Authentication)]
        [TestCase(403, CloudFailureKind.Permission)]
        [TestCase(400, CloudFailureKind.ProviderResponse)]
        public void CloudService_DoesNotRetryNonRetryableClientStatus(
            int statusCode,
            CloudFailureKind expectedKind)
        {
            var provider = new RecordingProvider();
            var transport = new SequenceTransport(
                new CloudHttpResponse(statusCode, new Dictionary<string, string>(), string.Empty));
            var service = new CloudService(
                new CloudProviderRegistry().Register(provider),
                transport,
                (_, _) => UniTask.CompletedTask);

            var exception = Assert.Throws<CloudException>(() => service.UploadObjectAsync(
                    CreateContext("videos/no-retry.bin"),
                    null,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult());

            Assert.AreEqual(expectedKind, exception.Kind);
            Assert.AreEqual(1, transport.CallCount);
            Assert.AreEqual(1, provider.CreateRequestCallCount);
        }

        [UnityTest]
        public IEnumerator CloudService_UploadBatchUsesBoundedConcurrencyAndMonotonicAggregateProgress()
        {
            var provider = new RecordingProvider();
            var transport = new DelayedTransport();
            var credentialPath = IOPath.Combine(TempDirectory, "credentials.json");
            var store = new CloudCredentialStore(credentialPath);
            store.Save(
                CloudProviderId.TencentCos,
                "publisher",
                new CloudCredential("access", "secret"));
            var config = new CloudProjectConfig
            {
                ProviderId = CloudProviderId.TencentCos,
                CredentialProfileName = "publisher",
                Bucket = "bucket",
                Region = "region"
            };
            var requests = new List<CloudObjectUploadRequest>();
            for (var i = 0; i < 9; i++)
            {
                var path = IOPath.Combine(TempDirectory, $"batch-{i}.bin");
                IOFile.WriteAllBytes(path, new byte[100 + i]);
                requests.Add(new CloudObjectUploadRequest(
                    path,
                    $"videos/batch-{i}.bin",
                    "application/octet-stream"));
            }

            var progress = new RecordingProgress();
            var service = new CloudService(
                new CloudProviderRegistry().Register(provider),
                transport,
                () => config,
                store,
                (_, _) => UniTask.CompletedTask);

            var task = service.UploadBatchAsync(
                    new CloudBatchUploadRequest(requests, 4),
                    progress,
                    CancellationToken.None)
                .AsTask();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            var result = task.GetAwaiter().GetResult();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(9, result.Succeeded.Count);
            Assert.That(transport.MaximumActive, Is.GreaterThan(1));
            Assert.That(transport.MaximumActive, Is.LessThanOrEqualTo(4));
            Assert.IsTrue(progress.Values.Zip(
                progress.Values.Skip(1),
                (left, right) => right.TotalBytesSent >= left.TotalBytesSent).All(value => value));
            Assert.AreEqual(progress.Values.Last().TotalBytes, progress.Values.Last().TotalBytesSent);
        }

        private CloudPutObjectContext CreateContext(string objectKey)
        {
            return new CloudPutObjectContext(
                CloudProviderId.TencentCos,
                "bucket",
                "region",
                string.Empty,
                new CloudCredential("access", "secret"),
                new CloudObjectUploadRequest(
                    m_LocalFilePath,
                    objectKey,
                    "application/octet-stream"));
        }

        private sealed class RecordingHttpHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> m_Handler;

            public RecordingHttpHandler(
                Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            {
                m_Handler = handler;
            }

            public byte[] Body { get; private set; }

            public string HeaderValue { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                HeaderValue = request.Headers.TryGetValues("x-test", out var values)
                    ? values.Single()
                    : string.Empty;
                if (request.Content != null)
                {
                    Body = await request.Content.ReadAsByteArrayAsync();
                }

                return await m_Handler(request, cancellationToken);
            }
        }

        private sealed class RecordingProvider : ICloudProvider
        {
            private int m_CreateRequestCallCount;

            public string ProviderId => CloudProviderId.TencentCos;

            public CloudProviderCapabilities Capabilities => CloudProviderCapabilities.PutObject;

            public int CreateRequestCallCount => m_CreateRequestCallCount;

            public void Validate(CloudPutObjectContext context)
            {
            }

            public CloudHttpRequest CreatePutObjectRequest(CloudPutObjectContext context)
            {
                Interlocked.Increment(ref m_CreateRequestCallCount);
                return new CloudHttpRequest(
                    new Uri("https://storage.example.com/" + context.Request.ObjectKey),
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
                    string.Empty,
                    string.Empty);
            }
        }

        private sealed class SequenceTransport : ICloudHttpTransport
        {
            private readonly Queue<object> m_Outcomes;

            public SequenceTransport(params object[] outcomes)
            {
                m_Outcomes = new Queue<object>(outcomes);
            }

            public int CallCount { get; private set; }

            public UniTask<CloudHttpResponse> SendAsync(
                CloudHttpRequest request,
                CloudObjectUploadRequest upload,
                IProgress<CloudUploadProgress> progress,
                CancellationToken cancellationToken)
            {
                CallCount++;
                var outcome = m_Outcomes.Dequeue();
                if (outcome is CloudException exception)
                {
                    throw exception;
                }

                return UniTask.FromResult((CloudHttpResponse)outcome);
            }
        }

        private sealed class DelayedTransport : ICloudHttpTransport
        {
            private int m_Active;
            private int m_MaximumActive;

            public int MaximumActive => m_MaximumActive;

            public async UniTask<CloudHttpResponse> SendAsync(
                CloudHttpRequest request,
                CloudObjectUploadRequest upload,
                IProgress<CloudUploadProgress> progress,
                CancellationToken cancellationToken)
            {
                var active = Interlocked.Increment(ref m_Active);
                UpdateMaximum(active);
                try
                {
                    await Task.Delay(25, cancellationToken);
                    var length = new System.IO.FileInfo(upload.LocalFilePath).Length;
                    progress?.Report(new CloudUploadProgress(
                        upload.ObjectKey,
                        length,
                        length,
                        length,
                        length));
                    return new CloudHttpResponse(
                        200,
                        new Dictionary<string, string>(),
                        string.Empty);
                }
                finally
                {
                    Interlocked.Decrement(ref m_Active);
                }
            }

            private void UpdateMaximum(int active)
            {
                while (true)
                {
                    var current = m_MaximumActive;
                    if (active <= current ||
                        Interlocked.CompareExchange(ref m_MaximumActive, active, current) == current)
                    {
                        return;
                    }
                }
            }
        }

        private sealed class RecordingProgress : IProgress<CloudUploadProgress>
        {
            private readonly object m_Gate = new object();
            private readonly List<CloudUploadProgress> m_Values = new List<CloudUploadProgress>();

            public IReadOnlyList<CloudUploadProgress> Values
            {
                get
                {
                    lock (m_Gate)
                    {
                        return m_Values.ToArray();
                    }
                }
            }

            public void Report(CloudUploadProgress value)
            {
                lock (m_Gate)
                {
                    m_Values.Add(value);
                }
            }
        }
    }
}
