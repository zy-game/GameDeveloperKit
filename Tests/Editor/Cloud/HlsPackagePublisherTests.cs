using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorCloud;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.MediaEditor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using IOFile = System.IO.File;

namespace GameDeveloperKit.Tests.Cloud
{
    public sealed class HlsPackagePublisherTests
    {
        private string m_Root;
        private HlsTranscodeResult m_Result;
        private CloudProjectConfig m_Config;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(Path.GetTempPath(), "gdk-hls-publish-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(m_Root, "720P"));
            IOFile.WriteAllText(
                Path.Combine(m_Root, "master.m3u8"),
                "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=2000000,RESOLUTION=1280x720\n720P/index.m3u8\n");
            IOFile.WriteAllText(
                Path.Combine(m_Root, "720P", "index.m3u8"),
                "#EXTM3U\n#EXTINF:2,\nsegment_00000.ts\n#EXT-X-ENDLIST\n");
            IOFile.WriteAllBytes(Path.Combine(m_Root, "720P", "segment_00000.ts"), new byte[] { 1, 2, 3 });
            var previewPath = Path.Combine(m_Root, "preview.jpg");
            IOFile.WriteAllBytes(previewPath, new byte[]
            {
                0xff, 0xd8,
                0xff, 0xc0, 0x00, 0x07, 0x08, 0x01, 0x68, 0x02, 0x80,
                0xff, 0xd9
            });
            m_Result = new HlsTranscodeResult(
                m_Root,
                Path.Combine(m_Root, "master.m3u8"),
                new[]
                {
                    new HlsRenditionInfo(
                        "720P",
                        1280,
                        720,
                        2000000,
                        Path.Combine(m_Root, "720P", "index.m3u8"))
                },
                string.Empty,
                string.Empty,
                previewPath,
                2000);
            m_Config = new CloudProjectConfig
            {
                ProviderId = CloudProviderId.TencentCos,
                CredentialProfileName = "publisher",
                Bucket = "bucket",
                Region = "region",
                RootPrefix = "videos"
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_Root))
            {
                Directory.Delete(m_Root, true);
            }
        }

