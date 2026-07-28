using System;
using System.Linq;
using GameDeveloperKit.EditorCloud;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.StoryEditor.Media;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Tests
{
    public sealed class HlsCatalogCodecTests
    {
        private const string CatalogJson =
            "{\"schemaVersion\":1,\"generation\":7," +
            "\"updatedAtUtc\":\"2026-07-28T01:00:00Z\",\"items\":[" +
            "{\"mediaId\":\"opening-a1\",\"name\":\"Opening\",\"sourceFileName\":\"opening.mp4\"," +
            "\"sourceSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"," +
            "\"uploader\":\"Alice\",\"createdAtUtc\":\"2026-07-27T01:00:00Z\"," +
            "\"updatedAtUtc\":\"2026-07-28T01:00:00Z\",\"kind\":\"video\",\"format\":\"hls\"," +
            "\"objectPrefix\":\"opening-a1/\",\"location\":\"opening-a1/master.m3u8\"," +
            "\"thumbnail\":\"opening-a1/preview.jpg\",\"width\":1920,\"height\":1080," +
            "\"bitrate\":5000000,\"durationMs\":90000,\"renditions\":[" +
            "{\"label\":\"1080p\",\"location\":\"opening-a1/1080p/index.m3u8\"," +
            "\"width\":1920,\"height\":1080,\"bitrate\":5000000,\"durationMs\":90000}]}," +
            "{\"mediaId\":\"ending-b2\",\"name\":\"Ending\",\"uploader\":\"Bob\"," +
            "\"kind\":\"video\",\"format\":\"hls\",\"location\":\"ending-b2/master.m3u8\"}]}";

        [Test]
        public void ParseDocument_WhenSchemaIsValid_PreservesLibraryMetadata()
        {
            var document = HlsCatalogCodec.ParseDocument(
                CatalogJson,
                "https://cdn.example.com/videos",
                true);

            Assert.AreEqual(1, document.SchemaVersion);
            Assert.AreEqual(7, document.Generation);
            Assert.AreEqual(2, document.Items.Count);
            var item = document.Items[0];
            Assert.AreEqual("Alice", item.Uploader);
            Assert.AreEqual("opening.mp4", item.SourceFileName);
            Assert.AreEqual("opening-a1/preview.jpg", item.ThumbnailLocation);
            Assert.AreEqual(1, item.Renditions.Count);
            Assert.AreEqual(2026, item.UpdatedAtUtc.Value.Year);
        }

        [Test]
        public void ParseDocument_WhenSchemaIsUnknown_ThrowsUnsupportedSchema()
        {
            var exception = Assert.Throws<CatalogException>(() =>
                HlsCatalogCodec.ParseDocument(
                    "{\"schemaVersion\":2,\"items\":[]}",
                    "https://cdn.example.com/videos",
                    true));

            Assert.AreEqual(CatalogErrorKind.UnsupportedSchema, exception.Kind);
        }

        [Test]
        public void Search_FiltersByUploaderAndUsesOffsetCursor()
        {
            var document = HlsCatalogCodec.ParseDocument(
                CatalogJson,
                "https://cdn.example.com/videos",
                true);

            var byUploader = HlsCatalogCodec.Search(document, MediaKind.Video, "alice", null, 10);
            var firstPage = HlsCatalogCodec.Search(document, MediaKind.Video, string.Empty, null, 1);
            var secondPage = HlsCatalogCodec.Search(document, MediaKind.Video, string.Empty, firstPage.NextCursor, 1);

            Assert.AreEqual("opening-a1", byUploader.Items.Single().MediaId);
            Assert.AreEqual("1", firstPage.NextCursor);
            Assert.AreEqual("ending-b2", secondPage.Items.Single().MediaId);
            Assert.AreEqual(string.Empty, secondPage.NextCursor);
        }

        [Test]
        public void Search_WhenCursorIsNotOffset_ThrowsInvalidCursor()
        {
            var document = HlsCatalogCodec.ParseDocument(
                CatalogJson,
                "https://cdn.example.com/videos",
                true);

            var exception = Assert.Throws<CatalogException>(() =>
                HlsCatalogCodec.Search(document, MediaKind.Video, string.Empty, "page-2", 10));

            Assert.AreEqual(CatalogErrorKind.InvalidCursor, exception.Kind);
        }

        [Test]
        public void BuildCatalogUri_UsesCdnDirectoryAndOnlyRefreshAddsQuery()
        {
            var normal = CatalogClient.BuildCatalogUri("https://cdn.example.com/videos/", false);
            var refresh = CatalogClient.BuildCatalogUri("https://cdn.example.com/videos/", true);

            Assert.AreEqual("https://cdn.example.com/videos/catalog.json", normal.AbsoluteUri);
            Assert.AreEqual("/videos/catalog.json", refresh.AbsolutePath);
            StringAssert.Contains("catalogRevision=", refresh.Query);
        }

        [Test]
        public void LibraryWindow_SourceContainsExpectedIndustrialEntryPoints()
        {
            var source = System.IO.File.ReadAllText(FrameworkFilePath(
                "Editor/StoryEditor/Media/HlsMediaLibraryWindow.cs"));

            StringAssert.Contains("GameDeveloperKit/媒体/HLS 流媒体库", source);
            StringAssert.Contains("name = \"hls-library-add\"", source);
            StringAssert.Contains("name = \"hls-library-refresh\"", source);
            StringAssert.Contains("name = \"hls-library-search\"", source);
            StringAssert.Contains("name = \"hls-library-storage\"", source);
            StringAssert.Contains("Catalog.thumbnail", source.Replace("item.ThumbnailLocation", "Catalog.thumbnail"));
            StringAssert.DoesNotContain("VideoThumbnailExtractor", source);
            StringAssert.Contains("listPane.style.flexGrow = 7f;", source);
            StringAssert.Contains("flexGrow = 3f", source);
            StringAssert.DoesNotContain("new TwoPaneSplitView", source);

            var addStart = source.IndexOf("private async UniTask SelectMp4Async", StringComparison.Ordinal);
            var loadStart = source.IndexOf("private async UniTask LoadPageAsync", addStart, StringComparison.Ordinal);
            var addSource = source.Substring(addStart, loadStart - addStart);
            StringAssert.Contains("EnsureCloudCredentialConfigured", addSource);
            StringAssert.Contains("MainWindow.OpenCloudConfiguration", addSource);
            StringAssert.Contains("m_CatalogRepository.LoadOriginAsync", addSource);
            StringAssert.DoesNotContain("m_CatalogClient.SearchAsync", addSource);
        }

        [Test]
        public void LibraryWindow_FormatsActiveCloudStorageForHeader()
        {
            var label = HlsMediaLibraryWindow.FormatStorageLabel(new CloudProjectConfig
            {
                ProviderId = CloudProviderId.AliyunOss,
                Bucket = "video-bucket",
                Region = "cn-hangzhou"
            });

            Assert.AreEqual("阿里云 OSS · video-bucket · cn-hangzhou", label);
        }

        [Test]
        public void LibraryWindow_HeaderNameColumnGrowsWithItemRows()
        {
            var createHeader = typeof(HlsMediaLibraryWindow).GetMethod(
                "CreateListHeader",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var header = (VisualElement)createHeader.Invoke(null, null);
            var columns = header.Children().OfType<Label>().ToArray();

            Assert.AreEqual(6, columns.Length);
            Assert.AreEqual("名称", columns[1].text);
            Assert.AreEqual(1f, columns[1].style.flexGrow.value);
        }

        private static string FrameworkFilePath(string relativePath)
        {
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(
                UnityEngine.Application.dataPath,
                "GameDeveloperKit",
                relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar)));
        }
    }
}
