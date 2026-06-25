using GameDeveloperKit.Story;
using NUnit.Framework;
using UnityEngine;

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
                StoryMediaCommandNames.VideoSourceStreamingAssets,
                clip,
                $"{Application.streamingAssetsPath}/videos/0.mp4");
        }

        [Test]
        public void Resolve_WhenSourceIsPersistentDataPath_UsesPersistentDataRoot()
        {
            AssertResolve(
                StoryMediaCommandNames.VideoSourcePersistentDataPath,
                "videos/0.mp4",
                $"{Application.persistentDataPath}/videos/0.mp4");
        }

        [Test]
        public void Resolve_WhenSourceIsNetworkStream_ReturnsUrl()
        {
            AssertResolve(
                StoryMediaCommandNames.VideoSourceNetworkStream,
                "https://example.com/video.mp4",
                "https://example.com/video.mp4");
        }

        [TestCase(null, "videos/0.mp4")]
        [TestCase("", "videos/0.mp4")]
        [TestCase("streaming_assets", "guid:xxxx")]
        [TestCase("streaming_assets", "Assets/GameDeveloperKit/Simples/videos/0.mp4")]
        public void TryResolve_WhenInputIsInvalid_ReturnsFalse(string source, string clip)
        {
            var resolved = StoryVideoPathResolver.TryResolve(source, clip, out var resolvedPath, out var errorMessage);

            Assert.IsFalse(resolved);
            Assert.IsNull(resolvedPath);
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
        }

        [Test]
        public void TryResolve_WhenClipIsLocalAbsolutePath_ReturnsFalse()
        {
            var resolved = StoryVideoPathResolver.TryResolve(
                StoryMediaCommandNames.VideoSourceStreamingAssets,
                $"{Application.dataPath}/StreamingAssets/videos/0.mp4",
                out var resolvedPath,
                out var errorMessage);

            Assert.IsFalse(resolved);
            Assert.IsNull(resolvedPath);
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
        }

        private static void AssertResolve(string source, string clip, string expectedPath)
        {
            var resolved = StoryVideoPathResolver.TryResolve(source, clip, out var resolvedPath, out var errorMessage);

            Assert.IsTrue(resolved, errorMessage);
            Assert.AreEqual(Normalize(expectedPath), Normalize(resolvedPath));
        }

        private static string Normalize(string path)
        {
            return path?.Replace('\\', '/');
        }
    }
}
