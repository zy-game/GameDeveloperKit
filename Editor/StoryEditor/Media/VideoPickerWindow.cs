using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Story.Media;
using GameDeveloperKit.Story.Text;
using GameDeveloperKit.Story.Model;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using AvProMediaPathType = RenderHeads.Media.AVProVideo.MediaPathType;
using AvProMediaPlayer = RenderHeads.Media.AVProVideo.MediaPlayer;
using IOFile = System.IO.File;

namespace GameDeveloperKit.StoryEditor.Media
{
    internal sealed class VideoPickerWindow : EditorWindow
    {
        private const int PageSize = 30;
        private const float CardWidth = 168f;
        private const float ThumbnailWidth = 160f;
        private const float ThumbnailHeight = 90f;
        private static readonly ThumbnailSessionCache s_ThumbnailCache = new ThumbnailSessionCache();
        private static readonly VideoThumbnailDiskCache s_LocalThumbnailCache = new VideoThumbnailDiskCache();
        private static readonly SemaphoreSlim s_LocalThumbnailGate = new SemaphoreSlim(1, 1);

        private readonly List<Texture2D> m_TemporaryTextures = new List<Texture2D>();
        private Action<string> m_Confirmed;
        private ICatalogClient m_CatalogClient;
        private CancellationTokenSource m_LifetimeCancellation;
        private CancellationTokenSource m_SearchCancellation;
        private TextField m_SearchField;
        private Label m_Status;
        private ScrollView m_List;
        private VisualElement m_Details;
        private Button m_ConfirmButton;
        private string m_NextCursor;
        private int m_RequestVersion;
        private CatalogItem m_SelectedCatalogItem;
        private VideoReference m_SelectedReference;
        private VideoReference m_ComposedReference;
        private UsageIndex m_UsageIndex;
        private bool m_ShowCdn = true;

        public static void Open(string currentValue, Action<string> confirmed)
        {
            var window = CreateInstance<VideoPickerWindow>();
            window.titleContent = new GUIContent("选择剧情视频");
            window.minSize = new Vector2(760f, 520f);
            window.m_Confirmed = confirmed;
            VideoReferenceCodec.TryDeserialize(currentValue, out window.m_SelectedReference, out _);
            window.m_ComposedReference = window.m_SelectedReference;
            window.RefreshDetails();
            window.ShowUtility();
        }

        private void OnEnable()
        {
            m_LifetimeCancellation = new CancellationTokenSource();
            m_CatalogClient = new CatalogClient(CatalogSettings.LoadOrCreate());
            m_UsageIndex = new UsageIndex();
            m_UsageIndex.Rebuild();
            BuildUi();
            ShowCdn();
        }

