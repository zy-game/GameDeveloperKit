using System;
using System.IO;
using System.Linq;
using GameDeveloperKit.MediaEditor;
using NUnit.Framework;
using IOFile = System.IO.File;

namespace GameDeveloperKit.Tests
{
    public sealed class HlsFfmpegCommandBuilderTests
    {
        private string m_Root;
        private string m_Input;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(Path.GetTempPath(), "gdk-hls-command-" + Guid.NewGuid().ToString("N"));
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
        public void Build_WhenSourceHasAudio_MapsAlignedVideoAndAudioVariants()
        {
            var plan = CreatePlan(new MediaProbeInfo(1920, 1080, 30d, 30d, 16000000L, true));
            var arguments = HlsFfmpegCommandBuilder.Build(plan, Path.Combine(m_Root, "output"));

            Assert.AreEqual(4, arguments.Count(argument => argument == "0:a:0"));
            CollectionAssert.Contains(arguments, "expr:gte(t,n_forced*2)");
            CollectionAssert.Contains(
                arguments,
                "v:0,a:0,name:1080P v:1,a:1,name:720P v:2,a:2,name:480P v:3,a:3,name:240P");
            CollectionAssert.Contains(arguments, "-hls_playlist_type");
            CollectionAssert.Contains(arguments, "independent_segments");
            StringAssert.Contains("split=4", arguments[Array.IndexOf(arguments.ToArray(), "-filter_complex") + 1]);
        }

        [Test]
        public void Build_WhenSourceHasNoAudio_DoesNotMapOrEncodeAudio()
        {
            var plan = CreatePlan(new MediaProbeInfo(1280, 720, 30d, 24d, 16000000L, false));
            var arguments = HlsFfmpegCommandBuilder.Build(plan, Path.Combine(m_Root, "output"));

            Assert.IsFalse(arguments.Any(argument => argument == "0:a:0"));
            Assert.IsFalse(arguments.Any(argument => argument.StartsWith("-c:a:", StringComparison.Ordinal)));
            CollectionAssert.Contains(arguments, "v:0,name:720P v:1,name:480P v:2,name:240P");
            CollectionAssert.Contains(arguments, "48");
        }

        [Test]
        public void Build_WhenThreeLegalRenditionsAreSelected_MapsOnlyThoseRenditions()
        {
            var plan = CreatePlan(
                new MediaProbeInfo(2560, 1440, 30d, 30d, 5200000L, true),
                "1080P",
                "720P",
                "480P");

            var arguments = HlsFfmpegCommandBuilder.Build(plan, Path.Combine(m_Root, "output"));

            CollectionAssert.Contains(
                arguments,
                "v:0,a:0,name:1080P v:1,a:1,name:720P v:2,a:2,name:480P");
            StringAssert.Contains("split=3", arguments[Array.IndexOf(arguments.ToArray(), "-filter_complex") + 1]);
            Assert.IsFalse(arguments.Any(argument => argument.Contains("name:240P")));
        }

        [Test]
        public void PreviewArguments_UseBoundedSampleAndFixedJpegOutput()
        {
            var plan = CreatePlan(new MediaProbeInfo(1920, 1080, 80d, 30d, 16000000L, true));

            var arguments = HlsPreviewImage.BuildArguments(plan, Path.Combine(m_Root, "output"));

            Assert.AreEqual("5", arguments[Array.IndexOf(arguments.ToArray(), "-ss") + 1]);
            StringAssert.Contains("pad=640:360", arguments[Array.IndexOf(arguments.ToArray(), "-vf") + 1]);
            StringAssert.EndsWith("preview.jpg", arguments.Last());
        }

        private HlsTranscodePlan CreatePlan(MediaProbeInfo source, params string[] selectedLabels)
        {
            var eligible = HlsRenditionEligibilityPolicy
                .Evaluate(source, HlsRenditionPresets.Default)
                .Renditions
                .Where(rendition => rendition.IsEligible)
                .Select(rendition => rendition.Preset)
                .ToArray();
            var renditions = selectedLabels.Length == 0
                ? eligible
                : eligible.Where(preset => selectedLabels.Contains(preset.Label)).ToArray();
            var request = new HlsTranscodeRequest(m_Input, "intro", renditions);
            return HlsTranscodePlanner.Create(request, source, m_Root);
        }
    }
}
