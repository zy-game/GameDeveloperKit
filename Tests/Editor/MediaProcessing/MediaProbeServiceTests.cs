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
                + "{\"codec_type\":\"video\",\"width\":1920,\"height\":1080,\"avg_frame_rate\":\"30000/1001\",\"bit_rate\":\"5200000\"},"
                + "{\"codec_type\":\"audio\"}],"
                + "\"format\":{\"duration\":\"12.500\"}}";

            var result = MediaProbeService.Parse(json);

            Assert.AreEqual(1920, result.Width);
            Assert.AreEqual(1080, result.Height);
            Assert.AreEqual(12.5d, result.DurationSeconds, 0.001d);
            Assert.AreEqual(29.97d, result.FrameRate, 0.01d);
            Assert.AreEqual(5200000L, result.VideoBitrate);
            Assert.IsTrue(result.HasAudio);
        }

        [Test]
        public void Parse_WhenVideoHasNoAudio_ReturnsFalse()
        {
            const string json = "{\"streams\":["
                + "{\"codec_type\":\"video\",\"width\":640,\"height\":360,\"duration\":\"5\",\"r_frame_rate\":\"24/1\",\"bit_rate\":\"800000\"}]}";

            var result = MediaProbeService.Parse(json);

            Assert.IsFalse(result.HasAudio);
            Assert.AreEqual(5d, result.DurationSeconds);
        }

        [TestCase(null)]
        [TestCase("0")]
        [TestCase("-1")]
        public void Parse_WhenVideoBitrateIsNotPositive_Rejects(string bitrate)
        {
            var bitrateProperty = bitrate == null ? string.Empty : ",\"bit_rate\":\"" + bitrate + "\"";
            var json = "{\"streams\":[{\"codec_type\":\"video\",\"width\":1920,\"height\":1080,"
                + "\"duration\":\"5\",\"r_frame_rate\":\"30/1\"" + bitrateProperty + "}]}";

            var exception = Assert.Throws<System.IO.InvalidDataException>(() => MediaProbeService.Parse(json));

            StringAssert.Contains("bit_rate", exception.Message);
        }

        [Test]
        public void Parse_WhenOnlyFormatBitrateExists_UsesContainerBitrate()
        {
            const string json = "{\"streams\":[{\"codec_type\":\"video\",\"width\":1920,\"height\":1080,"
                + "\"duration\":\"5\",\"r_frame_rate\":\"30/1\"}],"
                + "\"format\":{\"duration\":\"5\",\"bit_rate\":\"16000000\"}}";

            var result = MediaProbeService.Parse(json);

            Assert.AreEqual(16000000L, result.VideoBitrate);
        }

        [Test]
        public void Parse_WhenVideoBitrateIsMissing_SubtractsKnownAudioBitrateFromContainer()
        {
            const string json = "{\"streams\":["
                + "{\"codec_type\":\"video\",\"width\":1920,\"height\":1080,\"duration\":\"5\",\"r_frame_rate\":\"30/1\"},"
                + "{\"codec_type\":\"audio\",\"bit_rate\":\"192000\"}],"
                + "\"format\":{\"duration\":\"5\",\"bit_rate\":\"16000000\"}}";

            var result = MediaProbeService.Parse(json);

            Assert.AreEqual(15808000L, result.VideoBitrate);
            Assert.IsTrue(result.HasAudio);
        }

        [Test]
        public void Parse_WhenBitratesAreMissing_EstimatesFromContainerSizeAndDuration()
        {
            const string json = "{\"streams\":[{\"codec_type\":\"video\",\"width\":1920,\"height\":1080,"
                + "\"duration\":\"5\",\"r_frame_rate\":\"30/1\"}],"
                + "\"format\":{\"duration\":\"5\",\"size\":\"10000000\"}}";

            var result = MediaProbeService.Parse(json);

            Assert.AreEqual(16000000L, result.VideoBitrate);
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
