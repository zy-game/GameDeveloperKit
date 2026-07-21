using System;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Story.Authoring;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Protocol;
using GameDeveloperKit.StoryEditor.Model;
using GameDeveloperKit.StoryEditor.Media;
using GameDeveloperKit.Story.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using IOFile = System.IO.File;

namespace GameDeveloperKit.Tests
{
    public sealed class StoryMediaCatalogTests
    {
        [Test]
        public void LocalizationTextCatalog_WhenZhCnJsonParsed_SearchesKeyAndResolvesPreview()
        {
            var catalog = LocalizationTextCatalog.Parse("{\"entries\":{\"story.line\":\"中文对白\"}}");

            Assert.IsNull(catalog.Error);
            Assert.IsTrue(catalog.TryGetText("story.line", out var text));
            Assert.AreEqual("中文对白", text);
            Assert.AreEqual("中文对白", catalog.Resolve(new TextReference(TextMode.LocalizationKey, "story.line")));
            Assert.AreEqual("直接文本", catalog.Resolve(new TextReference(TextMode.Literal, "直接文本")));
        }

        [Test]
        public void LocalizationTextCatalog_WhenSearched_MatchesKeyAndChineseTextWithStablePriority()
        {
            var catalog = LocalizationTextCatalog.Parse(
                "{\"entries\":{" +
                "\"story.rain\":\"雨夜抵达\"," +
                "\"story.rain.long\":\"雨夜后的车站\"," +
                "\"episode.arrival\":\"雨夜抵达\"," +
                "\"ui.leave\":\"离开车站\"}}");

            var keyMatches = catalog.Search("story.rain");
            Assert.AreEqual(2, keyMatches.Count);
            Assert.AreEqual("story.rain", keyMatches[0].Key);
            Assert.AreEqual("story.rain.long", keyMatches[1].Key);

            var textMatches = catalog.Search("雨夜");
            CollectionAssert.AreEquivalent(
                new[] { "story.rain", "story.rain.long", "episode.arrival" },
                textMatches.Select(x => x.Key));
            Assert.AreEqual(0, catalog.Search("不存在").Count);
        }

