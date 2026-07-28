using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorCloud;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.LubanConfigEditor.UI;
using GameDeveloperKit.MediaEditor;
using GameDeveloperKit.Story.Media;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace GameDeveloperKit.StoryEditor.Media
{
    internal sealed class HlsMediaLibraryWindow : EditorWindow
    {
        private const int PageSize = 100;
        private const float PreviewWidth = 112f;
        private const float PreviewHeight = 63f;
        private static readonly ThumbnailSessionCache s_ThumbnailCache = new ThumbnailSessionCache();

        private readonly List<Texture2D> m_Textures = new List<Texture2D>();
        private CatalogClient m_CatalogClient;
        private HlsCatalogOriginRepository m_CatalogRepository;
        private HlsRemoteObjectCleaner m_RemoteCleaner;
        private CancellationTokenSource m_LifetimeCancellation;
        private CancellationTokenSource m_RequestCancellation;
        private TextField m_SearchField;
        private Button m_AddButton;
        private Button m_RefreshButton;
        private Button m_NextPageButton;
        private Button m_RetryCleanupButton;
        private Label m_Status;
        private ScrollView m_List;
        private ScrollView m_Details;
        private string m_NextCursor;
        private int m_RequestVersion;
        private CatalogItem m_Selected;
        private bool m_Busy;
        private string m_PendingCleanupMediaId;

        [MenuItem("GameDeveloperKit/媒体/HLS 流媒体库")]
        public static void Open()
        {
            var window = GetWindow<HlsMediaLibraryWindow>();
            window.titleContent = new GUIContent("HLS 流媒体库");
            window.minSize = new Vector2(980f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            m_LifetimeCancellation = new CancellationTokenSource();
            m_CatalogClient = new CatalogClient(EditorGlobalConfig.LoadOrCreate().StoryMedia);
            m_CatalogRepository = new HlsCatalogOriginRepository();
            m_RemoteCleaner = new HlsRemoteObjectCleaner();
            BuildUi();
            Run(LoadPageAsync(null, false));
        }

        private void OnDisable()
        {
            m_RequestCancellation?.Cancel();
            m_RequestCancellation?.Dispose();
            m_RequestCancellation = null;
            m_LifetimeCancellation?.Cancel();
            m_LifetimeCancellation?.Dispose();
            m_LifetimeCancellation = null;
            DestroyTextures();
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1f;

            var header = new VisualElement
            {
                name = "hls-library-header",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 8f,
                    paddingRight = 8f,
                    paddingTop = 7f,
                    paddingBottom = 7f,
                    borderBottomWidth = 1f
                }
            };
            header.style.borderBottomColor = new Color(0f, 0f, 0f, 0.28f);

            m_AddButton = new Button { name = "hls-library-add", text = "+", tooltip = "添加 HLS 视频" };
            m_AddButton.style.width = 30f;
            m_AddButton.style.height = 24f;
            m_AddButton.clicked += () => Run(SelectMp4Async());
            header.Add(m_AddButton);

            m_RefreshButton = new Button(() => Run(LoadPageAsync(null, true)))
            {
                name = "hls-library-refresh",
                text = "刷新"
            };
            m_RefreshButton.style.marginLeft = 5f;
            header.Add(m_RefreshButton);

            m_RetryCleanupButton = new Button(() => Run(RetryCleanupAsync()))
            {
                name = "hls-library-retry-cleanup",
                text = "重试清理"
            };
            m_RetryCleanupButton.style.marginLeft = 5f;
            m_RetryCleanupButton.style.display = DisplayStyle.None;
            header.Add(m_RetryCleanupButton);

            m_SearchField = new TextField
            {
                name = "hls-library-search",
                isDelayed = true,
                tooltip = "名称、上传人或 Media ID"
            };
            m_SearchField.style.flexGrow = 1f;
            m_SearchField.style.marginLeft = 10f;
            m_SearchField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    Run(LoadPageAsync(null, false));
                }
            });
            header.Add(m_SearchField);
            header.Add(new Button(() => Run(LoadPageAsync(null, false)))
            {
                name = "hls-library-search-button",
                text = "搜索"
            });
            rootVisualElement.Add(header);

            m_Status = new Label("正在加载 CDN 媒体库…")
            {
                name = "hls-library-status",
                style =
                {
                    paddingLeft = 10f,
                    paddingRight = 10f,
                    paddingTop = 5f,
                    paddingBottom = 5f
                }
            };
            rootVisualElement.Add(m_Status);

            var content = new VisualElement
            {
                name = "hls-library-content",
                style = { flexGrow = 1f, flexDirection = FlexDirection.Row, minWidth = 0f }
            };
            var listPane = new VisualElement { name = "hls-library-list-pane" };
            listPane.style.flexGrow = 7f;
            listPane.style.flexBasis = 0f;
            listPane.style.minWidth = 0f;
            listPane.Add(CreateListHeader());
            m_List = new ScrollView { name = "hls-library-list", style = { flexGrow = 1f } };
            m_List.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction(
                    "添加视频",
                    _ => Run(SelectMp4Async()),
                    _ => MenuStatus());
                evt.menu.AppendAction(
                    "刷新",
                    _ => Run(LoadPageAsync(null, true)),
                    _ => MenuStatus());
                if (string.IsNullOrWhiteSpace(m_PendingCleanupMediaId) is false)
                {
                    evt.menu.AppendAction(
                        "重试清理云端对象",
                        _ => Run(RetryCleanupAsync()),
                        _ => MenuStatus());
                }
            }));
            listPane.Add(m_List);

            m_NextPageButton = new Button(() => Run(LoadPageAsync(m_NextCursor, false)))
            {
                name = "hls-library-next-page",
                text = "下一页"
            };
            m_NextPageButton.style.alignSelf = Align.FlexEnd;
            m_NextPageButton.style.marginRight = 8f;
            m_NextPageButton.style.marginTop = 5f;
            m_NextPageButton.style.marginBottom = 6f;
            m_NextPageButton.SetEnabled(false);
            listPane.Add(m_NextPageButton);
            content.Add(listPane);

            m_Details = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "hls-library-details",
                style =
                {
                    flexGrow = 3f,
                    flexBasis = 0f,
                    minWidth = 0f,
                    paddingLeft = 12f,
                    paddingRight = 12f,
                    borderLeftWidth = 1f
                }
            };
            m_Details.style.borderLeftColor = new Color(0f, 0f, 0f, 0.28f);
            content.Add(m_Details);
            rootVisualElement.Add(content);
            RefreshDetails();
        }

        private async UniTask SelectMp4Async()
        {
            if (EnsureCloudCredentialConfigured() is false)
            {
                return;
            }

            var sourcePath = EditorUtility.OpenFilePanel("选择需要发布的 MP4", InitialDirectory(), "mp4");
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return;
            }

            if (string.Equals(System.IO.Path.GetExtension(sourcePath), ".mp4", StringComparison.OrdinalIgnoreCase) is false)
            {
                EditorUtility.DisplayDialog("无法添加视频", "请选择 MP4 文件。", "确定");
                return;
            }

            SetBusy(true);
            m_Status.text = "正在计算源视频指纹…";
            try
            {
                var fingerprint = await HlsPublishWorkflow.ComputeSourceSha256Async(
                    sourcePath,
                    m_LifetimeCancellation.Token);
                var origin = await m_CatalogRepository.LoadOriginAsync(
                    m_LifetimeCancellation.Token);
                var existing = origin.Document.Items.FirstOrDefault(item =>
                    string.Equals(item.SourceSha256, fingerprint, StringComparison.Ordinal));
                if (existing != null && EditorUtility.DisplayDialog(
                        "覆盖 HLS 流媒体",
                        "已存在 HLS 流媒体，继续将覆盖云端资源，是否继续？",
                        "继续覆盖",
                        "取消") is false)
                {
                    m_Status.text = "已取消覆盖。";
                    return;
                }

                var intent = new HlsPublishIntent(
                    sourcePath.Replace('\\', '/'),
                    existing?.Name ?? System.IO.Path.GetFileNameWithoutExtension(sourcePath),
                    fingerprint,
                    existing?.MediaId ?? HlsPublishWorkflow.CreateMediaId(),
                    existing != null,
                    existing?.CreatedAtUtc,
                    existing?.UpdatedAtUtc);
                HlsTranscodeWindow.OpenForPublish(intent, _ =>
                {
                    Focus();
                    Run(LoadPageAsync(null, true));
                });
                m_Status.text = "已打开 HLS 转码与发布任务。";
            }
            catch (OperationCanceledException)
            {
                m_Status.text = "已取消添加视频。";
            }
            catch (Exception exception)
            {
                m_Status.text = "无法添加视频：" + exception.Message;
            }
            finally
            {
                SetBusy(false);
            }
        }

        private bool EnsureCloudCredentialConfigured()
        {
            var cloud = EditorGlobalConfig.LoadOrCreate().Cloud;
            try
            {
                if (new CloudCredentialStore().TryGet(
                        cloud.ProviderId,
                        cloud.CredentialProfileName,
                        out _))
                {
                    return true;
                }

                var providerName = string.Equals(
                    cloud.ProviderId,
                    CloudProviderId.AliyunOss,
                    StringComparison.Ordinal)
                    ? "阿里云 OSS"
                    : "腾讯 COS";
                var scope = $"{providerName} / {cloud.CredentialProfileName}";
                m_Status.text = $"无法添加视频：本机未保存云凭证（{scope}）。";
                if (EditorUtility.DisplayDialog(
                        "缺少云凭证",
                        $"当前项目使用 {scope}，但本机尚未保存该凭证。",
                        "打开云配置",
                        "取消"))
                {
                    MainWindow.OpenCloudConfiguration();
                }

                return false;
            }
            catch (CloudException exception)
            {
                m_Status.text = "无法添加视频：" + exception.Message;
                if (EditorUtility.DisplayDialog(
                        "云配置不可用",
                        exception.Message,
                        "打开云配置",
                        "取消"))
                {
                    MainWindow.OpenCloudConfiguration();
                }

                return false;
            }
        }

        private async UniTask LoadPageAsync(string cursor, bool bypassCache)
        {
            m_RequestCancellation?.Cancel();
            m_RequestCancellation?.Dispose();
            m_RequestCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                m_LifetimeCancellation?.Token ?? CancellationToken.None);
            var token = m_RequestCancellation.Token;
            var requestVersion = ++m_RequestVersion;
            SetBusy(true);
            m_Status.text = "正在加载 CDN 媒体库…";
            try
            {
                var page = await m_CatalogClient.SearchAsync(
                    MediaKind.Video,
                    m_SearchField?.value,
                    cursor,
                    PageSize,
                    bypassCache,
                    token);
                if (requestVersion != m_RequestVersion)
                {
                    return;
                }

                RenderPage(page);
                m_Status.text = page.Items.Count == 0
                    ? "暂无 HLS 流媒体。"
                    : $"当前页 {page.Items.Count} 个 HLS 视频。";
            }
            catch (OperationCanceledException)
            {
                if (requestVersion == m_RequestVersion)
                {
                    m_Status.text = "目录请求已取消。";
                }
            }
            catch (CatalogException exception)
            {
                if (requestVersion == m_RequestVersion)
                {
                    ClearPage();
                    m_Status.text = $"目录错误 [{exception.Kind}]：{exception.Message}";
                }
            }
            catch (Exception exception)
            {
                if (requestVersion == m_RequestVersion)
                {
                    ClearPage();
                    m_Status.text = "目录加载失败：" + exception.Message;
                }
            }
            finally
            {
                if (requestVersion == m_RequestVersion)
                {
                    SetBusy(false);
                }
            }
        }

        private void RenderPage(CatalogPage page)
        {
            ClearPage();
            m_NextCursor = page.NextCursor;
            for (var index = 0; index < page.Items.Count; index++)
            {
                m_List.Add(CreateItemRow(page.Items[index], m_RequestVersion, m_RequestCancellation.Token));
            }

            m_NextPageButton.SetEnabled(string.IsNullOrWhiteSpace(m_NextCursor) is false);
        }

        private VisualElement CreateItemRow(
            CatalogItem item,
            int requestVersion,
            CancellationToken cancellationToken)
        {
            var row = new VisualElement
            {
                name = "hls-library-item-" + item.MediaId,
                style =
                {
                    height = 72f,
                    minHeight = 72f,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    borderBottomWidth = 1f,
                    paddingLeft = 7f,
                    paddingRight = 7f
                }
            };
            row.style.borderBottomColor = new Color(0f, 0f, 0f, 0.18f);
            row.Add(CreatePreview(item, requestVersion, cancellationToken));
            row.Add(CreateCell(item.Name, 150f, true));
            row.Add(CreateCell(string.IsNullOrWhiteSpace(item.Uploader) ? "-" : item.Uploader, 90f));
            row.Add(CreateCell(FormatDate(item.UpdatedAtUtc), 126f));
            row.Add(CreateCell($"{item.Width}x{item.Height}", 82f));
            row.Add(CreateCell(FormatDuration(item.DurationMs), 68f));
            row.RegisterCallback<ClickEvent>(_ => Select(item, row));
            row.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("查看信息", _ => Select(item, row));
                evt.menu.AppendAction(
                    "重命名",
                    _ => OpenRename(item),
                    _ => MenuStatus());
                evt.menu.AppendAction("复制播放地址", _ => CopyPlaybackUrl(item));
                evt.menu.AppendAction(
                    "删除",
                    _ => Run(DeleteAsync(item)),
                    _ => MenuStatus());
                evt.menu.AppendSeparator();
                evt.menu.AppendAction(
                    "刷新",
                    _ => Run(LoadPageAsync(null, true)),
                    _ => MenuStatus());
            }));
            return row;
        }

        private VisualElement CreatePreview(
            CatalogItem item,
            int requestVersion,
            CancellationToken cancellationToken)
        {
            var preview = new VisualElement
            {
                name = "hls-library-preview",
                style =
                {
                    width = PreviewWidth,
                    height = PreviewHeight,
                    minWidth = PreviewWidth,
                    marginRight = 9f,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    backgroundColor = new Color(0f, 0f, 0f, 0.18f)
                }
            };
            preview.Add(new Label("无预览") { name = "hls-library-preview-placeholder" });
            if (string.IsNullOrWhiteSpace(item.ThumbnailLocation) is false)
            {
                Run(LoadPreviewAsync(item, preview, requestVersion, cancellationToken));
            }

            return preview;
        }

        private async UniTask LoadPreviewAsync(
            CatalogItem item,
            VisualElement preview,
            int requestVersion,
            CancellationToken cancellationToken)
        {
            string url;
            try
            {
                url = CatalogReferenceFactory.ExpandHttpsLocation(
                    EditorGlobalConfig.LoadOrCreate().StoryMedia.CdnBaseUrl,
                    item.ThumbnailLocation);
                if (item.UpdatedAtUtc.HasValue)
                {
                    url += (url.IndexOf('?', StringComparison.Ordinal) >= 0 ? "&" : "?") +
                           "v=" + item.UpdatedAtUtc.Value.UtcDateTime.Ticks;
                }
            }
            catch (CatalogException)
            {
                return;
            }

            byte[] bytes;
            if (s_ThumbnailCache.TryGet(url, out var cached))
            {
                bytes = cached;
            }
            else
            {
                using (var request = UnityWebRequest.Get(url))
                using (cancellationToken.Register(request.Abort))
                {
                    request.timeout = EditorGlobalConfig.LoadOrCreate().StoryMedia.TimeoutSeconds;
                    await request.SendWebRequest();
                    cancellationToken.ThrowIfCancellationRequested();
                    if (request.result != UnityWebRequest.Result.Success || request.responseCode < 200 || request.responseCode >= 300)
                    {
                        return;
                    }

                    bytes = request.downloadHandler?.data;
                    if (bytes == null || bytes.Length == 0)
                    {
                        return;
                    }

                    s_ThumbnailCache.Set(url, bytes);
                }
            }

            if (requestVersion != m_RequestVersion)
            {
                return;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (texture.LoadImage(bytes, true) is false)
            {
                DestroyImmediate(texture);
                return;
            }

            m_Textures.Add(texture);
            preview.Clear();
            preview.Add(new Image
            {
                image = texture,
                scaleMode = ScaleMode.ScaleToFit,
                style = { width = PreviewWidth, height = PreviewHeight }
            });
        }

        private void Select(CatalogItem item, VisualElement row)
        {
            m_Selected = item;
            foreach (var child in m_List.Children())
            {
                child.style.backgroundColor = Color.clear;
            }

            row.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.20f, 0.37f, 0.52f, 0.65f)
                : new Color(0.25f, 0.55f, 0.85f, 0.35f);
            RefreshDetails();
        }

        private void OpenRename(CatalogItem item)
        {
            if (m_Busy)
            {
                return;
            }

            HlsMediaRenameWindow.Open(item.Name, newName => Run(RenameAsync(item, newName)));
        }

        private async UniTask RenameAsync(CatalogItem item, string newName)
        {
            SetBusy(true);
            m_Status.text = $"正在重命名“{item.Name}”…";
            try
            {
                await m_CatalogRepository.RenameAsync(
                    item.MediaId,
                    item.UpdatedAtUtc,
                    newName,
                    Environment.UserName,
                    m_LifetimeCancellation.Token);
                await LoadPageAsync(null, true);
                m_Status.text = $"已重命名为“{newName.Trim()}”。";
            }
            catch (OperationCanceledException)
            {
                m_Status.text = "重命名已取消。";
            }
            catch (CatalogException exception)
            {
                m_Status.text = $"重命名失败 [{exception.Kind}]：{exception.Message}";
            }
            catch (CloudException exception)
            {
                m_Status.text = $"重命名失败 [{exception.Kind}]：{exception.Message}";
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async UniTask DeleteAsync(CatalogItem item)
        {
            if (EditorUtility.DisplayDialog(
                    "删除 HLS 流媒体",
                    $"将从媒体库删除“{item.Name}”，并清理云端目录 {item.ObjectPrefix}\n此操作不可撤销，是否继续？",
                    "删除",
                    "取消") is false)
            {
                return;
            }

            var catalogRemoved = false;
            SetBusy(true);
            m_Status.text = $"正在从 Catalog 删除“{item.Name}”…";
            try
            {
                await m_CatalogRepository.RemoveAsync(
                    item.MediaId,
                    item.UpdatedAtUtc,
                    m_LifetimeCancellation.Token);
                catalogRemoved = true;
                m_Status.text = "Catalog 已更新，正在清理云端对象…";
                var cleanup = await m_RemoteCleaner.CleanupAsync(
                    item.MediaId,
                    m_LifetimeCancellation.Token);
                if (cleanup.IsSuccess)
                {
                    ClearPendingCleanup(item.MediaId);
                }
                else
                {
                    SetPendingCleanup(item.MediaId);
                }

                await LoadPageAsync(null, true);
                m_Status.text = cleanup.IsSuccess
                    ? $"已删除“{item.Name}”，并清理 {cleanup.SucceededCount} 个云端对象。"
                    : $"已从 Catalog 删除“{item.Name}”，但有 {cleanup.Failed.Count} 个云端对象清理失败，可点击“重试清理”。";
            }
            catch (OperationCanceledException)
            {
                if (catalogRemoved)
                {
                    SetPendingCleanup(item.MediaId);
                    m_Status.text = "Catalog 已删除，但云端清理已取消，可点击“重试清理”。";
                }
                else
                {
                    m_Status.text = "删除已取消。";
                }
            }
            catch (Exception exception) when (
                exception is CatalogException ||
                exception is CloudException)
            {
                if (catalogRemoved)
                {
                    SetPendingCleanup(item.MediaId);
                    await LoadPageAsync(null, true);
                    m_Status.text = "Catalog 已删除，但云端清理失败，可点击“重试清理”：" + exception.Message;
                }
                else
                {
                    m_Status.text = "删除失败：" + exception.Message;
                }
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async UniTask RetryCleanupAsync()
        {
            var mediaId = m_PendingCleanupMediaId;
            if (string.IsNullOrWhiteSpace(mediaId))
            {
                return;
            }

            SetBusy(true);
            m_Status.text = $"正在重试清理 {mediaId} 的云端对象…";
            try
            {
                var cleanup = await m_RemoteCleaner.CleanupAsync(
                    mediaId,
                    m_LifetimeCancellation.Token);
                if (cleanup.IsSuccess)
                {
                    ClearPendingCleanup(mediaId);
                    m_Status.text = $"云端清理完成，共清理 {cleanup.SucceededCount} 个对象。";
                }
                else
                {
                    m_Status.text = $"仍有 {cleanup.Failed.Count} 个云端对象清理失败。";
                }
            }
            catch (OperationCanceledException)
            {
                m_Status.text = "云端清理已取消。";
            }
            catch (CloudException exception)
            {
                m_Status.text = $"云端清理失败 [{exception.Kind}]：{exception.Message}";
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void CopyPlaybackUrl(CatalogItem item)
        {
            try
            {
                EditorGUIUtility.systemCopyBuffer = CatalogReferenceFactory.ExpandHttpsLocation(
                    EditorGlobalConfig.LoadOrCreate().StoryMedia.CdnBaseUrl,
                    item.Location);
                m_Status.text = "播放地址已复制。";
            }
            catch (CatalogException exception)
            {
                m_Status.text = $"复制失败 [{exception.Kind}]：{exception.Message}";
            }
        }

        private void RefreshDetails()
        {
            m_Details?.Clear();
            if (m_Selected == null)
            {
                m_Details?.Add(new Label("未选择视频。"));
                return;
            }

            AddDetail("名称", m_Selected.Name);
            AddDetail("Media ID", m_Selected.MediaId);
            AddDetail("上传人", string.IsNullOrWhiteSpace(m_Selected.Uploader) ? "-" : m_Selected.Uploader);
            AddDetail("创建时间", FormatDate(m_Selected.CreatedAtUtc));
            AddDetail("更新时间", FormatDate(m_Selected.UpdatedAtUtc));
            AddDetail("源文件", string.IsNullOrWhiteSpace(m_Selected.SourceFileName) ? "-" : m_Selected.SourceFileName);
            AddDetail("SHA-256", string.IsNullOrWhiteSpace(m_Selected.SourceSha256) ? "-" : m_Selected.SourceSha256);
            AddDetail("播放位置", m_Selected.Location);
            AddDetail("预览图", string.IsNullOrWhiteSpace(m_Selected.ThumbnailLocation) ? "-" : m_Selected.ThumbnailLocation);
            AddDetail("分辨率", $"{m_Selected.Width}x{m_Selected.Height}");
            AddDetail("码率", m_Selected.Bitrate.ToString(CultureInfo.InvariantCulture));
            AddDetail("时长", FormatDuration(m_Selected.DurationMs));
            AddDetail("Renditions", m_Selected.Renditions.Count.ToString(CultureInfo.InvariantCulture));
        }

        private void AddDetail(string label, string value)
        {
            var title = new Label(label)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginTop = 9f,
                    marginBottom = 2f
                }
            };
            m_Details.Add(title);
            m_Details.Add(new Label(value ?? string.Empty)
            {
                tooltip = value ?? string.Empty,
                style = { minWidth = 0f, whiteSpace = WhiteSpace.Normal }
            });
        }

        private void ClearPage()
        {
            m_Selected = null;
            m_NextCursor = string.Empty;
            m_List?.Clear();
            m_NextPageButton?.SetEnabled(false);
            DestroyTextures();
            RefreshDetails();
        }

        private void SetBusy(bool busy)
        {
            m_Busy = busy;
            m_AddButton?.SetEnabled(busy is false);
            m_RefreshButton?.SetEnabled(busy is false);
            m_SearchField?.SetEnabled(busy is false);
            m_NextPageButton?.SetEnabled(busy is false && string.IsNullOrWhiteSpace(m_NextCursor) is false);
            m_RetryCleanupButton?.SetEnabled(
                busy is false && string.IsNullOrWhiteSpace(m_PendingCleanupMediaId) is false);
        }

        private DropdownMenuAction.Status MenuStatus()
        {
            return m_Busy
                ? DropdownMenuAction.Status.Disabled
                : DropdownMenuAction.Status.Normal;
        }

        private void SetPendingCleanup(string mediaId)
        {
            m_PendingCleanupMediaId = mediaId;
            if (m_RetryCleanupButton != null)
            {
                m_RetryCleanupButton.style.display = DisplayStyle.Flex;
                m_RetryCleanupButton.SetEnabled(m_Busy is false);
            }
        }

        private void ClearPendingCleanup(string mediaId)
        {
            if (string.Equals(m_PendingCleanupMediaId, mediaId, StringComparison.Ordinal) is false)
            {
                return;
            }

            m_PendingCleanupMediaId = string.Empty;
            if (m_RetryCleanupButton != null)
            {
                m_RetryCleanupButton.style.display = DisplayStyle.None;
                m_RetryCleanupButton.SetEnabled(false);
            }
        }

        private static string InitialDirectory()
        {
            return System.IO.Directory.Exists(Application.dataPath)
                ? Application.dataPath
                : System.IO.Directory.GetCurrentDirectory();
        }

        private void DestroyTextures()
        {
            foreach (var texture in m_Textures)
            {
                if (texture != null)
                {
                    DestroyImmediate(texture);
                }
            }

            m_Textures.Clear();
        }

        private static VisualElement CreateListHeader()
        {
            var header = new VisualElement
            {
                name = "hls-library-list-header",
                style =
                {
                    height = 26f,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 7f,
                    paddingRight = 7f,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            header.Add(CreateCell("预览", PreviewWidth + 9f));
            header.Add(CreateCell("名称", 150f, true));
            header.Add(CreateCell("上传人", 90f));
            header.Add(CreateCell("更新时间", 126f));
            header.Add(CreateCell("分辨率", 82f));
            header.Add(CreateCell("时长", 68f));
            return header;
        }

        private static Label CreateCell(string text, float width, bool grow = false)
        {
            var label = new Label(text ?? string.Empty)
            {
                style =
                {
                    width = width,
                    minWidth = width,
                    overflow = Overflow.Hidden,
                    whiteSpace = WhiteSpace.NoWrap,
                    paddingRight = 6f
                }
            };
            if (grow)
            {
                label.style.flexGrow = 1f;
            }

            return label;
        }

        private static string FormatDate(DateTimeOffset? value)
        {
            return value.HasValue
                ? value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                : "-";
        }

        private static string FormatDuration(long durationMs)
        {
            if (durationMs <= 0)
            {
                return "-";
            }

            return TimeSpan.FromMilliseconds(durationMs).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        }

        private static void Run(UniTask operation)
        {
            operation.Forget(exception => Debug.LogException(exception));
        }
    }

    internal sealed class HlsMediaRenameWindow : EditorWindow
    {
        private Action<string> m_Confirmed;
        private TextField m_NameField;

        public static void Open(string currentName, Action<string> confirmed)
        {
            var window = CreateInstance<HlsMediaRenameWindow>();
            window.titleContent = new GUIContent("重命名 HLS 视频");
            window.minSize = new Vector2(360f, 105f);
            window.maxSize = new Vector2(520f, 105f);
            window.m_Confirmed = confirmed;
            window.BuildUi(currentName);
            window.ShowUtility();
            window.Focus();
        }

        private void BuildUi(string currentName)
        {
            rootVisualElement.style.paddingLeft = 10f;
            rootVisualElement.style.paddingRight = 10f;
            rootVisualElement.style.paddingTop = 10f;
            rootVisualElement.style.paddingBottom = 10f;
            m_NameField = new TextField("名称")
            {
                value = currentName ?? string.Empty,
                isDelayed = false
            };
            m_NameField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    Confirm();
                }
            });
            rootVisualElement.Add(m_NameField);

            var actions = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd,
                    marginTop = 12f
                }
            };
            actions.Add(new Button(Close) { text = "取消" });
            var confirm = new Button(Confirm) { text = "确定" };
            confirm.style.marginLeft = 6f;
            actions.Add(confirm);
            rootVisualElement.Add(actions);
            m_NameField.schedule.Execute(() =>
            {
                m_NameField.Focus();
                m_NameField.SelectAll();
            });
        }

        private void Confirm()
        {
            var value = m_NameField?.value?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                ShowNotification(new GUIContent("名称不能为空"));
                return;
            }

            var confirmed = m_Confirmed;
            Close();
            confirmed?.Invoke(value);
        }
    }
}
