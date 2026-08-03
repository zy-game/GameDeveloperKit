using System;
using System.Collections.Generic;
using GameDeveloperKit.Media;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Model;
using GameDeveloperKit.Story.Playback;
using GameDeveloperKit.Story.Protocol;
using NUnit.Framework;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryMediaReferenceTests
    {
        private MediaDeliverySettings m_Settings;

        [SetUp]
        public void SetUp()
        {
            m_Settings = ScriptableObject.CreateInstance<MediaDeliverySettings>();
            m_Settings.SetPublicUrls("https://origin.example.com", "https://cdn.example.com");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(m_Settings);
        }

        [Test]
        public void VideoReferenceCodec_WhenCurrentHls_RoundTripsRelativePathsOnly()
        {
            var reference = new VideoReference(
                new MediaPath("videos/story/intro/master.m3u8"),
                VideoFormat.Hls,
                new[]
                {
                    new VideoRendition(
                        "1080P",
                        new MediaPath("videos/story/intro/1080P/index.m3u8"),
                        1920,
                        1080,
                        6000000,
                        92340)
                });

            var json = VideoReferenceCodec.Serialize(reference);
            var parsed = VideoReferenceCodec.TryDeserialize(json, out var restored, out var error);

            Assert.IsTrue(parsed, error);
            Assert.AreEqual("videos/story/intro/master.m3u8", restored.Primary.Value);
            Assert.AreEqual(VideoFormat.Hls, restored.Format);
            Assert.AreEqual(1, restored.Renditions.Count);
            Assert.AreEqual("videos/story/intro/1080P/index.m3u8", restored.Renditions[0].Path.Value);
            StringAssert.DoesNotContain("mediaId", json);
            StringAssert.DoesNotContain("mediaSource", json);
            StringAssert.DoesNotContain("location", json);
        }

        [Test]
        public void VideoReferenceCodec_WhenVersionOneSchemaIsProvided_ReturnsError()
        {
            var parsed = VideoReferenceCodec.TryDeserialize(
                "{\"version\":1,\"primary\":{\"location\":\"https://cdn.example.com/video.m3u8\"},\"format\":\"hls\",\"renditions\":[]}",
                out var reference,
                out var error);

            Assert.IsFalse(parsed);
            Assert.IsNull(reference);
            StringAssert.Contains("unsupported", error);
        }

        [Test]
        public void AudioReferenceCodec_WhenCdnReferenceRoundTrips_StoresRelativePathOnly()
        {
            var reference = new MediaReference(MediaKind.Audio, MediaSource.Cdn, "audio/story/theme.ogg");

            var json = AudioReferenceCodec.Serialize(reference);
            var parsed = AudioReferenceCodec.TryDeserialize(json, out var restored, out var error);

            Assert.IsTrue(parsed, error);
            Assert.AreEqual(MediaSource.Cdn, restored.Source);
            Assert.AreEqual("audio/story/theme.ogg", restored.Location);
            StringAssert.DoesNotContain("mediaId", json);
            StringAssert.DoesNotContain("https://", json);
        }

        [Test]
        public void AudioReferenceCodec_WhenVersionOneSchemaIsProvided_ReturnsError()
        {
            var parsed = AudioReferenceCodec.TryDeserialize(
                "{\"version\":1,\"source\":\"cdn\",\"mediaId\":\"theme\",\"location\":\"https://cdn.example.com/theme.ogg\"}",
                out _,
                out var error);

            Assert.IsFalse(parsed);
            StringAssert.Contains("unsupported", error);
        }

        [Test]
        public void VideoReferenceCodec_WhenLegacyVideoCommandIsProvided_ReturnsError()
        {
            var arguments = new ArgumentBag(new Dictionary<string, Value>
            {
                ["mediaSource"] = Value.FromString("streaming_assets"),
                [MediaCommandNames.ClipArgument] = Value.FromString("videos/story/intro.mp4")
            });

            Assert.IsFalse(VideoReferenceCodec.TryDeserializeCommand(arguments, out _, out var error));
            StringAssert.Contains("format", error);
        }

        [Test]
        public void VideoReferenceCodec_WhenCurrentCommandIsProvided_DeserializesRelativePaths()
        {
            var renditions = new[]
            {
                new VideoRendition(
                    "1080P",
                    new MediaPath("videos/story/intro/1080P/index.m3u8"),
                    1920,
                    1080,
                    6000000,
                    90000)
            };
            var arguments = new ArgumentBag(new Dictionary<string, Value>
            {
                [MediaCommandNames.ClipArgument] = Value.FromString("videos/story/intro/master.m3u8"),
                [MediaCommandNames.VideoFormatArgument] = Value.FromString("hls"),
                [MediaCommandNames.VideoRenditionsArgument] = Value.FromString(VideoReferenceCodec.SerializeRenditions(renditions))
            });

            var parsed = VideoReferenceCodec.TryDeserializeCommand(arguments, out var reference, out var error);

            Assert.IsTrue(parsed, error);
            Assert.AreEqual("videos/story/intro/master.m3u8", reference.Primary.Value);
            Assert.AreEqual("videos/story/intro/1080P/index.m3u8", reference.Renditions[0].Path.Value);
        }

        [Test]
        public void VideoReference_WhenFormatDoesNotMatchPath_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new VideoReference(new MediaPath("videos/story/intro.mp4"), VideoFormat.Hls));
        }

        [Test]
        public void VideoReference_WhenMp4HasValidRenditions_AcceptsPrimaryAndAdditionalPaths()
        {
            var primary = new MediaPath("videos/story/intro.mp4");
            var reference = new VideoReference(
                primary,
                VideoFormat.Mp4,
                new[]
                {
                    new VideoRendition("1080P", primary, 1920, 1080, 6000000, 90000),
                    new VideoRendition(
                        "720P",
                        new MediaPath("videos/story/intro-720.mp4"),
                        1280,
                        720,
                        3000000,
                        90400)
                });

            Assert.AreEqual(2, reference.Renditions.Count);
        }

        [TestCase(1920, 1080, 90000, "duplicated")]
        [TestCase(1024, 720, 90000, "aspect ratio")]
        [TestCase(1280, 720, 90600, "500 ms")]
        public void VideoReference_WhenMp4AdditionalRenditionIsInvalid_Throws(
            int width,
            int height,
            long durationMs,
            string expectedError)
        {
            var primary = new MediaPath("videos/story/intro.mp4");
            var primaryRendition = new VideoRendition("1080P", primary, 1920, 1080, 6000000, 90000);
            var additional = new VideoRendition(
                "additional",
                new MediaPath("videos/story/intro-alt.mp4"),
                width,
                height,
                3000000,
                durationMs);

            var exception = Assert.Throws<ArgumentException>(() =>
                new VideoReference(primary, VideoFormat.Mp4, new[] { primaryRendition, additional }));
            StringAssert.Contains(expectedError, exception.Message);
        }

        [Test]
        public void VideoRequestFactory_WhenHlsHasRenditions_ResolvesCdnForPrimaryAndOptions()
        {
            var reference = new VideoReference(
                new MediaPath("videos/story/intro/master.m3u8"),
                VideoFormat.Hls,
                new[]
                {
                    new VideoRendition(
                        "720P",
                        new MediaPath("videos/story/intro/720P/index.m3u8"),
                        1280,
                        720,
                        3000000,
                        90000),
                    new VideoRendition(
                        "1080P",
                        new MediaPath("videos/story/intro/1080P/index.m3u8"),
                        1920,
                        1080,
                        6000000,
                        90000)
                });

            var request = VideoRequestFactory.Create(reference, m_Settings, true, false);

            Assert.IsTrue(request.Options.SupportsAutoQuality);
            Assert.AreEqual(VideoQualityMode.Auto, request.Options.InitialQuality.Mode);
            Assert.AreEqual(2, request.Options.QualityOptions.Count);
            Assert.AreEqual("https://cdn.example.com/videos/story/intro/master.m3u8", request.Path);
            Assert.AreEqual(
                "https://cdn.example.com/videos/story/intro/720P/index.m3u8",
                request.Options.QualityOptions[0].Location);
        }

        [Test]
        public void VideoRequestFactory_WhenCdnIsNotConfigured_ResolvesOrigin()
        {
            m_Settings.SetPublicUrls("https://origin.example.com");
            var reference = new VideoReference(
                new MediaPath("videos/story/intro/master.m3u8"),
                VideoFormat.Hls);

            var request = VideoRequestFactory.Create(reference, m_Settings, false, false);

            Assert.AreEqual("https://origin.example.com/videos/story/intro/master.m3u8", request.Path);
        }
    }
}