        private void OnDisable()
        {
            CancelSearch();
            m_LifetimeCancellation?.Cancel();
            m_LifetimeCancellation?.Dispose();
            m_LifetimeCancellation = null;
            for (var i = 0; i < m_TemporaryTextures.Count; i++)
            {
                DestroyImmediate(m_TemporaryTextures[i]);
            }

            m_TemporaryTextures.Clear();
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 10f;
            rootVisualElement.style.paddingRight = 10f;
            rootVisualElement.style.paddingTop = 10f;
            rootVisualElement.style.paddingBottom = 10f;

            var tabs = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            tabs.Add(new Button(ShowCdn) { text = "CDN" });
            tabs.Add(new Button(ShowStreamingAssets) { text = "StreamingAssets" });
            rootVisualElement.Add(tabs);

            var search = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8f } };
            m_SearchField = new TextField { style = { flexGrow = 1f } };
            m_SearchField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    RunAsync(SearchCatalog(null));
                }
            });
            search.Add(m_SearchField);
            search.Add(new Button(() => RunAsync(SearchCatalog(null))) { text = "搜索" });
            search.Add(new Button(() => RunAsync(SearchCatalog(m_NextCursor))) { text = "下一页" });
            rootVisualElement.Add(search);

            m_Status = new Label { style = { marginTop = 6f, marginBottom = 6f } };
            rootVisualElement.Add(m_Status);

            var content = new TwoPaneSplitView(0, 430f, TwoPaneSplitViewOrientation.Horizontal)
            {
                style = { flexGrow = 1f }
            };
            m_List = new ScrollView();
            m_List.contentContainer.style.flexDirection = FlexDirection.Row;
            m_List.contentContainer.style.flexWrap = Wrap.Wrap;
            m_List.contentContainer.style.alignContent = Align.FlexStart;
            m_Details = new ScrollView { style = { paddingLeft = 10f } };
            content.Add(m_List);
            content.Add(m_Details);
            rootVisualElement.Add(content);

            var footer = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd, marginTop = 8f } };
            footer.Add(new Button(() =>
            {
                m_Confirmed?.Invoke(string.Empty);
                Close();
            }) { text = "清除" });
            footer.Add(new Button(Close) { text = "取消" });
            m_ConfirmButton = new Button(ConfirmSelection) { text = "使用此视频" };
            footer.Add(m_ConfirmButton);
            rootVisualElement.Add(footer);
            RefreshDetails();
        }

        private void ShowCdn()
        {
            m_ShowCdn = true;
            m_SearchField?.SetEnabled(true);
            RunAsync(SearchCatalog(null));
        }

        private void ShowStreamingAssets()
        {
            m_ShowCdn = false;
            CancelSearch();
            m_SearchField?.SetEnabled(false);
            m_List?.Clear();
            var requestVersion = m_RequestVersion;
            try
            {
                var root = Path.Combine(Application.dataPath, "StreamingAssets");
                var references = new StreamingAssetsVideoScanner().Scan(root);
                for (var i = 0; i < references.Count; i++)
                {
                    AddLocalItem(references[i], requestVersion);
                }

                SetStatus($"找到 {references.Count} 个本地视频。");
            }
            catch (Exception exception)
            {
                SetStatus($"本地视频扫描失败：{exception.Message}");
            }
        }

        private async UniTask SearchCatalog(string cursor)
        {
            if (m_ShowCdn is false || m_List == null)
            {
                return;
            }

            CancelSearch();
            m_SearchCancellation = CancellationTokenSource.CreateLinkedTokenSource(m_LifetimeCancellation.Token);
            var cancellationToken = m_SearchCancellation.Token;
            var requestVersion = ++m_RequestVersion;
            m_List.Clear();
            SetStatus("正在加载 CDN 视频…");
            try
            {
                var page = await m_CatalogClient.SearchAsync(
                    MediaKind.Video,
                    m_SearchField?.value,
                    cursor,
                    PageSize,
                    cancellationToken);
                if (requestVersion != m_RequestVersion || m_ShowCdn is false)
                {
                    return;
                }

                m_NextCursor = page.NextCursor;
                for (var i = 0; i < page.Items.Count; i++)
                {
                    AddCatalogItem(page.Items[i], requestVersion, cancellationToken);
                }

                SetStatus($"找到 {page.Items.Count} 个 CDN 视频。" +
                          (string.IsNullOrWhiteSpace(m_NextCursor) ? string.Empty : " 可继续翻页。"));
            }
            catch (OperationCanceledException)
            {
                if (requestVersion == m_RequestVersion && m_ShowCdn)
                {
                    SetStatus("目录请求已取消。");
                }
            }
            catch (CatalogException exception)
            {
                if (requestVersion == m_RequestVersion && m_ShowCdn)
                {
                    SetStatus($"目录错误 [{exception.Kind}]：{exception.Message}");
                }
            }
            catch (Exception exception)
            {
                if (requestVersion == m_RequestVersion && m_ShowCdn)
                {
                    SetStatus($"目录加载失败：{exception.Message}");
                }
            }
        }

        private void AddCatalogItem(CatalogItem item, int requestVersion, CancellationToken cancellationToken)
        {
            var card = CreateCard(item.Name, $"{item.Format} · {item.Width}×{item.Height}");
            card.RegisterCallback<ClickEvent>(_ => SelectCatalogItem(item));
            m_List.Add(card);
            if (string.IsNullOrWhiteSpace(item.ThumbnailLocation) is false)
            {
                RunAsync(LoadThumbnail(item, card, requestVersion, cancellationToken));
            }
        }

        private void AddLocalItem(VideoReference reference, int requestVersion)
        {
            var card = CreateCard(Path.GetFileName(reference.Primary.Location), reference.Format.ToString());
            card.RegisterCallback<ClickEvent>(_ =>
            {
                m_SelectedCatalogItem = null;
                m_SelectedReference = reference;
                RefreshDetails();
            });
            m_List.Add(card);
            RunAsync(LoadLocalThumbnail(reference, card, requestVersion, m_LifetimeCancellation.Token));
        }

        private async UniTask LoadThumbnail(
            CatalogItem item,
            VisualElement card,
            int requestVersion,
            CancellationToken cancellationToken)
        {
            string url;
            try
            {
                url = CatalogReferenceFactory.ExpandHttpsLocation(CatalogSettings.LoadOrCreate().CdnBaseUrl, item.ThumbnailLocation);
            }
            catch (CatalogException)
            {
                return;
            }

            if (s_ThumbnailCache.TryGet(url, out var cachedData))
            {
                AddDownloadedThumbnail(card, cachedData, requestVersion);
                return;
            }

            using (var request = UnityWebRequest.Get(url))
            using (cancellationToken.Register(request.Abort))
            {
                try
                {
                    await request.SendWebRequest();
                }
                catch (Exception)
                {
                    return;
                }

                if (requestVersion != m_RequestVersion || request.result != UnityWebRequest.Result.Success || card.panel == null)
                {
                    return;
                }

                var data = request.downloadHandler?.data;
                if (data == null || data.Length == 0)
                {
                    return;
                }

                s_ThumbnailCache.Set(url, data);
                AddDownloadedThumbnail(card, data, requestVersion);
            }
        }

        private async UniTask LoadLocalThumbnail(
            VideoReference reference,
            VisualElement card,
            int requestVersion,
            CancellationToken cancellationToken)
        {
            if (requestVersion != m_RequestVersion || m_ShowCdn || card.panel == null)
            {
                return;
            }

            var absolutePath = Path.GetFullPath(Path.Combine(
                Application.streamingAssetsPath,
                reference.Primary.Location.Replace('/', Path.DirectorySeparatorChar)));
            if (TryApplyCachedLocalThumbnail(absolutePath, card, requestVersion))
            {
                return;
            }

            await s_LocalThumbnailGate.WaitAsync(cancellationToken);
            try
            {
                if (requestVersion != m_RequestVersion || m_ShowCdn || card.panel == null)
                {
                    return;
                }

                if (TryApplyCachedLocalThumbnail(absolutePath, card, requestVersion))
                {
                    return;
                }

                var texture = await VideoThumbnailExtractor.ExtractAsync(absolutePath, cancellationToken);
                s_LocalThumbnailCache.TryStore(absolutePath, texture);
                if (requestVersion != m_RequestVersion || m_ShowCdn || card.panel == null)
                {
                    DestroyImmediate(texture);
                    return;
                }

                m_TemporaryTextures.Add(texture);
                SetCardThumbnail(card, texture);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (card.panel != null)
                {
                    var placeholder = card.Q<Label>("video-thumbnail-placeholder");
                    if (placeholder != null)
                    {
                        placeholder.text = "预览失败";
                    }

                    card.tooltip = $"{card.tooltip}\n预览生成失败：{exception.Message}";
                }
            }
            finally
            {
                s_LocalThumbnailGate.Release();
            }
        }

        private bool TryApplyCachedLocalThumbnail(
            string absolutePath,
            VisualElement card,
            int requestVersion)
        {
            if (s_LocalThumbnailCache.TryLoad(absolutePath, out var texture) is false)
            {
                return false;
            }

            if (requestVersion != m_RequestVersion || m_ShowCdn || card.panel == null)
            {
                DestroyImmediate(texture);
                return true;
            }

            m_TemporaryTextures.Add(texture);
            SetCardThumbnail(card, texture);
            return true;
        }

        private void AddDownloadedThumbnail(VisualElement card, byte[] data, int requestVersion)
        {
            if (requestVersion != m_RequestVersion || card.panel == null)
            {
                return;
            }

            var texture = new Texture2D(2, 2);
            if (texture.LoadImage(data) is false)
            {
                DestroyImmediate(texture);
                return;
            }

            m_TemporaryTextures.Add(texture);
            SetCardThumbnail(card, texture);
        }

        private static VisualElement CreateCard(string title, string subtitle)
        {
            var card = new VisualElement
            {
                tooltip = title ?? string.Empty,
                style =
                {
                    width = CardWidth,
                    height = 132f,
                    paddingLeft = 4f,
                    paddingRight = 4f,
                    paddingTop = 4f,
                    paddingBottom = 4f,
                    marginRight = 6f,
                    marginBottom = 6f,
                    borderLeftWidth = 1f,
                    borderRightWidth = 1f,
                    borderTopWidth = 1f,
                    borderBottomWidth = 1f,
                    borderLeftColor = new Color(0.28f, 0.28f, 0.28f),
                    borderRightColor = new Color(0.28f, 0.28f, 0.28f),
                    borderTopColor = new Color(0.28f, 0.28f, 0.28f),
                    borderBottomColor = new Color(0.28f, 0.28f, 0.28f)
                }
            };

            var preview = new VisualElement { name = "video-thumbnail-container" };
            preview.style.width = ThumbnailWidth;
            preview.style.height = ThumbnailHeight;
            preview.style.alignItems = Align.Center;
            preview.style.justifyContent = Justify.Center;
            preview.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            var placeholder = new Label("无预览") { name = "video-thumbnail-placeholder" };
            placeholder.style.color = new Color(0.55f, 0.55f, 0.55f);
            preview.Add(placeholder);
            card.Add(preview);

            var titleLabel = new Label(title ?? string.Empty);
            titleLabel.style.height = 18f;
            titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            titleLabel.style.overflow = Overflow.Hidden;
            card.Add(titleLabel);
            var subtitleLabel = new Label(subtitle ?? string.Empty);
            subtitleLabel.style.height = 16f;
            subtitleLabel.style.fontSize = 10f;
            subtitleLabel.style.color = new Color(0.65f, 0.65f, 0.65f);
            subtitleLabel.style.overflow = Overflow.Hidden;
            card.Add(subtitleLabel);
            return card;
        }

        private static void SetCardThumbnail(VisualElement card, Texture texture)
        {
            var container = card?.Q<VisualElement>("video-thumbnail-container");
            if (container == null)
            {
                return;
            }

            container.Clear();
            var image = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit };
            image.style.width = ThumbnailWidth;
            image.style.height = ThumbnailHeight;
            container.Add(image);
        }

        private void SelectCatalogItem(CatalogItem item)
        {
            try
            {
                m_SelectedCatalogItem = item;
                m_SelectedReference = CatalogReferenceFactory.CreateVideoReference(item, CatalogSettings.LoadOrCreate().CdnBaseUrl);
                RefreshDetails();
            }
            catch (CatalogException exception)
            {
                SetStatus($"无法使用该视频 [{exception.Kind}]：{exception.Message}");
            }
        }

        private void RefreshDetails()
        {
            m_Details?.Clear();
            if (m_SelectedReference == null)
            {
                m_Details?.Add(new Label("请选择一个视频查看详情。"));
                m_ConfirmButton?.SetEnabled(false);
                return;
            }

            var primary = m_SelectedReference.Primary;
            m_Details.Add(new Label(m_SelectedCatalogItem?.Name ?? Path.GetFileName(primary.Location)));
            m_Details.Add(new Label($"来源：{primary.Source}"));
            m_Details.Add(new Label($"Media ID：{primary.MediaId}"));
            m_Details.Add(new Label($"格式：{m_SelectedReference.Format}"));
            m_Details.Add(new Label($"位置：{primary.Location}"));
            if (m_SelectedCatalogItem != null)
            {
                m_Details.Add(new Label($"尺寸：{m_SelectedCatalogItem.Width}×{m_SelectedCatalogItem.Height}"));
                m_Details.Add(new Label($"码率：{m_SelectedCatalogItem.Bitrate}"));
                m_Details.Add(new Label($"时长：{m_SelectedCatalogItem.DurationMs} ms"));
            }

            m_Details.Add(new Label($"Renditions：{m_SelectedReference.Renditions.Count}"));
            RenderRenditions();
            RenderUsage();
            m_ConfirmButton?.SetEnabled(true);
        }

        private void RenderUsage()
        {
            m_Details.Add(new Label("视频使用情况"));
            if (m_UsageIndex == null || m_UsageIndex.IsAvailable is false)
            {
                m_Details.Add(new Label($"索引不可用：{m_UsageIndex?.ErrorMessage ?? "尚未构建"}"));
                m_Details.Add(new Button(RebuildUsage) { text = "重新构建索引" });
                return;
            }

            var usages = m_UsageIndex.Find(m_SelectedReference.Primary);
            if (usages.Count == 0)
            {
                m_Details.Add(new Label("未被任何剧情资产使用。"));
            }
            else
            {
                m_Details.Add(new Label($"使用 {usages.Count} 次"));
                for (var i = 0; i < usages.Count; i++)
                {
                    var usage = usages[i];
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    row.Add(new Label($"{usage.StoryId}/{usage.EpisodeId}/{usage.NodeId} {usage.NodeTitle}\n{usage.AssetPath}")
                    {
                        style = { flexGrow = 1f }
                    });
                    row.Add(new Button(() => PingUsage(usage)) { text = "定位" });
                    m_Details.Add(row);
                }
            }

            m_Details.Add(new Button(RebuildUsage) { text = "刷新使用索引" });
        }

        private void RebuildUsage()
        {
            m_UsageIndex ??= new UsageIndex();
            m_UsageIndex.Rebuild();
            RefreshDetails();
        }

        private static void PingUsage(MediaUsage usage)
        {
            var asset = AssetDatabase.LoadAssetAtPath<StoryEditor.Model.AuthoringAsset>(usage.AssetPath);
            if (asset == null)
            {
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void RenderRenditions()
        {
            var reference = m_ComposedReference ?? m_SelectedReference;
            if (reference == null || reference.Format != VideoFormat.Mp4)
            {
                return;
            }

            m_Details.Add(new Label("MP4 分辨率版本（主版本不可删除）"));
            if (reference.Renditions.Count == 0)
            {
                RenderMetadataEditor(reference, true);
                return;
            }

            for (var i = 0; i < reference.Renditions.Count; i++)
            {
                var index = i;
                var rendition = reference.Renditions[i];
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                row.Add(new Label($"{rendition.Label} · {rendition.Width}×{rendition.Height} · {rendition.Bitrate}bps")
                {
                    style = { flexGrow = 1f }
                });
                if (i > 0)
                {
                    row.Add(new Button(() =>
                    {
                        m_ComposedReference = VideoRenditionEditor.Remove(m_ComposedReference, index);
                        m_SelectedReference = m_ComposedReference;
                        RefreshDetails();
                    }) { text = "删除" });
                }

                m_Details.Add(row);
            }

            if (m_SelectedReference != null &&
                ReferenceEquals(m_SelectedReference, m_ComposedReference) is false &&
                m_SelectedReference.Format == VideoFormat.Mp4)
            {
                if (m_SelectedReference.Renditions.Count == 0)
                {
                    RenderMetadataEditor(m_SelectedReference, false);
                    return;
                }

                m_Details.Add(new Button(() =>
                {
                    try
                    {
                        m_ComposedReference = VideoRenditionEditor.Add(m_ComposedReference, m_SelectedReference);
                        m_SelectedReference = m_ComposedReference;
                        SetStatus("已添加 MP4 分辨率版本。");
                        RefreshDetails();
                    }
                    catch (Exception exception)
                    {
                        SetStatus($"无法添加分辨率版本：{exception.Message}");
                    }
                }) { text = "添加为当前视频的分辨率版本" });
            }
        }

        private void RenderMetadataEditor(VideoReference reference, bool primary)
        {
            m_Details.Add(new Label(primary
                ? "该 MP4 缺少尺寸/时长元数据，补齐后才能配置多分辨率。"
                : "候选 MP4 缺少尺寸/时长元数据，请先补齐。"));
            var width = new IntegerField("宽度") { value = 1920 };
            var height = new IntegerField("高度") { value = 1080 };
            var bitrate = new IntegerField("码率(bps)") { value = 0 };
            var duration = new LongField("时长(ms)") { value = 0L };
            m_Details.Add(width);
            m_Details.Add(height);
            m_Details.Add(bitrate);
            m_Details.Add(duration);
            m_Details.Add(new Button(() =>
            {
                try
                {
                    var enriched = VideoRenditionEditor.WithPrimaryMetadata(
                        reference,
                        width.value,
                        height.value,
                        bitrate.value,
                        duration.value);
                    if (primary)
                    {
                        m_ComposedReference = enriched;
                    }

                    m_SelectedReference = enriched;
                    SetStatus("MP4 元数据已补齐。");
                    RefreshDetails();
                }
                catch (Exception exception)
                {
                    SetStatus($"MP4 元数据无效：{exception.Message}");
                }
            }) { text = "应用元数据" });
        }

        private void ConfirmSelection()
        {
            if (m_SelectedReference == null)
            {
                return;
            }

            m_Confirmed?.Invoke(VideoReferenceCodec.Serialize(m_SelectedReference));
            Close();
        }

        private void CancelSearch()
        {
            m_RequestVersion++;
            m_SearchCancellation?.Cancel();
            m_SearchCancellation?.Dispose();
            m_SearchCancellation = null;
        }

        private void RunAsync(UniTask operation)
        {
            operation.Forget(exception => SetStatus($"媒体操作失败：{exception.Message}"));
        }

        private void SetStatus(string message)
        {
            if (m_Status != null)
            {
                m_Status.text = message ?? string.Empty;
            }
        }
    }

    internal sealed class VideoThumbnailDiskCache
    {
        public const string ProjectCacheRoot = "Library/GameDeveloperKit/StoryVideoThumbnails";

        private readonly string m_CacheRoot;

        public VideoThumbnailDiskCache()
            : this(GetProjectCacheRoot())
        {
        }

        internal VideoThumbnailDiskCache(string cacheRoot)
        {
            if (string.IsNullOrWhiteSpace(cacheRoot))
            {
                throw new ArgumentException("Thumbnail cache root cannot be empty.", nameof(cacheRoot));
            }

            m_CacheRoot = Path.GetFullPath(cacheRoot);
        }

        public bool TryLoad(string absoluteVideoPath, out Texture2D texture)
        {
            texture = null;
            Texture2D loadedTexture = null;
            string cachePath = null;
            try
            {
                cachePath = GetCachePath(absoluteVideoPath);
                if (IOFile.Exists(cachePath) is false)
                {
                    return false;
                }

                var data = IOFile.ReadAllBytes(cachePath);
                if (data.Length == 0)
                {
                    TryDeleteFile(cachePath);
                    return false;
                }

                loadedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = Path.GetFileNameWithoutExtension(absoluteVideoPath) + " Preview",
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (loadedTexture.LoadImage(data) is false)
                {
                    UnityEngine.Object.DestroyImmediate(loadedTexture);
                    TryDeleteFile(cachePath);
                    return false;
                }

                texture = loadedTexture;
                return true;
            }
            catch (Exception exception) when (IsRecoverableCacheException(exception))
            {
                if (loadedTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(loadedTexture);
                }

                TryDeleteFile(cachePath);
                return false;
            }
        }

        public bool TryStore(string absoluteVideoPath, Texture2D texture)
        {
            if (texture == null)
            {
                return false;
            }

            string temporaryPath = null;
            try
            {
                var cachePath = GetCachePath(absoluteVideoPath);
                var data = texture.EncodeToPNG();
                if (data == null || data.Length == 0)
                {
                    return false;
                }

                var directory = Path.GetDirectoryName(cachePath);
                Directory.CreateDirectory(directory);
                temporaryPath = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                IOFile.WriteAllBytes(temporaryPath, data);
                if (IOFile.Exists(cachePath))
                {
                    TryDeleteFile(temporaryPath);
                }
                else
                {
                    IOFile.Move(temporaryPath, cachePath);
                }

                temporaryPath = null;
                DeleteSupersededEntries(directory, cachePath);
                return true;
            }
            catch (Exception exception) when (IsRecoverableCacheException(exception))
            {
                return false;
            }
            finally
            {
                TryDeleteFile(temporaryPath);
            }
        }

        internal string GetCachePath(string absoluteVideoPath)
        {
            if (string.IsNullOrWhiteSpace(absoluteVideoPath))
            {
                throw new ArgumentException("Video path cannot be empty.", nameof(absoluteVideoPath));
            }

            var video = new FileInfo(Path.GetFullPath(absoluteVideoPath));
            if (video.Exists is false)
            {
                throw new FileNotFoundException("Video file does not exist.", video.FullName);
            }

            var normalizedPath = video.FullName.Replace('\\', '/');
            if (Path.DirectorySeparatorChar == '\\')
            {
                normalizedPath = normalizedPath.ToUpperInvariant();
            }

            var directory = Path.Combine(m_CacheRoot, ComputeHash(normalizedPath));
            var version = $"{video.Length:x16}-{video.LastWriteTimeUtc.Ticks:x16}.png";
            return Path.Combine(directory, version);
        }

        private static string GetProjectCacheRoot()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                return Path.GetFullPath(ProjectCacheRoot);
            }

            return Path.Combine(projectRoot, ProjectCacheRoot.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ComputeHash(string value)
        {
            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static void DeleteSupersededEntries(string directory, string currentCachePath)
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(directory, "*.png", SearchOption.TopDirectoryOnly))
                {
                    if (string.Equals(path, currentCachePath, StringComparison.OrdinalIgnoreCase) is false)
                    {
                        TryDeleteFile(path);
                    }
                }
            }
            catch (Exception exception) when (IsRecoverableCacheException(exception))
            {
                _ = exception;
            }
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                if (IOFile.Exists(path))
                {
                    IOFile.Delete(path);
                }
            }
            catch (Exception exception) when (IsRecoverableCacheException(exception))
            {
                _ = exception;
            }
        }

        private static bool IsRecoverableCacheException(Exception exception)
        {
            return exception is IOException ||
                   exception is UnauthorizedAccessException ||
                   exception is ArgumentException ||
                   exception is NotSupportedException ||
                   exception is CryptographicException ||
                   exception is UnityException;
        }
    }

    internal static class VideoThumbnailExtractor
    {
        private const string DecoderName = "StoryVideoThumbnailDecoder";

        public static async UniTask<Texture2D> ExtractAsync(
            string absolutePath,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                throw new ArgumentException("Video path cannot be empty.", nameof(absolutePath));
            }

            if (System.IO.File.Exists(absolutePath) is false)
            {
                throw new FileNotFoundException("Video file does not exist.", absolutePath);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var gameObject = new GameObject(DecoderName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var player = gameObject.AddComponent<AvProMediaPlayer>();
            player.AutoOpen = false;
            player.AutoStart = false;
            player.AudioMuted = true;
            try
            {
                if (player.OpenMedia(AvProMediaPathType.AbsolutePathOrURL, absolutePath, false) is false)
                {
                    throw new InvalidOperationException("AVPro 无法打开该视频。");
                }

                using (var updatePump = new EditorUpdatePump(player))
                {
                    await updatePump.FirstFrameReady.AttachExternalCancellation(cancellationToken).Timeout(
                        TimeSpan.FromSeconds(12),
                        DelayType.Realtime);
                    cancellationToken.ThrowIfCancellationRequested();
                    var frame = player.ExtractFrame(null);
                    if (frame == null)
                    {
                        throw new InvalidOperationException("视频没有可读取的画面帧。");
                    }

                    try
                    {
                        return ResizeThumbnail(frame, 320, 180);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(frame);
                    }
                }
            }
            finally
            {
                DisposePlayer(gameObject, player);
            }
        }

        private static void DisposePlayer(GameObject gameObject, AvProMediaPlayer player)
        {
            try
            {
                player.Stop();
            }
            finally
            {
                try
                {
                    player.CloseMedia();
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }
        }

        private static Texture2D ResizeThumbnail(Texture source, int maxWidth, int maxHeight)
        {
            var scale = Math.Min((double)maxWidth / source.width, (double)maxHeight / source.height);
            scale = Math.Min(1d, scale);
            var width = Math.Max(1, (int)Math.Round(source.width * scale));
            var height = Math.Max(1, (int)Math.Round(source.height * scale));
            var renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, renderTexture);
                RenderTexture.active = renderTexture;
                var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
                result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                result.Apply(false, false);
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private sealed class EditorUpdatePump : IDisposable
        {
            private readonly UniTaskCompletionSource m_FirstFrameReady = new UniTaskCompletionSource();
            private AvProMediaPlayer m_Player;
            private int m_LastRenderedFrame = -1;
            private bool m_PlaybackStarted;

            public EditorUpdatePump(AvProMediaPlayer player)
            {
                m_Player = player ?? throw new ArgumentNullException(nameof(player));
                EditorApplication.update += OnEditorUpdate;
            }

            public UniTask FirstFrameReady => m_FirstFrameReady.Task;

            public void Dispose()
            {
                EditorApplication.update -= OnEditorUpdate;
                m_Player = null;
            }

            private void OnEditorUpdate()
            {
                if (m_Player == null)
                {
                    return;
                }

                try
                {
                    m_Player.EditorUpdate();
                    if (Time.renderedFrameCount == m_LastRenderedFrame)
                    {
                        InternalEditorUtility.RepaintAllViews();
                        return;
                    }

                    m_LastRenderedFrame = Time.renderedFrameCount;
                    if (TryCompleteFirstFrame())
                    {
                        return;
                    }

                    TryStartPlayback();
                    TryCompleteFirstFrame();
                }
                catch (Exception exception)
                {
                    m_FirstFrameReady.TrySetException(exception);
                }
            }

            private void TryStartPlayback()
            {
                var control = m_Player.Control;
                var textureProducer = m_Player.TextureProducer;
                if (m_PlaybackStarted ||
                    control == null ||
                    textureProducer == null ||
                    control.CanPlay() is false)
                {
                    return;
                }

                m_Player.Play();
                m_PlaybackStarted = true;
            }

            private bool TryCompleteFirstFrame()
            {
                var textureProducer = m_Player.TextureProducer;
                if (textureProducer?.GetTexture() == null)
                {
                    return false;
                }

                var frameCount = textureProducer.GetTextureFrameCount();
                if (frameCount <= 0)
                {
                    return false;
                }

                m_FirstFrameReady.TrySetResult();
                return true;
            }
        }
    }

    internal sealed class AudioPickerWindow : EditorWindow
    {
        private const int PageSize = 30;
        private Action<string> m_Confirmed;
        private ICatalogClient m_CatalogClient;
        private CancellationTokenSource m_Cancellation;
        private TextField m_Search;
        private ScrollView m_List;
        private Label m_Status;
        private MediaReference? m_Selected;

        public static void Open(string currentValue, Action<string> confirmed)
        {
            var window = CreateInstance<AudioPickerWindow>();
            window.titleContent = new GUIContent("选择剧情音频");
            window.minSize = new Vector2(650f, 460f);
            window.m_Confirmed = confirmed;
            if (AudioReferenceCodec.TryDeserialize(currentValue, out var reference, out _)) window.m_Selected = reference;
            window.ShowAuxWindow();
        }

        private void OnEnable()
        {
            m_Cancellation = new CancellationTokenSource();
            m_CatalogClient = new CatalogClient(CatalogSettings.LoadOrCreate());
            BuildUi();
            RunAsync(ShowCdn());
        }

        private void OnDisable()
        {
            m_Cancellation?.Cancel();
            m_Cancellation?.Dispose();
        }

        private void BuildUi()
        {
            var tabs = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            tabs.Add(new Button(() => RunAsync(ShowCdn())) { text = "CDN" });
            tabs.Add(new Button(() => ShowReferences(AudioReferenceSources.ScanStreamingAssets(Path.Combine(Application.dataPath, "StreamingAssets")), "StreamingAssets")) { text = "StreamingAssets" });
            tabs.Add(new Button(() => ShowReferences(AudioReferenceSources.ReadResourceSnapshot(), "Resource")) { text = "Resource" });
            rootVisualElement.Add(tabs);
            var searchRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            m_Search = new TextField { style = { flexGrow = 1f } };
            searchRow.Add(m_Search);
            searchRow.Add(new Button(() => RunAsync(ShowCdn())) { text = "搜索" });
            rootVisualElement.Add(searchRow);
            m_Status = new Label();
            rootVisualElement.Add(m_Status);
            m_List = new ScrollView { style = { flexGrow = 1f } };
            rootVisualElement.Add(m_List);
            var footer = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd } };
            footer.Add(new Button(() => { m_Confirmed?.Invoke(string.Empty); Close(); }) { text = "清除" });
            footer.Add(new Button(Close) { text = "取消" });
            footer.Add(new Button(() => { if (m_Selected.HasValue) m_Confirmed?.Invoke(AudioReferenceCodec.Serialize(m_Selected.Value)); Close(); }) { text = "使用此音频" });
            rootVisualElement.Add(footer);
        }

        private async UniTask ShowCdn()
        {
            m_List.Clear();
            m_Status.text = "正在加载 CDN 音频…";
            try
            {
                var page = await m_CatalogClient.SearchAsync(MediaKind.Audio, m_Search?.value, null, PageSize, m_Cancellation.Token);
                for (var i = 0; i < page.Items.Count; i++)
                {
                    var item = page.Items[i];
                    AddReference(CatalogReferenceFactory.CreateAudioReference(item, CatalogSettings.LoadOrCreate().CdnBaseUrl), item.Name);
                }
                m_Status.text = $"找到 {page.Items.Count} 个 CDN 音频。";
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                m_Status.text = $"CDN 音频加载失败：{exception.Message}";
            }
        }

        private void ShowReferences(IReadOnlyList<MediaReference> references, string source)
        {
            m_List.Clear();
            for (var i = 0; i < references.Count; i++) AddReference(references[i], Path.GetFileName(references[i].Location));
            m_Status.text = $"找到 {references.Count} 个 {source} 音频。";
        }

        private void AddReference(MediaReference reference, string name)
        {
            var button = new Button(() => { m_Selected = reference; m_Status.text = $"已选择：{reference.Location}"; })
            {
                text = $"{name}\n{reference.Source} · {reference.Location}"
            };
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            m_List.Add(button);
        }

        private static void RunAsync(UniTask task)
        {
            task.Forget(Debug.LogException);
        }
    }

}
