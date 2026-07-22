using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.LocalizationEditor;
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
        public void LocalizationTextCatalog_DoesNotExposeJsonParser()
        {
            Assert.IsNull(typeof(LocalizationTextCatalog).GetMethod(
                "Parse",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic));
        }

        [Test]
        public void LocalizationPickerView_WhenExactKeyIsEntered_CanConfirmExistingSelection()
        {
            var catalog = new PickerCatalogStub();
            LocalizationSelection? selected = null;
            var view = new LocalizationPickerView(
                new LocalizationPickerRequest("story.rain", "zh-CN", false),
                value => selected = value,
                catalog,
                new PickerAuthoringStub(catalog));

            Assert.NotNull(view.Q<TextField>("localization-picker-search"));
            Assert.AreEqual(1, view.Q<ScrollView>("localization-picker-results").childCount);
            Assert.IsTrue(view.CanConfirm);
            view.ConfirmSelected();
            Assert.IsTrue(selected.HasValue);
            Assert.AreEqual("story.rain", selected.Value.Key);
        }

        [Test]
        public void LocalizationPickerView_WhenCreatingKey_RefreshesAndImmediatelyConfirmsSelection()
        {
            var catalog = new PickerCatalogStub();
            var authoring = new PickerAuthoringStub(catalog);
            LocalizationSelection? selected = null;
            var view = new LocalizationPickerView(
                new LocalizationPickerRequest(string.Empty, "zh-CN", true, "新的文本"),
                value => selected = value,
                catalog,
                authoring);

            var keyField = view.Q<TextField>("localization-picker-create-key");
            var textField = view.Q<TextField>("localization-picker-create-text");
            Assert.AreEqual(string.Empty, keyField.value);
            Assert.AreEqual("新的文本", textField.value);

            keyField.value = "story.new";
            InvokePrivate(view, "CreateKey");

            Assert.IsTrue(selected.HasValue);
            Assert.AreEqual("story.new", selected.Value.Key);
            Assert.AreEqual("新的文本", selected.Value.PreviewText);
            Assert.AreEqual("story.new", authoring.LastCreatedKey);
            Assert.AreEqual("zh-CN", authoring.LastCreatedLocale);
            Assert.AreEqual("新的文本", authoring.LastCreatedText);
            Assert.IsTrue(catalog.Refresh().Entries.ContainsKey("story.new"));
        }

        [Test]
        public void LocalizationPickerView_WhenCatalogIsInvalid_ShowsChineseDiagnosticAndDisablesKeyActions()
        {
            var catalog = new PickerCatalogStub("尚未绑定全局本地化 Catalog。");
            var view = new LocalizationPickerView(
                new LocalizationPickerRequest(string.Empty, "zh-CN", true, "直接文本"),
                _ => Assert.Fail("Invalid catalog must not confirm a key."),
                catalog,
                new PickerAuthoringStub(catalog));

            Assert.IsFalse(view.CanConfirm);
            Assert.AreEqual(0, view.Q<ScrollView>("localization-picker-results").childCount);
            StringAssert.Contains("尚未绑定", view.Q<Label>("localization-picker-status").text);
            Assert.IsFalse(view.Q<Button>("localization-picker-create-button").enabledSelf);
        }

        [Test]
        public void TextReferencePickerWindow_WhenBuilt_EmbedsCommonPickerAndTwoStorySaveActions()
        {
            var window = ScriptableObject.CreateInstance<TextReferencePickerWindow>();
            try
            {
                InvokePrivate(
                    window,
                    "BuildUi",
                    TextReferenceCodec.Serialize(new TextReference(TextMode.Literal, "雨夜")));

                var buttons = window.rootVisualElement.Query<Button>().ToList();
                var search = window.rootVisualElement.Q<TextField>("localization-picker-search");
                Assert.NotNull(window.rootVisualElement.Q<VisualElement>("localization-picker-view"));
                Assert.NotNull(search);
                Assert.AreEqual("雨夜", search.value);
                Assert.IsTrue(buttons.Any(x => x.text == "保存为直接文本"));
                Assert.IsTrue(buttons.Any(x => x.text == "保存为多语言 Key"));
                Assert.NotNull(window.rootVisualElement.Q<Button>("localization-picker-create-button"));
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
        public void VideoPickerWindow_WhenOpened_UsesStableUtilityWindow()
        {
            var source = IOFile.ReadAllText(FrameworkFilePath("Editor/StoryEditor/Media/VideoPickerWindow.cs"));
            var classStart = source.IndexOf("internal sealed class VideoPickerWindow", StringComparison.Ordinal);
            var enableStart = source.IndexOf("private void OnEnable()", classStart, StringComparison.Ordinal);
            var openMethod = source.Substring(classStart, enableStart - classStart);

            StringAssert.Contains("window.ShowUtility();", openMethod);
            StringAssert.DoesNotContain("window.ShowAuxWindow();", openMethod);
        }

        [Test]
        public void VideoPickerWindow_WhenScanned_DrivesAvProFromEditorUpdate()
        {
            var source = IOFile.ReadAllText(FrameworkFilePath("Editor/StoryEditor/Media/VideoPickerWindow.cs"));
            var extractorStart = source.IndexOf("internal static class VideoThumbnailExtractor", StringComparison.Ordinal);
            var audioPickerStart = source.IndexOf("internal sealed class AudioPickerWindow", extractorStart, StringComparison.Ordinal);
            var extractorSource = source.Substring(extractorStart, audioPickerStart - extractorStart);

            StringAssert.Contains("EditorApplication.update += OnEditorUpdate;", extractorSource);
            StringAssert.Contains("m_Player.EditorUpdate();", extractorSource);
            StringAssert.Contains("InternalEditorUtility.RepaintAllViews();", extractorSource);
            StringAssert.Contains("EditorApplication.update -= OnEditorUpdate;", extractorSource);
            StringAssert.Contains("control.CanPlay() is false", extractorSource);
            StringAssert.Contains("textureProducer.GetTextureFrameCount()", extractorSource);
            StringAssert.DoesNotContain("Events.AddListener", extractorSource);
            StringAssert.DoesNotContain("SeekFast(", extractorSource);
            StringAssert.DoesNotContain("m_Player.Info", extractorSource);
        }

        [Test]
        public void VideoThumbnailDiskCache_WhenVideoIsUnchanged_RoundTripsPng()
        {
            var root = Path.Combine(Path.GetTempPath(), "gdk-story-video-cache-" + Guid.NewGuid().ToString("N"));
            var videoPath = Path.Combine(root, "video.mp4");
            Texture2D source = null;
            Texture2D restored = null;
            try
            {
                Directory.CreateDirectory(root);
                IOFile.WriteAllBytes(videoPath, new byte[] { 1, 2, 3 });
                source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                source.SetPixels(new[] { Color.red, Color.green, Color.blue, Color.white });
                source.Apply(false, false);
                var cache = new VideoThumbnailDiskCache(Path.Combine(root, "cache"));

                Assert.IsTrue(cache.TryStore(videoPath, source));
                Assert.IsTrue(cache.TryLoad(videoPath, out restored));
                Assert.AreEqual(2, restored.width);
                Assert.AreEqual(2, restored.height);
                StringAssert.EndsWith(".png", cache.GetCachePath(videoPath));
            }
            finally
            {
                if (source != null)
                {
                    UnityEngine.Object.DestroyImmediate(source);
                }

                if (restored != null)
                {
                    UnityEngine.Object.DestroyImmediate(restored);
                }

                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void VideoThumbnailDiskCache_WhenVideoChanges_InvalidatesCachedPng()
        {
            var root = Path.Combine(Path.GetTempPath(), "gdk-story-video-cache-" + Guid.NewGuid().ToString("N"));
            var videoPath = Path.Combine(root, "video.mp4");
            Texture2D source = null;
            Texture2D restored = null;
            try
            {
                Directory.CreateDirectory(root);
                IOFile.WriteAllBytes(videoPath, new byte[] { 1, 2, 3 });
                source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                source.SetPixels(new[] { Color.red, Color.green, Color.blue, Color.white });
                source.Apply(false, false);
                var cache = new VideoThumbnailDiskCache(Path.Combine(root, "cache"));
                var originalCachePath = cache.GetCachePath(videoPath);
                Assert.IsTrue(cache.TryStore(videoPath, source));

                IOFile.WriteAllBytes(videoPath, new byte[] { 1, 2, 3, 4 });

                Assert.AreNotEqual(originalCachePath, cache.GetCachePath(videoPath));
                Assert.IsFalse(cache.TryLoad(videoPath, out restored));
            }
            finally
            {
                if (source != null)
                {
                    UnityEngine.Object.DestroyImmediate(source);
                }

                if (restored != null)
                {
                    UnityEngine.Object.DestroyImmediate(restored);
                }

                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void VideoThumbnailDiskCache_UsesProjectLibraryDirectory()
        {
            Assert.AreEqual(
                "Library/GameDeveloperKit/StoryVideoThumbnails",
                VideoThumbnailDiskCache.ProjectCacheRoot);
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

        private static string FrameworkFilePath(string relativePath)
        {
            var normalizedRelativePath = relativePath.Replace('\\', '/').Trim('/');
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(VideoPickerWindow).Assembly);
            if (string.IsNullOrWhiteSpace(packageInfo?.resolvedPath) is false)
            {
                var packageFilePath = Path.Combine(packageInfo.resolvedPath, normalizedRelativePath);
                if (IOFile.Exists(packageFilePath) || Directory.Exists(packageFilePath))
                {
                    return packageFilePath;
                }
            }

            var assetsFilePath = Path.Combine("Assets/GameDeveloperKit", normalizedRelativePath);
            return IOFile.Exists(assetsFilePath) || Directory.Exists(assetsFilePath)
                ? assetsFilePath
                : Path.Combine("Packages/com.gamedeveloperkit.framework", normalizedRelativePath);
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

        private sealed class PickerCatalogStub : ILocalizationEditorCatalog
        {
            private readonly Dictionary<string, LocalizationEditorEntry> m_Entries =
                new Dictionary<string, LocalizationEditorEntry>(StringComparer.Ordinal);
            private readonly string m_Error;
            private long m_NextKeyId = 3;

            public PickerCatalogStub(string error = null)
            {
                m_Error = error;
                if (string.IsNullOrWhiteSpace(error))
                {
                    m_Entries.Add("story.rain", new LocalizationEditorEntry(1, "story.rain", "雨夜", false));
                    m_Entries.Add("story.missing", new LocalizationEditorEntry(2, "story.missing", string.Empty, true));
                }
            }

            public LocalizationCatalogSnapshot Refresh()
            {
                var diagnostics = string.IsNullOrWhiteSpace(m_Error)
                    ? Array.Empty<LocalizationCatalogDiagnostic>()
                    : new[]
                    {
                        new LocalizationCatalogDiagnostic(LocalizationCatalogDiagnosticSeverity.Error, m_Error)
                    };
                return new LocalizationCatalogSnapshot(1, "zh-CN", m_Entries, diagnostics);
            }

            public bool TryGetText(string key, out string text)
            {
                return TryGetText(key, "zh-CN", out text);
            }

            public bool TryGetText(string key, string locale, out string text)
            {
                text = null;
                if (m_Entries.TryGetValue(key ?? string.Empty, out var entry) is false || entry.IsMissing)
                {
                    return false;
                }

                text = entry.PreviewText;
                return true;
            }

            public IReadOnlyList<LocalizationSearchResult> Search(string query, int limit = 100)
            {
                return Search(query, "zh-CN", limit);
            }

            public IReadOnlyList<LocalizationSearchResult> Search(string query, string previewLocale, int limit = 100)
            {
                var value = query?.Trim() ?? string.Empty;
                return m_Entries.Values
                    .Where(entry => value.Length == 0 ||
                                    entry.Key.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    entry.PreviewText.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Take(limit)
                    .Select(entry => new LocalizationSearchResult(
                        entry.KeyId,
                        entry.Key,
                        entry.PreviewText,
                        entry.IsMissing))
                    .ToArray();
            }

            public long Add(string key, string text)
            {
                var keyId = m_NextKeyId++;
                m_Entries.Add(key, new LocalizationEditorEntry(keyId, key, text, false));
                return keyId;
            }
        }

        private sealed class PickerAuthoringStub : ILocalizationAuthoringService
        {
            private readonly PickerCatalogStub m_Catalog;

            public PickerAuthoringStub(PickerCatalogStub catalog)
            {
                m_Catalog = catalog;
            }

            public string LastCreatedKey { get; private set; }

            public string LastCreatedLocale { get; private set; }

            public string LastCreatedText { get; private set; }

            public LocalizationAuthoringSnapshot Refresh()
            {
                return null;
            }

            public LocalizationMutationResult CreateCatalog(string folderPath, string catalogName, string initialLocale)
            {
                throw new NotSupportedException();
            }

            public LocalizationMutationResult BindCatalog(GameDeveloperKit.Localization.LocalizationCatalogAsset catalog)
            {
                throw new NotSupportedException();
            }

            public LocalizationMutationResult CreateKey(string key, string locale, string value)
            {
                LastCreatedKey = key?.Trim();
                LastCreatedLocale = locale?.Trim();
                LastCreatedText = value;
                var keyId = m_Catalog.Add(LastCreatedKey, value ?? string.Empty);
                return LocalizationMutationResult.Success(null, keyId: keyId);
            }

            public LocalizationMutationResult RenameKey(long keyId, string newKey)
            {
                throw new NotSupportedException();
            }

            public LocalizationMutationResult RemoveKey(long keyId)
            {
                throw new NotSupportedException();
            }

            public LocalizationMutationResult SetText(long keyId, string locale, string value)
            {
                throw new NotSupportedException();
            }

            public LocalizationMutationResult RemoveText(long keyId, string locale)
            {
                throw new NotSupportedException();
            }

            public LocalizationMutationResult AddLocale(LocalizationLocaleDraft draft)
            {
                throw new NotSupportedException();
            }

            public LocalizationMutationResult RemoveLocale(string locale)
            {
                throw new NotSupportedException();
            }

            public LocalizationMutationResult SetDefaultLocale(string locale)
            {
                throw new NotSupportedException();
            }

            public LocalizationMutationResult SetLocaleDescriptor(
                string locale,
                string resourceLocation,
                string fallbackLocale)
            {
                throw new NotSupportedException();
            }

            public LocalizationMutationResult ApplyImport(LocalizationImportAssetMutation mutation)
            {
                throw new NotSupportedException();
            }

            public IReadOnlyList<string> FindKeyUsages(string key)
            {
                return Array.Empty<string>();
            }
        }
    }
}
