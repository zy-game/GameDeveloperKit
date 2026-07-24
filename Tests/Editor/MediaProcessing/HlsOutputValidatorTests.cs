using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.MediaEditor;
using NUnit.Framework;
using IOFile = System.IO.File;

namespace GameDeveloperKit.Tests
{
    public sealed class HlsOutputValidatorTests
    {
        private string m_Root;
        private string m_Input;
        private string m_Output;
        private HlsTranscodePlan m_Plan;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(Path.GetTempPath(), "gdk-hls-validate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_Root);
            m_Input = Path.Combine(m_Root, "source.mp4");
            IOFile.WriteAllBytes(m_Input, new byte[] { 0 });
            m_Output = Path.Combine(m_Root, "output");
            var request = new HlsTranscodeRequest(m_Input, "intro", HlsRenditionPresets.Default);
            m_Plan = HlsTranscodePlanner.Create(
                request,
                new MediaProbeInfo(1920, 1080, 12d, 30d, true),
                m_Root);
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
        public async Task ValidateAsync_WhenPackageIsComplete_ReturnsPlannedRenditions()
        {
            WriteValidPackage();
            var validator = new HlsOutputValidator(new StubProbeService(m_Plan));

            var result = await validator.ValidateAsync(
                m_Plan,
                m_Output,
                "ffprobe",
                CancellationToken.None);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("1080P", result[0].Label);
            Assert.AreEqual(1920, result[0].Width);
            Assert.Greater(result[0].Bitrate, 0);
        }

        [Test]
        public void ValidateAsync_WhenVariantSegmentIsMissing_RejectsPackage()
        {
            WriteValidPackage();
            IOFile.Delete(Path.Combine(m_Output, "720P", "segment_00000.ts"));
            var validator = new HlsOutputValidator(new StubProbeService(m_Plan));

            var exception = Assert.Throws<InvalidDataException>(() =>
                validator.ValidateAsync(m_Plan, m_Output, "ffprobe", CancellationToken.None)
                    .GetAwaiter()
                    .GetResult());

            StringAssert.Contains("不存在的切片", exception.Message);
        }

        [Test]
        public void ValidateAsync_WhenMasterPathEscapesPackage_RejectsPackage()
        {
            Directory.CreateDirectory(m_Output);
            IOFile.WriteAllText(
                Path.Combine(m_Output, "master.m3u8"),
                "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1,RESOLUTION=1920x1080\n../outside.m3u8\n");
            IOFile.WriteAllText(Path.Combine(m_Root, "outside.m3u8"), "#EXTM3U\n#EXT-X-ENDLIST\n");
            var validator = new HlsOutputValidator(new StubProbeService(m_Plan));

            var exception = Assert.Throws<InvalidDataException>(() =>
                validator.ValidateAsync(m_Plan, m_Output, "ffprobe", CancellationToken.None)
                    .GetAwaiter()
                    .GetResult());

            StringAssert.Contains("逃逸", exception.Message);
        }

        private void WriteValidPackage()
        {
            Directory.CreateDirectory(m_Output);
            var masterLines = new List<string> { "#EXTM3U" };
            for (var i = 0; i < m_Plan.Renditions.Count; i++)
            {
                var rendition = m_Plan.Renditions[i];
                var directory = Path.Combine(m_Output, rendition.Label);
                Directory.CreateDirectory(directory);
                IOFile.WriteAllText(
                    Path.Combine(directory, "index.m3u8"),
                    "#EXTM3U\n#EXT-X-PLAYLIST-TYPE:VOD\n#EXTINF:6.0,\nsegment_00000.ts\n#EXT-X-ENDLIST\n");
                IOFile.WriteAllBytes(Path.Combine(directory, "segment_00000.ts"), new byte[] { 0 });
                masterLines.Add(
                    $"#EXT-X-STREAM-INF:BANDWIDTH={rendition.VideoBitrate + rendition.AudioBitrate}," +
                    $"RESOLUTION={rendition.Width}x{rendition.Height}");
                masterLines.Add(rendition.PlaylistRelativePath);
            }

            IOFile.WriteAllLines(Path.Combine(m_Output, "master.m3u8"), masterLines);
        }

        private sealed class StubProbeService : IMediaProbeService
        {
            private readonly Dictionary<string, MediaProbeInfo> m_Results;

            public StubProbeService(HlsTranscodePlan plan)
            {
                m_Results = new Dictionary<string, MediaProbeInfo>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < plan.Renditions.Count; i++)
                {
                    var rendition = plan.Renditions[i];
                    m_Results[rendition.Label] = new MediaProbeInfo(
                        rendition.Width,
                        rendition.Height,
                        plan.Source.DurationSeconds,
                        plan.Source.FrameRate,
                        plan.Source.HasAudio);
                }
            }

            public UniTask<MediaProbeInfo> ProbeAsync(
                string ffprobePath,
                string inputPath,
                CancellationToken cancellationToken)
            {
                var label = new DirectoryInfo(Path.GetDirectoryName(inputPath)).Name;
                return UniTask.FromResult(m_Results[label]);
            }
        }
    }
}