        [Test]
        public void TextReferencePickerWindow_WhenBuilt_UsesSingleInputAndTwoExplicitSaveActions()
        {
            var window = ScriptableObject.CreateInstance<TextReferencePickerWindow>();
            try
            {
                InvokePrivate(
                    window,
                    "BuildUi",
                    TextReferenceCodec.Serialize(new TextReference(TextMode.Literal, "雨夜")));

                var inputs = window.rootVisualElement.Query<TextField>().ToList();
                var buttons = window.rootVisualElement.Query<Button>().ToList();
                Assert.AreEqual(1, inputs.Count);
                Assert.AreEqual("文本或多语言 Key", inputs[0].label);
                Assert.AreEqual("雨夜", inputs[0].value);
                Assert.IsTrue(buttons.Any(x => x.text == "保存为直接文本"));
                Assert.IsTrue(buttons.Any(x => x.text == "保存为多语言 Key"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void VideoPickerWindow_WhenCardCreated_ReservesStableThumbnailArea()
        {
            var method = typeof(VideoPickerWindow).GetMethod(
                "CreateCard",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var card = (VisualElement)method.Invoke(null, new object[] { "开场视频", "Mp4 · 1920×1080" });

            var thumbnail = card.Q<VisualElement>("video-thumbnail-container");
            Assert.IsNotNull(thumbnail);
            Assert.IsNotNull(thumbnail.Q<Label>("video-thumbnail-placeholder"));
            Assert.AreEqual(160f, thumbnail.style.width.value.value);
            Assert.AreEqual(90f, thumbnail.style.height.value.value);
            Assert.AreEqual(132f, card.style.height.value.value);
        }
        [Test]
        public void CatalogReferenceFactory_WhenLocationRelative_ExpandsPrimaryAndRenditions()
        {
            var item = CreateItem(
                "story/intro/master.m3u8",
                new CatalogRendition("1080p", null, "story/intro/1080.m3u8", 1920, 1080, 6000000, 90000));

            var reference = CatalogReferenceFactory.CreateVideoReference(item, "https://cdn.example.com/media");

            Assert.AreEqual("https://cdn.example.com/media/story/intro/master.m3u8", reference.Primary.Location);
            Assert.AreEqual("intro", reference.Renditions[0].MediaId);
            Assert.AreEqual("https://cdn.example.com/media/story/intro/1080.m3u8", reference.Renditions[0].Location);
        }

        [Test]
        public void CatalogReferenceFactory_WhenLocationAbsolute_PreservesUrl()
        {
            var item = new CatalogItem(
                "intro",
                "Intro",
                MediaKind.Video,
                "https://video.example.com/intro.mp4?version=2",
                VideoFormat.Mp4,
                null,
                3840,
                2160,
                12000000,
                90000,
                null);

            var reference = CatalogReferenceFactory.CreateVideoReference(item, "https://cdn.example.com/");

            Assert.AreEqual("https://video.example.com/intro.mp4?version=2", reference.Primary.Location);
            Assert.AreEqual(1, reference.Renditions.Count);
            Assert.AreEqual(2160, reference.Renditions[0].Height);
            Assert.AreEqual("4K", reference.Renditions[0].Label);
        }

        [Test]
        public void CatalogClient_WhenAudioResponseParsed_CreatesSelfContainedHttpsReference()
        {
            const string json = "{\"items\":[{\"mediaId\":\"theme\",\"name\":\"Theme\",\"kind\":\"audio\",\"location\":\"audio/theme.ogg\",\"durationMs\":45000}]}";

            var page = CatalogClient.ParsePage(json, MediaKind.Audio, "https://cdn.example.com/media/");
            var reference = CatalogReferenceFactory.CreateAudioReference(page.Items[0], "https://cdn.example.com/media/");
            var serialized = AudioReferenceCodec.Serialize(reference);

            Assert.AreEqual(MediaSource.Cdn, reference.Source);
            Assert.AreEqual("theme", reference.MediaId);
            Assert.AreEqual("https://cdn.example.com/media/audio/theme.ogg", reference.Location);
            Assert.IsTrue(AudioReferenceCodec.TryDeserialize(serialized, out var restored, out var error), error);
            Assert.AreEqual(reference.Location, restored.Location);
        }

        [Test]
        public void AudioReferenceSources_WhenStreamingAssetsScanned_IncludesOnlySupportedAudio()
        {
            var root = Path.Combine(Path.GetTempPath(), $"story-audio-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(root, "music"));
            try
            {
                IOFile.WriteAllBytes(Path.Combine(root, "music", "theme.ogg"), new byte[] { 1 });
                IOFile.WriteAllBytes(Path.Combine(root, "music", "notes.txt"), new byte[] { 1 });

                var references = AudioReferenceSources.ScanStreamingAssets(root);

                Assert.AreEqual(1, references.Count);
                Assert.AreEqual(MediaSource.StreamingAssets, references[0].Source);
                Assert.AreEqual("music/theme.ogg", references[0].Location);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void VideoRenditionEditor_WhenAddingAndRemovingMp4Version_PreservesPrimary()
        {
            var primary = CatalogReferenceFactory.CreateVideoReference(
                new CatalogItem(
                    "intro-1080", "Intro 1080", MediaKind.Video,
                    "intro-1080.mp4", VideoFormat.Mp4, null,
                    1920, 1080, 6000000, 90000, null),
                "https://cdn.example.com/");
            var candidate = CatalogReferenceFactory.CreateVideoReference(
                new CatalogItem(
                    "intro-720", "Intro 720", MediaKind.Video,
                    "intro-720.mp4", VideoFormat.Mp4, null,
                    1280, 720, 3000000, 90400, null),
                "https://cdn.example.com/");

            var combined = VideoRenditionEditor.Add(primary, candidate);
            var removed = VideoRenditionEditor.Remove(combined, 1);

            Assert.AreEqual(2, combined.Renditions.Count);
            Assert.AreEqual(primary.Primary.Location, combined.Primary.Location);
            Assert.AreEqual(1, removed.Renditions.Count);
            Assert.AreEqual(primary.Primary.Location, removed.Renditions[0].Location);
        }

        [Test]
        public void VideoRenditionEditor_WhenSourcesDiffer_RejectsCandidate()
        {
            var primary = CatalogReferenceFactory.CreateVideoReference(
                new CatalogItem(
                    "intro", "Intro", MediaKind.Video,
                    "intro.mp4", VideoFormat.Mp4, null,
                    1920, 1080, 6000000, 90000, null),
                "https://cdn.example.com/");
            var local = new VideoReference(
                new MediaReference(MediaKind.Video, MediaSource.StreamingAssets, null, "intro.mp4"),
                VideoFormat.Mp4,
                new[] { new VideoRendition("1080p", null, "intro.mp4", 1920, 1080, 6000000, 90000) });

            var exception = Assert.Throws<ArgumentException>(() => VideoRenditionEditor.Add(primary, local));
            StringAssert.Contains("same media source", exception.Message);
        }

        [Test]
        public void VideoRenditionEditor_WhenLocalMp4MetadataApplied_CanComposeVersions()
        {
            var primary = new VideoReference(
                new MediaReference(MediaKind.Video, MediaSource.StreamingAssets, null, "intro-1080.mp4"),
                VideoFormat.Mp4);
            var candidate = new VideoReference(
                new MediaReference(MediaKind.Video, MediaSource.StreamingAssets, null, "intro-720.mp4"),
                VideoFormat.Mp4);

            primary = VideoRenditionEditor.WithPrimaryMetadata(primary, 1920, 1080, 6000000, 90000);
            candidate = VideoRenditionEditor.WithPrimaryMetadata(candidate, 1280, 720, 3000000, 90400);
            var combined = VideoRenditionEditor.Add(primary, candidate);

            Assert.AreEqual(2, combined.Renditions.Count);
            Assert.AreEqual(720, combined.Renditions[1].Height);
        }

        [Test]
        public void UsageIndex_WhenCdnUrlChanges_MatchesStableMediaId()
        {
            var asset = CreateUsageAsset(new VideoReference(
                new MediaReference(MediaKind.Video, MediaSource.Cdn, "intro", "https://old.example.com/intro.mp4"),
                VideoFormat.Mp4));
            var index = new UsageIndex(() => new[] { ("Assets/Stories/Intro.asset", asset) });
            index.Rebuild();

            var usages = index.Find(new MediaReference(
                MediaKind.Video,
                MediaSource.Cdn,
                "intro",
                "https://new.example.com/intro.mp4"));

            Assert.AreEqual(1, usages.Count);
            Assert.AreEqual("story_usage", usages[0].StoryId);
            Assert.AreEqual("episode", usages[0].EpisodeId);
            Assert.AreEqual("video", usages[0].NodeId);
            Assert.AreEqual("Intro video", usages[0].NodeTitle);
            Assert.AreEqual("Assets/Stories/Intro.asset", usages[0].AssetPath);
        }

        [Test]
        public void UsageIndex_WhenBadNodeAndStreamingReferenceExist_SkipsBadAndFindsStreaming()
        {
            var reference = new VideoReference(
                new MediaReference(MediaKind.Video, MediaSource.StreamingAssets, null, "story/intro.mp4"),
                VideoFormat.Mp4);
            var asset = CreateUsageAsset(reference);
            asset.Episodes[0].Nodes.Add(new AuthoringNode
            {
                NodeId = "bad",
                NodeKind = NodeKind.PlayVideo,
                Title = "Bad"
            });
            asset.Episodes[0].Nodes[1].Parameters.Add(new AuthoringParameter
            {
                Key = MediaCommandNames.ClipArgument,
                Value = "not-json"
            });
            var index = new UsageIndex(() => new[] { ("Assets/Stories/Intro.asset", asset) });
            index.Rebuild();

            var usages = index.Find(reference.Primary);

            Assert.AreEqual(1, usages.Count);
        }

        [Test]
        public void UsageIndex_WhenLoaderFails_IsUnavailableAndFindThrows()
        {
            var index = new UsageIndex(() => throw new InvalidOperationException("scan failed"));

            index.Rebuild();

            Assert.IsFalse(index.IsAvailable);
            StringAssert.Contains("scan failed", index.ErrorMessage);
            Assert.Throws<InvalidOperationException>(() => index.Find(
                new MediaReference(MediaKind.Video, MediaSource.StreamingAssets, null, "story/intro.mp4")));
        }

        [TestCase("http://cdn.example.com/video.mp4")]
        [TestCase("../video.mp4")]
        [TestCase("/video.mp4")]
        [TestCase("story/%2e%2e/video.mp4")]
        [TestCase("story/%5c..%5cvideo.mp4")]
        [TestCase("story/%ZZ/video.mp4")]
        public void CatalogReferenceFactory_WhenLocationInvalid_ThrowsClassifiedError(string location)
        {
            var item = new CatalogItem(
                "intro",
                "Intro",
                MediaKind.Video,
                location,
                VideoFormat.Mp4,
                null,
                0,
                0,
                0,
                0,
                null);

            var exception = Assert.Throws<CatalogException>(() =>
                CatalogReferenceFactory.CreateVideoReference(item, "https://cdn.example.com/"));

            Assert.AreEqual(CatalogErrorKind.InvalidLocation, exception.Kind);
        }

        [Test]
        public void CatalogClient_WhenResponseHasDuplicateIds_ThrowsClassifiedError()
        {
            const string json = "{\"items\":[" +
                                "{\"mediaId\":\"same\",\"kind\":\"video\",\"location\":\"a.mp4\",\"format\":\"mp4\"}," +
                                "{\"mediaId\":\"same\",\"kind\":\"video\",\"location\":\"b.mp4\",\"format\":\"mp4\"}]}";

            var exception = Assert.Throws<CatalogException>(() =>
                CatalogClient.ParsePage(json, MediaKind.Video, "https://cdn.example.com/"));

            Assert.AreEqual(CatalogErrorKind.DuplicateMediaId, exception.Kind);
        }

        [Test]
        public void CatalogClient_WhenMetadataIsNegative_ThrowsInvalidResponse()
        {
            const string json = "{\"items\":[{" +
                                "\"mediaId\":\"video\",\"kind\":\"video\",\"location\":\"video.mp4\",\"format\":\"mp4\",\"durationMs\":-1}]}";

            var exception = Assert.Throws<CatalogException>(() =>
                CatalogClient.ParsePage(json, MediaKind.Video, "https://cdn.example.com/"));

            Assert.AreEqual(CatalogErrorKind.InvalidResponse, exception.Kind);
        }

        [Test]
        public void ThumbnailSessionCache_WhenDataStored_ReturnsIndependentBytes()
        {
            var cache = new ThumbnailSessionCache();
            var data = new byte[] { 1, 2, 3 };

            cache.Set("https://cdn.example.com/thumb.jpg", data);
            data[0] = 9;

            Assert.IsTrue(cache.TryGet("https://cdn.example.com/thumb.jpg", out var restored));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, restored);
        }

        [Test]
        public void CatalogClient_WhenSearching_UsesQueryCursorAndSessionCache()
        {
            var settings = CreateSettings();
            var calls = 0;
            Uri requestedUri = null;
            var client = new CatalogClient(
                settings,
                new CatalogSessionCache(),
                (uri, timeout, token) =>
                {
                    calls++;
                    requestedUri = uri;
                    return UniTask.FromResult("{\"items\":[],\"nextCursor\":\"page-2\"}");
                });

            var first = client.SearchAsync(MediaKind.Video, "opening rain", "page-1", 20, CancellationToken.None).GetAwaiter().GetResult();
            var second = client.SearchAsync(MediaKind.Video, "opening rain", "page-1", 20, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreSame(first, second);
            Assert.AreEqual(1, calls);
            Assert.IsTrue(
                requestedUri.Query.Contains("query=opening%20rain") || requestedUri.Query.Contains("query=opening+rain"),
                requestedUri.Query);
            StringAssert.Contains("cursor=page-1", requestedUri.Query);
            Assert.AreEqual("page-2", first.NextCursor);
        }

        [Test]
        public void CatalogClient_WhenCanceled_DoesNotCacheLateResult()
        {
            var settings = CreateSettings();
            var cache = new CatalogSessionCache();
            using (var cancellation = new CancellationTokenSource())
            {
                var client = new CatalogClient(
                    settings,
                    cache,
                    (uri, timeout, token) =>
                    {
                        cancellation.Cancel();
                        return UniTask.FromResult("{\"items\":[]}");
                    });

                Assert.Throws<OperationCanceledException>(() =>
                    client.SearchAsync(MediaKind.Video, "rain", null, 10, cancellation.Token).GetAwaiter().GetResult());
                Assert.IsFalse(cache.TryGet(
                    $"{settings.CatalogApiUrl}|{settings.CdnBaseUrl}|{settings.PreviewLocale}",
                    MediaKind.Video,
                    "rain",
                    null,
                    10,
                    out _));
            }
        }

        [Test]
        public void StreamingAssetsScanner_ListsMp4AndVodMasterOnly()
        {
            var root = Path.Combine(Path.GetTempPath(), "gdk-story-media-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "video", "hls"));
            try
            {
                IOFile.WriteAllText(Path.Combine(root, "video", "clip.mp4"), string.Empty);
                IOFile.WriteAllText(Path.Combine(root, "ignore.mov"), string.Empty);
                IOFile.WriteAllText(Path.Combine(root, "video", "live.m3u8"), "#EXTM3U\n#EXTINF:5\nlive.ts\n");
                IOFile.WriteAllText(
                    Path.Combine(root, "video", "master.m3u8"),
                    "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=6000000,RESOLUTION=1920x1080\nhls/1080.m3u8\n");
                IOFile.WriteAllText(Path.Combine(root, "video", "hls", "1080.m3u8"), "#EXTM3U\n#EXTINF:5\nsegment.ts\n#EXT-X-ENDLIST\n");

                var references = new StreamingAssetsVideoScanner().Scan(root);

                Assert.AreEqual(2, references.Count);
                var mp4 = references.Single(x => x.Format == VideoFormat.Mp4);
                var hls = references.Single(x => x.Format == VideoFormat.Hls);
                Assert.AreEqual("video/clip.mp4", mp4.Primary.Location);
                Assert.AreEqual("video/master.m3u8", hls.Primary.Location);
                Assert.AreEqual("video/hls/1080.m3u8", hls.Renditions[0].Location);
                Assert.AreEqual(1920, hls.Renditions[0].Width);
                Assert.AreEqual(1080, hls.Renditions[0].Height);
                Assert.AreEqual(6000000, hls.Renditions[0].Bitrate);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static CatalogSettings CreateSettings()
        {
            var settings = ScriptableObject.CreateInstance<CatalogSettings>();
            settings.CatalogApiUrl = "https://catalog.example.com/videos";
            settings.CdnBaseUrl = "https://cdn.example.com/";
            settings.TimeoutSeconds = 10;
            return settings;
        }

        private static void InvokePrivate(object instance, string name, params object[] args)
        {
            var method = instance.GetType().GetMethod(
                name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, name);
            method.Invoke(instance, args);
        }

        private static AuthoringAsset CreateUsageAsset(VideoReference reference)
        {
            var asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            asset.StoryId = "story_usage";
            var episode = new AuthoringEpisode { EpisodeId = "episode", Title = "Episode" };
            var node = new AuthoringNode
            {
                NodeId = "video",
                NodeKind = NodeKind.PlayVideo,
                Title = "Intro video"
            };
            node.Parameters.Add(new AuthoringParameter
            {
                Key = MediaCommandNames.ClipArgument,
                Value = VideoReferenceCodec.Serialize(reference)
            });
            episode.Nodes.Add(node);
            asset.Episodes.Add(episode);
            return asset;
        }

        private static CatalogItem CreateItem(string location, CatalogRendition rendition)
        {
            return new CatalogItem(
                "intro",
                "Intro",
                MediaKind.Video,
                location,
                VideoFormat.Hls,
                "thumbs/intro.jpg",
                1920,
                1080,
                6000000,
                90000,
                new[] { rendition });
        }
    }
}
