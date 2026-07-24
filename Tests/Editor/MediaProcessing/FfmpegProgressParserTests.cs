using System;
using GameDeveloperKit.MediaEditor;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    public sealed class FfmpegProgressParserTests
    {
        [Test]
        public void TryParse_WhenOutTimeIsReported_ReturnsNormalizedProgress()
        {
            var parsed = FfmpegProgressParser.TryParse(
                "out_time=00:00:05.000000",
                20d,
                out var progress,
                out var processedTime);

            Assert.IsTrue(parsed);
            Assert.AreEqual(0.25f, progress, 0.001f);
            Assert.AreEqual(TimeSpan.FromSeconds(5), processedTime);
        }

        [Test]
        public void TryParse_WhenMicrosecondsExceedDuration_ClampsProgress()
        {
            var parsed = FfmpegProgressParser.TryParse(
                "out_time_us=25000000",
                20d,
                out var progress,
                out _);

            Assert.IsTrue(parsed);
            Assert.AreEqual(1f, progress);
        }

        [TestCase("progress=continue")]
        [TestCase("frame=30")]
        [TestCase("")]
        public void TryParse_WhenLineHasNoTime_ReturnsFalse(string line)
        {
            Assert.IsFalse(FfmpegProgressParser.TryParse(line, 20d, out _, out _));
        }
    }
}
