using System;
using System.IO;
using System.Linq;
using GameDeveloperKit.MediaEditor;
using NUnit.Framework;
using IOFile = System.IO.File;

namespace GameDeveloperKit.Tests
{
    public sealed class HlsTranscodePlannerTests
    {
        private string m_Root;
        private string m_Input;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(Path.GetTempPath(), "gdk-hls-plan-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_Root);
            m_Input = Path.Combine(m_Root, "source.mp4");
            IOFile.WriteAllBytes(m_Input, new byte[] { 0 });
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
        public void Create_WhenSourceIs4K_UsesSixDefaultRenditions()
        {
            var plan = CreatePlan(new MediaProbeInfo(3840, 2160, 30d, 30d, 16000000L, true));

            CollectionAssert.AreEqual(
                new[] { 2160, 1440, 1080, 720, 480, 240 },
                plan.Renditions.Select(rendition => rendition.Height).ToArray());
            CollectionAssert.AreEqual(
                new[] { 3840, 2560, 1920, 1280, 854, 426 },
                plan.Renditions.Select(rendition => rendition.Width).ToArray());
            CollectionAssert.AreEqual(
                new[] { "4K", "2K", "1080P", "720P", "480P", "240P" },
                plan.Renditions.Select(rendition => rendition.Label).ToArray());
            CollectionAssert.AreEqual(
                new[] { 16000000, 6500000, 4000000, 2000000, 1000000, 350000 },
                plan.Renditions.Select(rendition => rendition.VideoBitrate).ToArray());
            Assert.AreEqual(2, plan.Request.SegmentDurationSeconds);
            StringAssert.EndsWith("Library/GameDeveloperKit/MediaProcessing/Hls/intro", plan.OutputDirectory);
        }

        [Test]
        public void Create_WhenSourceIs1080P_DoesNotInclude4KOr2K()
        {
            var plan = CreatePlan(new MediaProbeInfo(1920, 1080, 30d, 30d, 16000000L, true));

            CollectionAssert.AreEqual(
                new[] { "1080P", "720P", "480P", "240P" },
                plan.Renditions.Select(rendition => rendition.Label).ToArray());
        }

        [Test]
        public void Create_WhenSourceIs720P_DoesNotUpscale()
        {
            var plan = CreatePlan(new MediaProbeInfo(1280, 720, 30d, 30d, 16000000L, true));

            CollectionAssert.AreEqual(
                new[] { 720, 480, 240 },
                plan.Renditions.Select(rendition => rendition.Height).ToArray());
        }

        [Test]
        public void Create_WhenSourceIsBelowAllPresets_RejectsInsteadOfAddingFallback()
        {
            var request = new HlsTranscodeRequest(m_Input, "intro", HlsRenditionPresets.Default);

            var exception = Assert.Throws<ArgumentException>(() => HlsTranscodePlanner.Create(
                request,
                new MediaProbeInfo(320, 180, 30d, 30d, 16000000L, true),
                m_Root));

            StringAssert.Contains("不可选", exception.Message);
        }

        [Test]
        public void Create_WhenSourceHasNoAudio_DisablesAudioForEveryRendition()
        {
            var plan = CreatePlan(new MediaProbeInfo(1920, 1080, 30d, 30d, 16000000L, false));

            Assert.IsTrue(plan.Renditions.All(rendition => rendition.AudioBitrate == 0));
        }

        [Test]
        public void Create_WhenPresetExceedsSourceBitrate_RejectsWithBitrateDetails()
        {
            var preset = HlsRenditionPresets.Default.Single(item => item.Label == "2K");
            var request = new HlsTranscodeRequest(m_Input, "intro", new[] { preset });

            var exception = Assert.Throws<ArgumentException>(() => HlsTranscodePlanner.Create(
                request,
                new MediaProbeInfo(2560, 1440, 30d, 30d, 5200000L, true),
                m_Root));

            StringAssert.Contains("2K", exception.Message);
            StringAssert.Contains("6500000", exception.Message);
            StringAssert.Contains("5200000", exception.Message);
        }

        [Test]
        public void Create_WhenPresetExceedsSourceHeight_RejectsEvenWithEnoughBitrate()
        {
            var preset = HlsRenditionPresets.Default.Single(item => item.Label == "2K");
            var request = new HlsTranscodeRequest(m_Input, "intro", new[] { preset });

            var exception = Assert.Throws<ArgumentException>(() => HlsTranscodePlanner.Create(
                request,
                new MediaProbeInfo(1920, 1080, 30d, 30d, 16000000L, true),
                m_Root));

            StringAssert.Contains("超过源分辨率", exception.Message);
        }

        [Test]
        public void Create_WhenLegalSubsetIsSelected_PreservesOnlyThatSubset()
        {
            var selected = HlsRenditionPresets.Default
                .Where(item => item.Label == "1080P" || item.Label == "720P" || item.Label == "480P")
                .ToArray();
            var request = new HlsTranscodeRequest(m_Input, "intro", selected);

            var plan = HlsTranscodePlanner.Create(
                request,
                new MediaProbeInfo(2560, 1440, 30d, 30d, 5200000L, true),
                m_Root);

            CollectionAssert.AreEqual(
                new[] { "1080P", "720P", "480P" },
                plan.Renditions.Select(rendition => rendition.Label).ToArray());
            Assert.IsTrue(plan.Renditions.All(rendition => rendition.VideoBitrate <= plan.Source.VideoBitrate));
        }

        [TestCase("../intro")]
        [TestCase("intro/other")]
        [TestCase("intro\\other")]
        [TestCase(".")]
        public void Create_WhenPackageNameIsUnsafe_Rejects(string packageName)
        {
            var request = new HlsTranscodeRequest(m_Input, packageName, HlsRenditionPresets.Default);

            Assert.Throws<ArgumentException>(() => HlsTranscodePlanner.Create(
                request,
                new MediaProbeInfo(1920, 1080, 30d, 30d, 16000000L, true),
                m_Root));
        }

        [Test]
        public void Create_WhenInputIsNotMp4_Rejects()
        {
            var input = Path.Combine(m_Root, "source.mov");
            IOFile.WriteAllBytes(input, new byte[] { 0 });
            var request = new HlsTranscodeRequest(input, "intro", HlsRenditionPresets.Default);

            Assert.Throws<ArgumentException>(() => HlsTranscodePlanner.Create(
                request,
                new MediaProbeInfo(1920, 1080, 30d, 30d, 16000000L, true),
                m_Root));
        }

        [TestCase("../1080P")]
        [TestCase("1080 P")]
        [TestCase("1080P,agroup:other")]
        public void RenditionPreset_WhenLabelIsUnsafe_Rejects(string label)
        {
            Assert.Throws<ArgumentException>(() => new HlsRenditionPreset(label, 1080, 5500000, 128000));
        }

        [Test]
        public void Create_WhenRenditionLabelsCollide_Rejects()
        {
            var request = new HlsTranscodeRequest(
                m_Input,
                "intro",
                new[]
                {
                    new HlsRenditionPreset("same", 1080, 5500000, 128000),
                    new HlsRenditionPreset("same", 720, 2800000, 128000)
                });

            Assert.Throws<ArgumentException>(() => HlsTranscodePlanner.Create(
                request,
                new MediaProbeInfo(1920, 1080, 30d, 30d, 16000000L, true),
                m_Root));
        }

        private HlsTranscodePlan CreatePlan(MediaProbeInfo source)
        {
            var renditions = HlsRenditionEligibilityPolicy
                .Evaluate(source, HlsRenditionPresets.Default)
                .Renditions
                .Where(rendition => rendition.IsEligible)
                .Select(rendition => rendition.Preset)
                .ToArray();
            var request = new HlsTranscodeRequest(m_Input, "intro", renditions);
            return HlsTranscodePlanner.Create(request, source, m_Root);
        }
    }
}
