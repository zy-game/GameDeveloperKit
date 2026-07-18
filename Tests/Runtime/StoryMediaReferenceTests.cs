using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Protocol;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryMediaReferenceTests
    {
        [Test]
        public void VideoReferenceCodec_WhenCdnHls_RoundTripsAllFields()
        {
            var reference = new VideoReference(
                new MediaReference(
                    MediaKind.Video,
                    MediaSource.Cdn,
                    "intro-rain",
                    "https://cdn.example.com/story/intro/master.m3u8"),
                VideoFormat.Hls,
                new[]
                {
                    new VideoRendition(
                        "1080p",
                        "intro-rain",
                        "https://cdn.example.com/story/intro/1080.m3u8",
                        1920,
                        1080,
                        6000000,
                        92340)
                });

            var json = VideoReferenceCodec.Serialize(reference);
            var parsed = VideoReferenceCodec.TryDeserialize(json, out var restored, out var error);

            Assert.IsTrue(parsed, error);
            Assert.AreEqual(MediaKind.Video, restored.Primary.Kind);
            Assert.AreEqual(MediaSource.Cdn, restored.Primary.Source);
            Assert.AreEqual("intro-rain", restored.Primary.MediaId);
            Assert.AreEqual("https://cdn.example.com/story/intro/master.m3u8", restored.Primary.Location);
            Assert.AreEqual(VideoFormat.Hls, restored.Format);
            Assert.AreEqual(1, restored.Renditions.Count);
            Assert.AreEqual("1080p", restored.Renditions[0].Label);
            Assert.AreEqual(1920, restored.Renditions[0].Width);
            Assert.AreEqual(92340, restored.Renditions[0].DurationMs);
        }

        [Test]
        public void VideoReferenceCodec_WhenStreamingAssetsMp4_NormalizesSeparators()
        {
            var reference = new VideoReference(
                new MediaReference(MediaKind.Video, MediaSource.StreamingAssets, null, "story\\intro.mp4"),
                VideoFormat.Mp4);

            var json = VideoReferenceCodec.Serialize(reference);
            var parsed = VideoReferenceCodec.TryDeserialize(json, out var restored, out var error);

            Assert.IsTrue(parsed, error);
            Assert.AreEqual("story/intro.mp4", restored.Primary.Location);
            Assert.AreEqual(string.Empty, restored.Primary.MediaId);
        }

        [TestCase("http://cdn.example.com/video.mp4")]
        [TestCase("video.mp4")]
        [TestCase("https://user:password@cdn.example.com/video.mp4")]
        public void MediaReference_WhenCdnLocationIsInvalid_Throws(string location)
        {
            Assert.Throws<ArgumentException>(() =>
                new MediaReference(MediaKind.Video, MediaSource.Cdn, "video", location));
        }

        [TestCase("https://cdn.example.com/video.mp4")]
        [TestCase("/video.mp4")]
        [TestCase("../video.mp4")]
        [TestCase("story//video.mp4")]
        [TestCase("Assets/StreamingAssets/story/video.mp4")]
        public void MediaReference_WhenStreamingAssetsLocationIsInvalid_Throws(string location)
        {
            Assert.Throws<ArgumentException>(() =>
                new MediaReference(MediaKind.Video, MediaSource.StreamingAssets, null, location));
        }

        [Test]
        public void VideoReferenceCodec_WhenVersionIsUnsupported_ReturnsError()
        {
            var parsed = VideoReferenceCodec.TryDeserialize(
                "{\"version\":2,\"primary\":{\"kind\":\"video\",\"source\":\"cdn\",\"mediaId\":\"video\",\"location\":\"https://cdn.example.com/video.mp4\"},\"format\":\"mp4\",\"renditions\":[]}",
                out var reference,
                out var error);

            Assert.IsFalse(parsed);
            Assert.IsNull(reference);
            StringAssert.Contains("unsupported", error);
        }

        [Test]
        public void VideoReference_WhenFormatDoesNotMatchLocation_Throws()
        {
            var primary = new MediaReference(
                MediaKind.Video,
                MediaSource.Cdn,
                "video",
                "https://cdn.example.com/video.mp4?revision=1");

            Assert.Throws<ArgumentException>(() => new VideoReference(primary, VideoFormat.Hls));
        }

        [Test]
        public void VideoReference_WhenMp4HasAdditionalRendition_Throws()
        {
            var primary = new MediaReference(
                MediaKind.Video,
                MediaSource.Cdn,
                "video",
                "https://cdn.example.com/video.mp4");
            var rendition = new VideoRendition(
                "1080p",
                "video-1080",
                "https://cdn.example.com/video-1080.mp4",
                1920,
                1080,
                6000000,
                90000);

            Assert.Throws<ArgumentException>(() => new VideoReference(primary, VideoFormat.Mp4, new[] { rendition }));
        }

        [Test]
        public void VideoReferenceCodec_WhenCompiledCdnCommand_ValidatesAllArguments()
        {
            var renditions = new[]
            {
                new VideoRendition("1080p", "video", "https://cdn.example.com/1080.m3u8", 1920, 1080, 6000000, 90000)
            };
            var arguments = new ArgumentBag(new Dictionary<string, Value>
            {
                [MediaCommandNames.MediaSourceArgument] = Value.FromString(MediaCommandNames.VideoSourceCdn),
                [MediaCommandNames.MediaIdArgument] = Value.FromString("video"),
                [MediaCommandNames.ClipArgument] = Value.FromString("https://cdn.example.com/master.m3u8"),
                [MediaCommandNames.VideoFormatArgument] = Value.FromString("hls"),
                [MediaCommandNames.VideoRenditionsArgument] = Value.FromString(VideoReferenceCodec.SerializeRenditions(renditions))
            });

            var parsed = VideoReferenceCodec.TryDeserializeCommand(arguments, out var reference, out var legacy, out var error);

            Assert.IsTrue(parsed, error);
            Assert.IsFalse(legacy);
            Assert.AreEqual(MediaSource.Cdn, reference.Primary.Source);
            Assert.AreEqual("video", reference.Primary.MediaId);
            Assert.AreEqual(1, reference.Renditions.Count);
        }

        [Test]
        public void VideoReferenceCodec_WhenLegacyStreamingCommand_ReadsWithMigrationFlag()
        {
            var arguments = new ArgumentBag(new Dictionary<string, Value>
            {
                [MediaCommandNames.VideoSourceArgument] = Value.FromString(MediaCommandNames.VideoSourceStreamingAssets),
                [MediaCommandNames.ClipArgument] = Value.FromString("story/intro.mp4")
            });

            var parsed = VideoReferenceCodec.TryDeserializeCommand(arguments, out var reference, out var legacy, out var error);

            Assert.IsTrue(parsed, error);
            Assert.IsTrue(legacy);
            Assert.AreEqual(MediaSource.StreamingAssets, reference.Primary.Source);
        }

        [TestCase(MediaCommandNames.VideoSourcePersistentDataPath)]
        [TestCase(MediaCommandNames.VideoSourceNetworkStream)]
        public void VideoReferenceCodec_WhenLegacySourceUnsupported_ReturnsError(string source)
        {
            var arguments = new ArgumentBag(new Dictionary<string, Value>
            {
                [MediaCommandNames.VideoSourceArgument] = Value.FromString(source),
                [MediaCommandNames.ClipArgument] = Value.FromString("story/intro.mp4")
            });

            Assert.IsFalse(VideoReferenceCodec.TryDeserializeCommand(arguments, out _, out _, out var error));
            StringAssert.Contains("unsupported", error);
        }
    }
}