        [Test]
        public void PublishAsync_UploadsContentAndPreviewThenMasterWithoutMetadata()
        {
            var transport = new RecordingTransport();
            var publisher = CreatePublisher(transport, "media-a");
            var originalFiles = SnapshotPackage();

            var result = publisher.PublishAsync(
                    new HlsPackagePublishRequest(m_Result, "Intro"),
                    null,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            var calls = transport.Calls.ToArray();
            var masterIndex = Array.IndexOf(calls, "videos/media-a/master.m3u8");
            Assert.That(masterIndex, Is.GreaterThan(0));
            Assert.That(Array.IndexOf(calls, "videos/media-a/720P/index.m3u8"), Is.LessThan(masterIndex));
            Assert.That(Array.IndexOf(calls, "videos/media-a/720P/segment_00000.ts"), Is.LessThan(masterIndex));
            Assert.That(Array.IndexOf(calls, "videos/media-a/preview.jpg"), Is.LessThan(masterIndex));
            Assert.AreEqual(masterIndex, calls.Length - 1);
            Assert.IsFalse(calls.Any(key => key.EndsWith("metadata.json", StringComparison.Ordinal)));
            Assert.IsFalse(calls.Any(key => key.EndsWith("manifest.json", StringComparison.Ordinal)));
            Assert.AreEqual("media-a", result.MediaId);
            Assert.AreEqual("videos/media-a/master.m3u8", result.MasterObjectKey);
            Assert.AreEqual(calls.Length, result.Uploads.Count);
            CollectionAssert.AreEquivalent(originalFiles, SnapshotPackage());
        }

        [TestCase("videos/media-fail/720P/segment_00000.ts")]
        [TestCase("videos/media-fail/master.m3u8")]
        public void PublishAsync_WhenContentOrMasterFails_DoesNotCompletePublish(string failedKey)
        {
            var transport = new RecordingTransport(failedKey);
            var publisher = CreatePublisher(transport, "media-fail");

            var exception = Assert.Throws<CloudException>(() => publisher.PublishAsync(
                    new HlsPackagePublishRequest(m_Result, "Intro"),
                    null,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult());

            StringAssert.Contains(
                failedKey.EndsWith("master.m3u8", StringComparison.Ordinal) ? "master" : "content",
                exception.Message);
            if (failedKey.EndsWith("master.m3u8", StringComparison.Ordinal) is false)
            {
                Assert.IsFalse(transport.Calls.Any(key => key.EndsWith("master.m3u8", StringComparison.Ordinal)));
            }
        }

        [Test]
        public void PublishAsync_WhenCancelledDuringContent_DoesNotUploadMaster()
        {
            using var cancellation = new CancellationTokenSource();
            var transport = new RecordingTransport(cancelOnFirstRequest: cancellation);
            var publisher = CreatePublisher(transport, "media-cancel");

            Assert.Catch<OperationCanceledException>(() => publisher.PublishAsync(
                    new HlsPackagePublishRequest(m_Result, "Intro"),
                    null,
                    cancellation.Token)
                .GetAwaiter()
                .GetResult());

            Assert.IsFalse(transport.Calls.Any(key => key.EndsWith("master.m3u8", StringComparison.Ordinal)));
        }

        [Test]
        public void PublishAsync_IgnoresLegacyMetadataAndManifestFiles()
        {
            IOFile.WriteAllText(Path.Combine(m_Root, "metadata.json"), "{}");
            IOFile.WriteAllText(Path.Combine(m_Root, "manifest.json"), "{}");
            var transport = new RecordingTransport();
            var publisher = CreatePublisher(transport, "media-legacy-files");

            publisher.PublishAsync(
                    new HlsPackagePublishRequest(m_Result, "Intro"),
                    null,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.IsFalse(transport.Calls.Any(key => key.EndsWith("metadata.json", StringComparison.Ordinal)));
            Assert.IsFalse(transport.Calls.Any(key => key.EndsWith("manifest.json", StringComparison.Ordinal)));
        }

        [Test]
        public void PublishAsync_TwoSameNamedPackagesUseDifferentMediaPrefixes()
        {
            var transport = new RecordingTransport();
            var ids = new Queue<string>(new[] { "media-user-a", "media-user-b" });
            var publisher = CreatePublisher(transport, () => ids.Dequeue());

            var first = publisher.PublishAsync(
                    new HlsPackagePublishRequest(m_Result, "Same Name"),
                    null,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            var second = publisher.PublishAsync(
                    new HlsPackagePublishRequest(m_Result, "Same Name"),
                    null,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.AreNotEqual(first.MediaId, second.MediaId);
            Assert.AreNotEqual(first.MasterObjectKey, second.MasterObjectKey);
        }

        [Test]
        public void HlsTranscodeWindow_OpenForPublishReappliesIntentAndFocusesWindow()
        {
            var source = IOFile.ReadAllText(FrameworkFilePath(
                "Editor/MediaProcessing/UI/HlsTranscodeWindow.cs"));
            var methodStart = source.IndexOf("internal static void OpenForPublish", StringComparison.Ordinal);
            var methodEnd = source.IndexOf("private void OnEnable", methodStart, StringComparison.Ordinal);
            var method = source.Substring(methodStart, methodEnd - methodStart);

            StringAssert.Contains("window.ApplyPublishIntent();", method);
            StringAssert.Contains("window.Show();", method);
            StringAssert.Contains("window.Focus();", method);
        }

        [Test]
        public void HlsTranscodeWindow_DoesNotExposeStandaloneMenu()
        {
            var source = IOFile.ReadAllText(FrameworkFilePath(
                "Editor/MediaProcessing/UI/HlsTranscodeWindow.cs"));

            StringAssert.DoesNotContain(
                "GameDeveloperKit/媒体/HLS 转码",
                source);
        }

        [Test]
        public void HlsTranscodeWindow_DoesNotExposeInputPicker()
        {
            var window = ScriptableObject.CreateInstance<HlsTranscodeWindow>();
            try
            {
                window.Show();
                typeof(HlsTranscodeWindow)
                    .GetMethod("BuildUi", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(window, null);

                Assert.IsFalse(window.rootVisualElement.Query<Button>()
                    .ToList()
                    .Any(button => button.text == "选择"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void HlsTranscodeWindow_HidesLegacyManualCloudPublishAction()
        {
            var window = ScriptableObject.CreateInstance<HlsTranscodeWindow>();
            try
            {
                window.Show();
                typeof(HlsTranscodeWindow)
                    .GetMethod("BuildUi", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(window, null);

                var button = window.rootVisualElement.Q<Button>("hls-publish-cloud-button");
                Assert.NotNull(button);
                Assert.IsFalse(button.enabledSelf);
                Assert.AreEqual(DisplayStyle.None, button.style.display.value);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void HlsTranscodeWindow_RenditionsUseFilledBorderButtonsAndKeepOneSelected()
        {
            var window = ScriptableObject.CreateInstance<HlsTranscodeWindow>();
            try
            {
                window.Show();
                typeof(HlsTranscodeWindow)
                    .GetMethod("BuildUi", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(window, null);
                var toggles = window.rootVisualElement.Query<Toggle>()
                    .ToList()
                    .Where(toggle => toggle.name.StartsWith("hls-rendition-", StringComparison.Ordinal))
                    .ToArray();

                Assert.AreEqual(HlsRenditionPresets.Default.Count, toggles.Length);
                Assert.That(toggles[0].style.backgroundColor.value.a, Is.GreaterThan(0f));
                Assert.AreEqual(
                    DisplayStyle.None,
                    toggles[0].Q<VisualElement>(className: "unity-toggle__input").style.display.value);
                foreach (var toggle in toggles)
                {
                    toggle.value = false;
                }

                Assert.AreEqual(1, toggles.Count(toggle => toggle.value));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private HlsPackagePublisher CreatePublisher(RecordingTransport transport, string mediaId)
        {
            return CreatePublisher(transport, () => mediaId);
        }

        private HlsPackagePublisher CreatePublisher(
            RecordingTransport transport,
            Func<string> mediaIdFactory)
        {
            var credentialPath = Path.Combine(m_Root, "credentials.json");
            var store = new CloudCredentialStore(credentialPath);
            store.Save(
                CloudProviderId.TencentCos,
                "publisher",
                new CloudCredential("access", "secret"));
            var service = new CloudService(
                new CloudProviderRegistry().Register(new RecordingProvider()),
                transport,
                () => m_Config,
                store,
                (_, _) => UniTask.CompletedTask);
            return new HlsPackagePublisher(service, () => m_Config, mediaIdFactory);
        }

        private static string FrameworkFilePath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "GameDeveloperKit",
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private string[] SnapshotPackage()
        {
            return Directory.GetFiles(m_Root, "*", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path) != "credentials.json")
                .Select(path => path.Substring(m_Root.Length).Replace('\\', '/') + ":" + Convert.ToBase64String(IOFile.ReadAllBytes(path)))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private sealed class RecordingProvider : ICloudProvider
        {
            public string ProviderId => CloudProviderId.TencentCos;
            public CloudProviderCapabilities Capabilities => CloudProviderCapabilities.PutObject;
            public void Validate(CloudPutObjectContext context) { }

            public CloudHttpRequest CreatePutObjectRequest(CloudPutObjectContext context)
            {
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

        private sealed class RecordingTransport : ICloudHttpTransport
        {
            private readonly object m_Gate = new object();
            private readonly string m_FailedKey;
            private readonly CancellationTokenSource m_CancelOnFirstRequest;
            private readonly List<string> m_Calls = new List<string>();

            public RecordingTransport(
                string failedKey = null,
                CancellationTokenSource cancelOnFirstRequest = null)
            {
                m_FailedKey = failedKey;
                m_CancelOnFirstRequest = cancelOnFirstRequest;
            }

            public IReadOnlyList<string> Calls
            {
                get
                {
                    lock (m_Gate)
                    {
                        return m_Calls.ToArray();
                    }
                }
            }

            public UniTask<CloudHttpResponse> SendAsync(
                CloudHttpRequest request,
                CloudObjectUploadRequest upload,
                IProgress<CloudUploadProgress> progress,
                CancellationToken cancellationToken)
            {
                lock (m_Gate)
                {
                    m_Calls.Add(upload.ObjectKey);
                }

                if (m_CancelOnFirstRequest != null)
                {
                    m_CancelOnFirstRequest.Cancel();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var status = string.Equals(upload.ObjectKey, m_FailedKey, StringComparison.Ordinal)
                    ? 403
                    : 200;
                return UniTask.FromResult(new CloudHttpResponse(
                    status,
                    new Dictionary<string, string>(),
                    string.Empty));
            }
        }
    }
}
