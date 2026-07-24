using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.MediaEditor;
using NUnit.Framework;
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

        [Test]
        public async Task TranscodeAsync_WhenEncodingAndValidationSucceed_CommitsPackage()
        {
            var service = CreateService(new WritingProcessRunner(true));
            var request = new HlsTranscodeRequest(m_Input, "intro", HlsRenditionPresets.Default);

            var result = await service.TranscodeAsync(request, null, CancellationToken.None);

            Assert.IsTrue(IOFile.Exists(result.MasterPlaylistPath));
            Assert.AreEqual(3, result.Renditions.Count);
            Assert.AreEqual("1080P", result.Renditions[0].Label);
            Assert.IsTrue(result.MasterPlaylistPath.Replace('\\', '/').EndsWith(
                "Assets/StreamingAssets/videos/intro/master.m3u8",
                StringComparison.Ordinal));
            var jobsRoot = Path.Combine(m_Root, HlsOutputTransaction.JobsRelativePath);
            Assert.IsTrue(Directory.Exists(jobsRoot));
            Assert.IsEmpty(Directory.GetDirectories(jobsRoot));
        }

        [Test]
        public void TranscodeAsync_WhenEncodingFails_PreservesExistingTarget()
        {
            var target = Path.Combine(m_Root, "Assets", "StreamingAssets", "videos", "intro");
            Directory.CreateDirectory(target);
            IOFile.WriteAllText(Path.Combine(target, "old.txt"), "old");
            var service = CreateService(new WritingProcessRunner(false));
            var request = new HlsTranscodeRequest(
                m_Input,
                "intro",
                HlsRenditionPresets.Default,
                overwriteExisting: true);

            var exception = Assert.Throws<InvalidDataException>(() =>
                service.TranscodeAsync(request, null, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult());

            StringAssert.Contains("退出码 1", exception.Message);
            Assert.IsTrue(IOFile.Exists(Path.Combine(target, "old.txt")));
            Assert.IsFalse(IOFile.Exists(Path.Combine(target, "master.m3u8")));
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
            public UniTask<MediaProbeInfo> ProbeAsync(
                string ffprobePath,
                string inputPath,
                CancellationToken cancellationToken)
            {
                if (string.Equals(Path.GetExtension(inputPath), ".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    return UniTask.FromResult(new MediaProbeInfo(1920, 1080, 12d, 30d, true));
                }

                var label = new DirectoryInfo(Path.GetDirectoryName(inputPath)).Name;
                var dimensions = new Dictionary<string, int[]>
                {
                    ["1080P"] = new[] { 1920, 1080 },
                    ["720P"] = new[] { 1280, 720 },
                    ["480P"] = new[] { 854, 480 }
                };
                return UniTask.FromResult(new MediaProbeInfo(
                    dimensions[label][0],
                    dimensions[label][1],
                    12d,
                    30d,
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

            public UniTask<MediaProcessResult> RunAsync(
                MediaProcessRequest request,
                CancellationToken cancellationToken)
            {
                if (m_Succeed is false)
                {
                    return UniTask.FromResult(new MediaProcessResult(
                        1,
                        "ffmpeg",
                        string.Empty,
                        "injected failure",
                        TimeSpan.Zero));
                }

                var labels = new[] { "1080P", "720P", "480P" };
                var widths = new[] { 1920, 1280, 854 };
                var heights = new[] { 1080, 720, 480 };
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
        }
    }
}
