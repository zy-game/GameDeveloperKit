using GameDeveloperKit.Story;
using NUnit.Framework;
using UnityEngine;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.Story.Playback;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryVideoPathResolverTests
    {
        [TestCase("Assets/StreamingAssets/videos/0.mp4")]
        [TestCase("StreamingAssets/videos/0.mp4")]
        [TestCase("videos/0.mp4")]
        public void Resolve_WhenSourceIsStreamingAssets_UsesStreamingAssetsRoot(string clip)
        {
            AssertResolve(
                MediaCommandNames.VideoSourceStreamingAssets,
                clip,
                $"{Application.streamingAssetsPath}/videos/0.mp4");
        }

        [Test]
        public void Resolve_WhenSourceIsCdn_ReturnsAbsoluteHttpsUrl()
        {
            AssertResolve(
                MediaCommandNames.VideoSourceCdn,
                "https://cdn.example.com/videos/0.mp4",
                "https://cdn.example.com/videos/0.mp4");
        }

        [TestCase(null, "videos/0.mp4")]
        [TestCase("", "videos/0.mp4")]
        [TestCase("streaming_assets", "guid:xxxx")]
        [TestCase("streaming_assets", "Assets/Bundles/Story/videos/0.mp4")]
        [TestCase("streaming_assets", "videos//0.mp4")]
        [TestCase("streaming_assets", "videos/./0.mp4")]
        [TestCase("cdn", "http://cdn.example.com/video.mp4")]
        [TestCase("cdn", "videos/0.mp4")]
        [TestCase("persistent_data_path", "videos/0.mp4")]
        [TestCase("network_stream", "https://example.com/video.mp4")]
        public void TryResolve_WhenInputIsInvalid_ReturnsFalse(string source, string clip)
        {
            var resolved = VideoPathResolver.TryResolve(source, clip, out var resolvedPath, out var errorMessage);

            Assert.IsFalse(resolved);
            Assert.IsNull(resolvedPath);
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
        }

        [Test]
        public void TryResolve_WhenClipIsLocalAbsolutePath_ReturnsFalse()
        {
            var resolved = VideoPathResolver.TryResolve(
                MediaCommandNames.VideoSourceStreamingAssets,
                $"{Application.dataPath}/StreamingAssets/videos/0.mp4",
                out var resolvedPath,
                out var errorMessage);

            Assert.IsFalse(resolved);
            Assert.IsNull(resolvedPath);
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
        }

        private static void AssertResolve(string source, string clip, string expectedPath)
        {
            var resolved = VideoPathResolver.TryResolve(source, clip, out var resolvedPath, out var errorMessage);

            Assert.IsTrue(resolved, errorMessage);
            Assert.AreEqual(Normalize(expectedPath), Normalize(resolvedPath));
        }

        private static string Normalize(string path)
        {
            return path?.Replace('\\', '/');
        }
    }
}
