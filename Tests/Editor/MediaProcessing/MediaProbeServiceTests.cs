using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.MediaEditor;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    public sealed class MediaProbeServiceTests
    {
        [Test]
        public void Parse_WhenVideoAndAudioExist_ReturnsNormalizedProbe()
        {
            const string json = "{\"streams\":["
                + "{\"codec_type\":\"video\",\"width\":1920,\"height\":1080,\"avg_frame_rate\":\"30000/1001\"},"
                + "{\"codec_type\":\"audio\"}],"
                + "\"format\":{\"duration\":\"12.500\"}}";

            var result = MediaProbeService.Parse(json);

            Assert.AreEqual(1920, result.Width);
            Assert.AreEqual(1080, result.Height);
            Assert.AreEqual(12.5d, result.DurationSeconds, 0.001d);
            Assert.AreEqual(29.97d, result.FrameRate, 0.01d);
            Assert.IsTrue(result.HasAudio);
        }

        [Test]
        public void Parse_WhenVideoHasNoAudio_ReturnsFalse()
        {
            const string json = "{\"streams\":["
                + "{\"codec_type\":\"video\",\"width\":640,\"height\":360,\"duration\":\"5\",\"r_frame_rate\":\"24/1\"}]}";

            var result = MediaProbeService.Parse(json);

            Assert.IsFalse(result.HasAudio);
            Assert.AreEqual(5d, result.DurationSeconds);
        }

        [Test]
        public void Parse_WhenVideoStreamIsMissing_Rejects()
        {
            const string json = "{\"streams\":[{\"codec_type\":\"audio\"}],\"format\":{\"duration\":\"1\"}}";

            var exception = Assert.Throws<System.IO.InvalidDataException>(() => MediaProbeService.Parse(json));

            StringAssert.Contains("不包含视频流", exception.Message);
        }

        [Test]
        public void ProbeAsync_WhenProcessFails_ReportsExitCode()
        {
            var runner = new StubProcessRunner(new MediaProcessResult(
                2,
                "ffprobe",
                string.Empty,
                "bad input",
                TimeSpan.Zero));
            var service = new MediaProbeService(runner);

            var exception = Assert.Throws<System.IO.InvalidDataException>(() =>
                service.ProbeAsync("ffprobe", "input.mp4", CancellationToken.None)
                    .GetAwaiter()
                    .GetResult());

            StringAssert.Contains("退出码 2", exception.Message);
        }

        private sealed class StubProcessRunner : IMediaProcessRunner
        {
            private readonly MediaProcessResult m_Result;

            public StubProcessRunner(MediaProcessResult result)
            {
                m_Result = result;
            }

            public UniTask<MediaProcessResult> RunAsync(
                MediaProcessRequest request,
                CancellationToken cancellationToken)
            {
                return UniTask.FromResult(m_Result);
            }
        }
    }
}
