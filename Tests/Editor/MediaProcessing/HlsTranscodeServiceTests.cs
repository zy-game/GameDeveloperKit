using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.MediaEditor;
using NUnit.Framework;
using UnityEngine.TestTools;
using IOFile = System.IO.File;

namespace GameDeveloperKit.Tests
{
    public sealed class HlsTranscodeServiceTests
    {
        private string m_Root;
        private string m_Input;
        private StubProbeService m_ProbeService;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(Path.GetTempPath(), "gdk-hls-service-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_Root);
            m_Input = Path.Combine(m_Root, "source.mp4");
            IOFile.WriteAllBytes(m_Input, new byte[] { 0 });
            m_ProbeService = new StubProbeService();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_Root))
            {
                Directory.Delete(m_Root, true);
            }
        }

        [UnityTest]
        public IEnumerator TranscodeAsync_WhenEncodingAndValidationSucceed_CommitsPackage()
        {
            var service = CreateService(new WritingProcessRunner(true));
            var request = new HlsTranscodeRequest(m_Input, "intro", RenditionsUpTo1080P());

            return UniTask.ToCoroutine(async () =>
            {
                var result = await service.TranscodeAsync(request, null, CancellationToken.None);

                Assert.IsTrue(IOFile.Exists(result.MasterPlaylistPath));
                Assert.IsTrue(IOFile.Exists(result.PreviewImagePath));
                Assert.AreEqual(12000, result.DurationMs);
                Assert.AreEqual(4, result.Renditions.Count);
                Assert.AreEqual("1080P", result.Renditions[0].Label);
                Assert.IsTrue(result.MasterPlaylistPath.Replace('\\', '/').EndsWith(
                    "Library/GameDeveloperKit/MediaProcessing/Hls/intro/master.m3u8",
                    StringComparison.Ordinal));
                var jobsRoot = Path.Combine(m_Root, HlsOutputTransaction.JobsRelativePath);
                Assert.IsTrue(Directory.Exists(jobsRoot));
                Assert.IsEmpty(Directory.GetDirectories(jobsRoot));
            });
        }

        [Test]
        public void TranscodeAsync_WhenEncodingFails_PreservesExistingTarget()
        {
            var target = Path.Combine(m_Root, "Library", "GameDeveloperKit", "MediaProcessing", "Hls", "intro");
            Directory.CreateDirectory(target);
            IOFile.WriteAllText(Path.Combine(target, "old.txt"), "old");
            var service = CreateService(new WritingProcessRunner(false));
            var request = new HlsTranscodeRequest(
                m_Input,
                "intro",
                RenditionsUpTo1080P(),
                overwriteExisting: true);

            var exception = Assert.Throws<InvalidDataException>(() =>
                service.TranscodeAsync(request, null, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult());

            StringAssert.Contains("退出码 1", exception.Message);
            Assert.IsTrue(IOFile.Exists(Path.Combine(target, "old.txt")));
            Assert.IsFalse(IOFile.Exists(Path.Combine(target, "master.m3u8")));
        }

        [Test]
        public void TranscodeAsync_WhenSelectionExceedsSourceBitrate_RejectsBeforeStaging()
        {
            m_ProbeService.SourceVideoBitrate = 5200000L;
            var processRunner = new WritingProcessRunner(true);
            var service = CreateService(processRunner);
            var selected = HlsRenditionPresets.Default.Single(preset => preset.Label == "2K");
            var request = new HlsTranscodeRequest(m_Input, "intro", new[] { selected });

            var exception = Assert.Throws<ArgumentException>(() =>
                service.TranscodeAsync(request, null, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult());

            StringAssert.Contains("2K", exception.Message);
            Assert.AreEqual(0, processRunner.RunCount);
            Assert.IsFalse(Directory.Exists(Path.Combine(
                m_Root,
                "Assets",
                "StreamingAssets",
                "videos",
                "intro")));
            Assert.IsFalse(Directory.Exists(Path.Combine(m_Root, HlsOutputTransaction.JobsRelativePath)));
        }

        private static HlsRenditionPreset[] RenditionsUpTo1080P()
        {
            return HlsRenditionPresets.Default
                .Where(preset => preset.Height <= 1080)
                .ToArray();
        }

        private HlsTranscodeService CreateService(IMediaProcessRunner processRunner)
        {
            var dependencies = new HlsTranscodeDependencies(m_Root)
            {
                ToolchainProvider = () => new FfmpegToolchainStatus(
                    FfmpegToolchainState.Ready,
                    FfmpegToolchainSource.Manual,
                    "ffmpeg",
                    "ffprobe",
                    "ready",
                    null),
                ProbeService = m_ProbeService,
                ProcessRunner = processRunner,
                OutputValidator = new HlsOutputValidator(m_ProbeService)
            };
            return new HlsTranscodeService(dependencies);
        }

        private sealed class StubProbeService : IMediaProbeService
        {
            public long SourceVideoBitrate { get; set; } = 16000000L;

            public UniTask<MediaProbeInfo> ProbeAsync(
                string ffprobePath,
                string inputPath,
                CancellationToken cancellationToken)
            {
                if (string.Equals(Path.GetExtension(inputPath), ".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    return UniTask.FromResult(new MediaProbeInfo(
                        1920,
                        1080,
                        12d,
                        30d,
                        SourceVideoBitrate,
                        true));
                }

                var label = new DirectoryInfo(Path.GetDirectoryName(inputPath)).Name;
                var dimensions = new Dictionary<string, int[]>
                {
                    ["1080P"] = new[] { 1920, 1080 },
                    ["720P"] = new[] { 1280, 720 },
                    ["480P"] = new[] { 854, 480 },
                    ["240P"] = new[] { 426, 240 }
                };
                return UniTask.FromResult(new MediaProbeInfo(
                    dimensions[label][0],
                    dimensions[label][1],
                    12d,
                    30d,
                    16000000L,
                    true));
            }
        }

        private sealed class WritingProcessRunner : IMediaProcessRunner
        {
            private readonly bool m_Succeed;

            public WritingProcessRunner(bool succeed)
            {
                m_Succeed = succeed;
            }

            public int RunCount { get; private set; }

            public UniTask<MediaProcessResult> RunAsync(
                MediaProcessRequest request,
                CancellationToken cancellationToken)
            {
                RunCount++;
                if (m_Succeed is false)
                {
                    return UniTask.FromResult(new MediaProcessResult(
                        1,
                        "ffmpeg",
                        string.Empty,
                        "injected failure",
                        TimeSpan.Zero));
                }

                if (request.Arguments.LastOrDefault()?.EndsWith(
                        HlsPreviewImage.FileName,
                        StringComparison.OrdinalIgnoreCase) == true)
                {
                    WritePreview(request.Arguments.Last());
                    return UniTask.FromResult(new MediaProcessResult(
                        0,
                        "ffmpeg-preview",
                        string.Empty,
                        string.Empty,
                        TimeSpan.Zero));
                }

                var labels = new[] { "1080P", "720P", "480P", "240P" };
                var widths = new[] { 1920, 1280, 854, 426 };
                var heights = new[] { 1080, 720, 480, 240 };
                var master = new List<string> { "#EXTM3U" };
                for (var i = 0; i < labels.Length; i++)
                {
                    var directory = Path.Combine(request.WorkingDirectory, labels[i]);
                    Directory.CreateDirectory(directory);
                    IOFile.WriteAllText(
                        Path.Combine(directory, "index.m3u8"),
                        "#EXTM3U\n#EXT-X-PLAYLIST-TYPE:VOD\n#EXTINF:6,\nsegment_00000.ts\n#EXT-X-ENDLIST\n");
                    IOFile.WriteAllBytes(Path.Combine(directory, "segment_00000.ts"), new byte[] { 0 });
                    master.Add($"#EXT-X-STREAM-INF:BANDWIDTH=1000000,RESOLUTION={widths[i]}x{heights[i]}");
                    master.Add(labels[i] + "/index.m3u8");
                }

                IOFile.WriteAllLines(Path.Combine(request.WorkingDirectory, "master.m3u8"), master);
                request.StandardOutputLine?.Invoke("out_time=00:00:12.000000");
                return UniTask.FromResult(new MediaProcessResult(
                    0,
                    "ffmpeg",
                    "progress=end",
                    string.Empty,
                    TimeSpan.FromSeconds(1)));
            }

            private static void WritePreview(string path)
            {
                IOFile.WriteAllBytes(path, new byte[]
                {
                    0xff, 0xd8,
                    0xff, 0xc0, 0x00, 0x11, 0x08,
                    0x01, 0x68,
                    0x02, 0x80,
                    0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x00, 0x03, 0x11, 0x00,
                    0xff, 0xd9
                });
            }
        }
    }
}
