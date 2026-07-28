using System.Linq;
using GameDeveloperKit.MediaEditor;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    public sealed class HlsRenditionEligibilityPolicyTests
    {
        [Test]
        public void Evaluate_WhenSourceIs2KAt5Point2Mbps_CapsAt1080P()
        {
            var result = Evaluate(2560, 1440, 5200000L);

            Assert.AreEqual("1080P", result.HighestEligiblePreset.Label);
            AssertEligibility(result, "4K", false, true, true);
            AssertEligibility(result, "2K", false, false, true);
            AssertEligibility(result, "1080P", true, false, false);
            CollectionAssert.AreEqual(
                HlsRenditionPresets.Default.Select(preset => preset.Label).ToArray(),
                result.Renditions.Select(rendition => rendition.Preset.Label).ToArray());
        }

        [Test]
        public void Evaluate_WhenSourceBitrateEquals2KPreset_Allows2K()
        {
            var result = Evaluate(2560, 1440, 6500000L);

            Assert.AreEqual("2K", result.HighestEligiblePreset.Label);
            AssertEligibility(result, "2K", true, false, false);
            AssertEligibility(result, "4K", false, true, true);
        }

        [Test]
        public void Evaluate_When1080PSourceHasHighBitrate_StillDoesNotUpscale()
        {
            var result = Evaluate(1920, 1080, 16000000L);

            Assert.AreEqual("1080P", result.HighestEligiblePreset.Label);
            AssertEligibility(result, "2K", false, true, false);
            AssertEligibility(result, "4K", false, true, false);
        }

        [Test]
        public void Evaluate_WhenSourceIsBelowLowestPreset_ReturnsNoEligiblePreset()
        {
            var result = Evaluate(1920, 1080, 349999L);

            Assert.IsNull(result.HighestEligiblePreset);
            Assert.IsTrue(result.Renditions.All(rendition => rendition.IsEligible is false));
        }

        [Test]
        public void Evaluate_WhenSourceHasNoAudio_UsesSameVideoEligibility()
        {
            var withAudio = Evaluate(1920, 1080, 4000000L, true);
            var withoutAudio = Evaluate(1920, 1080, 4000000L, false);

            CollectionAssert.AreEqual(
                withAudio.Renditions.Select(rendition => rendition.IsEligible).ToArray(),
                withoutAudio.Renditions.Select(rendition => rendition.IsEligible).ToArray());
        }

        private static HlsRenditionEligibilityResult Evaluate(
            int width,
            int height,
            long videoBitrate,
            bool hasAudio = true)
        {
            return HlsRenditionEligibilityPolicy.Evaluate(
                new MediaProbeInfo(width, height, 30d, 30d, videoBitrate, hasAudio),
                HlsRenditionPresets.Default);
        }

        private static void AssertEligibility(
            HlsRenditionEligibilityResult result,
            string label,
            bool eligible,
            bool exceedsHeight,
            bool exceedsBitrate)
        {
            var rendition = result.Renditions.Single(item => item.Preset.Label == label);
            Assert.AreEqual(eligible, rendition.IsEligible);
            Assert.AreEqual(exceedsHeight, rendition.ExceedsSourceHeight);
            Assert.AreEqual(exceedsBitrate, rendition.ExceedsSourceVideoBitrate);
        }
    }
}
