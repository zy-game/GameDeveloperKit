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
            var plan = CreatePlan(new MediaProbeInfo(1920, 1080, 30d, 30d, true));
            var arguments = HlsFfmpegCommandBuilder.Build(plan, Path.Combine(m_Root, "output"));

            Assert.AreEqual(3, arguments.Count(argument => argument == "0:a:0"));
            CollectionAssert.Contains(arguments, "expr:gte(t,n_forced*2)");
            CollectionAssert.Contains(arguments, "v:0,a:0,name:1080P v:1,a:1,name:720P v:2,a:2,name:480P");
            CollectionAssert.Contains(arguments, "-hls_playlist_type");
            CollectionAssert.Contains(arguments, "independent_segments");
            StringAssert.Contains("split=3", arguments[Array.IndexOf(arguments.ToArray(), "-filter_complex") + 1]);
        }

        [Test]
        public void Build_WhenSourceHasNoAudio_DoesNotMapOrEncodeAudio()
        {
            var plan = CreatePlan(new MediaProbeInfo(1280, 720, 30d, 24d, false));
            var arguments = HlsFfmpegCommandBuilder.Build(plan, Path.Combine(m_Root, "output"));

            Assert.IsFalse(arguments.Any(argument => argument == "0:a:0"));
            Assert.IsFalse(arguments.Any(argument => argument.StartsWith("-c:a:", StringComparison.Ordinal)));
            CollectionAssert.Contains(arguments, "v:0,name:720P v:1,name:480P");
            CollectionAssert.Contains(arguments, "48");
        }

        private HlsTranscodePlan CreatePlan(MediaProbeInfo source)
        {
            var request = new HlsTranscodeRequest(m_Input, "intro", HlsRenditionPresets.Default);
            return HlsTranscodePlanner.Create(request, source, m_Root);
        }
    }
}
