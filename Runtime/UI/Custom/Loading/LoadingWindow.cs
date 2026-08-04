using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit;
using GameDeveloperKit.Media;
using GameDeveloperKit.Playable;
using GameDeveloperKit.Resource;
using GameDeveloperKit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed partial class LoadingWindow : UIWindow, IProcessingWindow
{
    /// <summary>
    /// 登录界面背景：Login_1（StreamingAssets 相对路径，master 清单）。
    /// </summary>
    public const string DefaultBackgroundVideoRelativePath =
        "videos/media-8ccf5c01769e4905/master.m3u8";

    /// <summary>
    /// 登录背景固定清晰度（禁用 HLS 自适应切换）。
    /// </summary>
    public const int FixedBackgroundVideoHeight = 2160;

    /// <summary>
    /// 登录背景视频就绪前的预览图（Resources 相对路径，无扩展名）。视频资源缺失/加载失败时保持静态背景。
    /// 取自 HLS 媒体库：https://moviegame.wwhy.games/videos/media-8ccf5c01769e4905/preview.jpg
    /// 放 Resources 以便同步加载立即显示，避免视频就绪前白屏。
    /// </summary>
    public const string DefaultBackgroundPreviewPath = "Images/Loading/preview";

    /// <summary>
    /// 高清登录背景预览（AssetBundle 地址）：打包时随 Preview 目录进入 bf1e7b40 bundle。
    /// bundle 加载成功后替换 Resources 兜底图。
    /// </summary>
    public const string BackgroundPreviewBundlePath =
        "Assets/Bundles/Images/Preview/loading_preview.png";

    private GameObject m_LoadPanel;
    private GameObject m_LoginPanel;
    private GameObject m_LoginPopup;
    private GameObject m_EnterPanel;
    private UniTaskCompletionSource m_EnterClickSource;
    private TextMeshProUGUI m_AgreementText;
    private TmpHyperlinkClickHandler m_AgreementLinkHandler;

    private RawImage m_BackgroundRawImage;
    private VideoPlayer m_LegacyBackgroundVideoPlayer;
    private VideoPlayableHandle m_BackgroundVideo;
    private RawImage m_BackgroundPreviewImage;
    private AssetHandle m_BackgroundPreviewHandle;
    private CancellationTokenSource m_BackgroundVideoCancellation;
    private string m_BackgroundVideoRelativePath;
    private Texture m_BoundBackgroundTexture;
    private bool m_BoundBackgroundVerticalFlip;
    private int m_BackgroundVideoRequestVersion;

    public bool IsAgreementAccepted => toggle_agreement != null && toggle_agreement.isOn;

    /// <summary>
    /// 点击进入登录但未勾选协议时回调（由上层弹出提示）。
    /// </summary>
    public Action OnAgreementRequired { get; set; }

    public override async UniTask OnAwakeAsync()
    {
        await InitializeDesignAsync();
        CachePanels();
        CacheBackgroundView();
        BindAgreementLinks();
        BindButtons();
        ShowLoadPanel();
        // 在窗口交互前把背景首帧贴上，避免登录页黑屏。
        // 不阻塞窗口初始化：视频资源缺失/加载失败时仅记录日志，保持预览图静态背景。
        SetBackgroundVideoAsync(DefaultBackgroundVideoRelativePath)
            .Forget(exception => Debug.LogWarning(
                $"[LoadingWindow] Background video load failed: {exception.Message}"));
    }

    public override UniTask OnOpenAsync()
    {
        ShowLoadPanel();
        return UniTask.CompletedTask;
    }

    public override void OnUpdate(float deltaTime, float unscaledDeltaTime)
    {
        RefreshBackgroundSurface();
        base.OnUpdate(deltaTime, unscaledDeltaTime);
    }

    public override void Release()
    {
        UnbindButtons();
        UnbindAgreementLinks();
        OnAgreementRequired = null;
        m_EnterClickSource?.TrySetCanceled();
        m_EnterClickSource = null;
        m_LoadPanel = null;
        m_LoginPanel = null;
        m_LoginPopup = null;
        m_EnterPanel = null;

        m_BackgroundVideoRequestVersion++;
        m_BackgroundVideoCancellation?.Cancel();
        m_BackgroundVideoCancellation?.Dispose();
        m_BackgroundVideoCancellation = null;
        StopBackgroundVideo();
        m_BackgroundVideoRelativePath = null;
        m_BackgroundRawImage = null;
        m_BackgroundPreviewImage = null;
        m_LegacyBackgroundVideoPlayer = null;

        if (m_BackgroundPreviewHandle != null)
        {
            App.Resource.UnloadAsset(m_BackgroundPreviewHandle).Forget(Debug.LogException);
            m_BackgroundPreviewHandle = null;
        }

        ReleaseDesign();
        base.Release();
    }

    public void UpdateProcessing(string message, float progress)
    {
        text_info?.SetText(message);
        slider_slider?.SetValueWithoutNotify(progress);
    }

    public void ShowLoadPanel()
    {
        SetActive(m_LoadPanel, true);
        SetActive(m_LoginPanel, false);
        SetActive(m_EnterPanel, false);
        SetActive(m_LoginPopup, false);
    }

    public void ShowLoginPanel()
    {
        SetActive(m_LoadPanel, false);
        SetActive(m_LoginPanel, true);
        SetActive(m_EnterPanel, false);
        SetActive(m_LoginPopup, false);
    }

    public void ShowLoginPopup()
    {
        SetActive(m_LoginPanel, true);
        SetActive(m_LoginPopup, true);
    }

    public void CloseLoginPopup()
    {
        SetActive(m_LoginPopup, false);
    }

    public void ShowEnterPanel()
    {
        SetActive(m_LoadPanel, false);
        SetActive(m_LoginPanel, false);
        SetActive(m_LoginPopup, false);
        SetActive(m_EnterPanel, true);
    }

    public UniTask WaitForEnterButtonClickAsync()
    {
        m_EnterClickSource?.TrySetCanceled();
        m_EnterClickSource = new UniTaskCompletionSource();
        return m_EnterClickSource.Task;
    }

    /// <summary>
    /// 切换登录界面背景视频。传入相对 StreamingAssets 的路径，例如
    /// <c>videos/media-xxxxxx/master.m3u8</c>。
    /// </summary>
    public async UniTask SetBackgroundVideoAsync(
        string streamingAssetsRelativePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(streamingAssetsRelativePath))
        {
            throw new ArgumentException(
                "Background video path cannot be empty.",
                nameof(streamingAssetsRelativePath));
        }

        var relativePath = ResolveFixedBackgroundRelativePath(streamingAssetsRelativePath);
        CacheBackgroundView();
        if (m_BackgroundRawImage == null)
        {
            Debug.LogError("[LoadingWindow] Background RawImage (bg) was not found.");
            return;
        }

        var requestVersion = ++m_BackgroundVideoRequestVersion;
        m_BackgroundVideoCancellation?.Cancel();
        m_BackgroundVideoCancellation?.Dispose();
        m_BackgroundVideoCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var linkedToken = m_BackgroundVideoCancellation.Token;

        StopBackgroundVideo();
        DisableLegacyBackgroundVideoPlayer();

        // 视频就绪前先用预览图占位（Resources 兜底立即显示 + bundle 高清异步替换）；
        // 预览图缺失时保持透明，不影响视频流程。
        await ShowBackgroundPreviewAsync(linkedToken);

        VideoPlayableHandle playback = null;
        try
        {
            playback = await App.Playable.Video.PlayAsync(
                CreateBackgroundVideoRequest(relativePath),
                linkedToken);

            linkedToken.ThrowIfCancellationRequested();
            if (requestVersion != m_BackgroundVideoRequestVersion)
            {
                playback.Stop();
                return;
            }

            m_BackgroundVideo = playback;
            m_BackgroundVideoRelativePath = relativePath;
            m_BackgroundVideo.TextureChanged += HandleBackgroundTextureChanged;
            await WaitForFirstFrameAsync(m_BackgroundVideo, linkedToken);
            if (requestVersion != m_BackgroundVideoRequestVersion)
            {
                return;
            }

            // 视频首帧就绪：隐藏独立预览图，切换为视频纹理。
            HideBackgroundPreview();
            RefreshBackgroundSurface(force: true);
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(m_BackgroundVideo, playback) is false)
            {
                playback?.Stop();
            }

            throw;
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(m_BackgroundVideo, playback) is false)
            {
                playback?.Stop();
            }

            Debug.LogException(exception);
            throw;
        }
    }

    private static VideoPlayableRequest CreateBackgroundVideoRequest(
        string streamingAssetsRelativePath,
        Transform parent = null)
    {
        var relativePath = ResolveFixedBackgroundRelativePath(streamingAssetsRelativePath);
        return new VideoPlayableRequest(
            ResolveMediaUrl(relativePath),
            new VideoPlayableOptions
            {
                Loop = true,
                Seekable = false,
                DontDestroyOnLoad = false,
                Parent = parent,
                SupportsAutoQuality = false,
                InitialQuality = new VideoQualitySelection(
                    VideoQualityMode.FixedHeight,
                    FixedBackgroundVideoHeight)
            });
    }

    /// <summary>
    /// 将 master.m3u8 解析为固定清晰度变体，避免 HLS 自适应切档。
    /// </summary>
    private static string ResolveFixedBackgroundRelativePath(string streamingAssetsRelativePath)
    {
        var relativePath = NormalizeStreamingAssetsRelativePath(streamingAssetsRelativePath);
        if (relativePath.EndsWith("/master.m3u8", StringComparison.OrdinalIgnoreCase) is false &&
            string.Equals(relativePath, "master.m3u8", StringComparison.OrdinalIgnoreCase) is false)
        {
            return relativePath;
        }

        var directory = relativePath.EndsWith("/master.m3u8", StringComparison.OrdinalIgnoreCase)
            ? relativePath.Substring(0, relativePath.Length - "/master.m3u8".Length)
            : string.Empty;
        var folder = ToBackgroundRenditionFolder(FixedBackgroundVideoHeight);
        return string.IsNullOrEmpty(directory)
            ? $"{folder}/index.m3u8"
            : $"{directory}/{folder}/index.m3u8";
    }

    private static string ToBackgroundRenditionFolder(int height)
    {
        return height switch
        {
            2160 => "4K",
            1440 => "2K",
            1080 => "1080P",
            720 => "720P",
            480 => "480P",
            240 => "240P",
            _ => $"{height}P"
        };
    }

    private static async UniTask WaitForFirstFrameAsync(
        VideoPlayableHandle playback,
        CancellationToken cancellationToken)
    {
        if (playback == null || playback.HasFirstFrame)
        {
            return;
        }

        var ready = new UniTaskCompletionSource();
        void OnFirstFrame(VideoPlayableHandle _)
        {
            ready.TrySetResult();
        }

        playback.FirstFrameReady += OnFirstFrame;
        try
        {
            if (playback.HasFirstFrame)
            {
                return;
            }

            await ready.Task.AttachExternalCancellation(cancellationToken);
        }
        finally
        {
            playback.FirstFrameReady -= OnFirstFrame;
        }
    }

    private void CacheBackgroundView()
    {
        if (m_BackgroundRawImage != null)
        {
            return;
        }

        var background = Document != null
            ? Document.transform.Find("bg")
            : null;
        if (background == null)
        {
            return;
        }

        m_BackgroundRawImage = background.GetComponent<RawImage>();
        m_LegacyBackgroundVideoPlayer = background.GetComponent<VideoPlayer>();
        DisableLegacyBackgroundVideoPlayer();
        EnsureBackgroundPreviewImage(background);
    }

    /// <summary>
    /// 预览图使用独立物体（bg 的子级、渲染在最上层）：VideoPlayable 缓冲期间会通过
    /// VideoSurfaceBinder 把 bg RawImage 绑定为 null（白色），预览图放同一 RawImage 上会被覆盖。
    /// </summary>
    private void EnsureBackgroundPreviewImage(Transform background)
    {
        var preview = background.Find("preview");
        if (preview == null)
        {
            var go = new GameObject("preview", typeof(RawImage));
            go.transform.SetParent(background, false);
            go.transform.SetAsLastSibling();
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            m_BackgroundPreviewImage = go.GetComponent<RawImage>();
        }
        else
        {
            m_BackgroundPreviewImage = preview.GetComponent<RawImage>();
        }

        if (m_BackgroundPreviewImage != null)
        {
            m_BackgroundPreviewImage.enabled = false;
        }
    }

    private void HideBackgroundPreview()
    {
        if (m_BackgroundPreviewImage == null)
        {
            return;
        }

        m_BackgroundPreviewImage.enabled = false;
    }

    private void DisableLegacyBackgroundVideoPlayer()
    {
        if (m_LegacyBackgroundVideoPlayer == null)
        {
            return;
        }

        //m_LegacyBackgroundVideoPlayer.Stop();
        //m_LegacyBackgroundVideoPlayer.enabled = false;
    }

    private void HandleBackgroundTextureChanged(VideoPlayableHandle playback)
    {
        if (ReferenceEquals(playback, m_BackgroundVideo) is false)
        {
            return;
        }

        RefreshBackgroundSurface(force: true);
    }

    private async UniTask ShowBackgroundPreviewAsync(CancellationToken cancellationToken)
    {
        // 1) Resources 兜底图立即显示（bundle 未包含/未就绪时也不白屏）。
        try
        {
            var fallback = Resources.Load<Texture2D>(DefaultBackgroundPreviewPath);
            if (fallback != null && m_BackgroundPreviewImage != null)
            {
                m_BackgroundPreviewImage.texture = fallback;
                m_BackgroundPreviewImage.enabled = true;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[LoadingWindow] Background preview fallback failed: {exception.Message}");
        }

        // 2) bundle 高清预览异步替换兜底图。
        try
        {
            var handle = await App.Resource.LoadAssetAsync(BackgroundPreviewBundlePath);
            if (handle == null || handle.Status != ResourceStatus.Succeeded || m_BackgroundPreviewImage == null)
            {
                return;
            }

            var texture = handle.GetAsset<Texture2D>();
            if (texture == null || m_BackgroundPreviewImage == null)
            {
                return;
            }

            m_BackgroundPreviewHandle = handle;
            m_BackgroundPreviewImage.texture = texture;
            m_BackgroundPreviewImage.enabled = true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[LoadingWindow] Background preview (bundle) load failed: {exception.Message}");
        }
    }

    private void RefreshBackgroundSurface(bool force = false)
    {
        if (m_BackgroundRawImage == null)
        {
            return;
        }

        if (m_BackgroundVideo == null)
        {
            // 视频未就绪：保留预览图占位，避免被空纹理覆盖。
            return;
        }

        var texture = m_BackgroundVideo?.Texture;
        var verticalFlip = m_BackgroundVideo?.RequiresVerticalFlip ?? false;
        if (force is false &&
            ReferenceEquals(texture, m_BoundBackgroundTexture) &&
            verticalFlip == m_BoundBackgroundVerticalFlip)
        {
            return;
        }

        m_BoundBackgroundTexture = texture;
        m_BoundBackgroundVerticalFlip = verticalFlip;
        VideoSurfaceBinder.Bind(m_BackgroundRawImage, texture, verticalFlip, VideoDisplayMode.FitVertically);
    }

    private void StopBackgroundVideo()
    {
        if (m_BackgroundVideo != null)
        {
            m_BackgroundVideo.TextureChanged -= HandleBackgroundTextureChanged;
            m_BackgroundVideo.Stop();
            m_BackgroundVideo = null;
        }

        m_BoundBackgroundTexture = null;
        m_BoundBackgroundVerticalFlip = false;
    }

    private static string NormalizeStreamingAssetsRelativePath(string path)
    {
        var normalized = path.Trim().Replace('\\', '/').TrimStart('/');
        const string streamingAssetsPrefix = "Assets/StreamingAssets/";
        const string streamingAssetsFolder = "StreamingAssets/";
        if (normalized.StartsWith(streamingAssetsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(streamingAssetsPrefix.Length);
        }
        else if (normalized.StartsWith(streamingAssetsFolder, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(streamingAssetsFolder.Length);
        }

        if (normalized.Length == 0)
        {
            throw new ArgumentException("Background video path cannot be empty.", nameof(path));
        }

        return normalized;
    }

    /// <summary>
    /// 登录背景视频统一从 HLS 媒体库（CDN/OSS）拉取，不随包分发。
    /// 优先使用 MediaDeliverySettings 配置的端点；未生成配置时兜底走媒体库 CDN。
    /// </summary>
    private static string ResolveMediaUrl(string relativePath)
    {
        var settings = App.Config?.MediaDelivery;
        if (settings != null)
        {
            return MediaUrlResolver.Resolve(new MediaPath(relativePath), settings);
        }

        return "https://moviegame.wwhy.games/" + relativePath.Replace('\\', '/');
    }

    private void CachePanels()
    {
        var root = Document != null ? Document.transform : null;
        if (root == null)
        {
            return;
        }

        m_LoadPanel = FindPanel(root, "load_panel", slider_slider != null ? slider_slider.transform : null);
        m_LoginPanel = FindPanel(root, "login_panel", btn_enter_login != null ? btn_enter_login.transform : null);
        m_LoginPopup = FindPanel(root, "login_popup", btn_login != null ? btn_login.transform : null);
        m_EnterPanel = FindPanel(root, "enter_panel", btn_enter_game != null ? btn_enter_game.transform : null);
    }

    private static GameObject FindPanel(Transform root, string panelName, Transform fallbackChild)
    {
        var panelTransform = root.Find("ContentContainer/" + panelName) ?? root.Find(panelName);
        if (panelTransform != null)
        {
            return panelTransform.gameObject;
        }

        if (fallbackChild == null)
        {
            return null;
        }

        var current = fallbackChild;
        while (current != null && current != root)
        {
            if (current.name == panelName)
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        return fallbackChild.parent != null ? fallbackChild.parent.gameObject : null;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private void BindAgreementLinks()
    {
        m_AgreementText = FindAgreementText();
        if (m_AgreementText == null)
        {
            return;
        }

        m_AgreementText.raycastTarget = true;
        m_AgreementText.richText = true;
        m_AgreementText.ForceMeshUpdate();

        m_AgreementLinkHandler = m_AgreementText.GetComponent<TmpHyperlinkClickHandler>();
        if (m_AgreementLinkHandler == null)
        {
            m_AgreementLinkHandler = m_AgreementText.gameObject.AddComponent<TmpHyperlinkClickHandler>();
        }

        m_AgreementLinkHandler.Bind(m_AgreementText);
    }

    private void UnbindAgreementLinks()
    {
        if (m_AgreementLinkHandler != null)
        {
            m_AgreementLinkHandler.Unbind();
            UnityEngine.Object.Destroy(m_AgreementLinkHandler);
            m_AgreementLinkHandler = null;
        }

        m_AgreementText = null;
    }

    private TextMeshProUGUI FindAgreementText()
    {
        if (toggle_agreement == null)
        {
            return null;
        }

        var parent = toggle_agreement.transform.parent;
        if (parent == null)
        {
            return null;
        }

        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child == toggle_agreement.transform)
            {
                continue;
            }

            var text = child.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                return text;
            }
        }

        return parent.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void BindButtons()
    {
        if (btn_enter_login != null)
        {
            btn_enter_login.onClick.AddListener(OnEnterLoginClicked);
        }

        if (btn_login != null)
        {
            btn_login.onClick.AddListener(OnLoginClicked);
        }

        if (btn_close_login != null)
        {
            btn_close_login.onClick.AddListener(OnCloseLoginClicked);
        }

        if (btn_enter_game != null)
        {
            btn_enter_game.onClick.AddListener(OnEnterGameClicked);
        }
    }

    private void UnbindButtons()
    {
        if (btn_enter_login != null)
        {
            btn_enter_login.onClick.RemoveListener(OnEnterLoginClicked);
        }

        if (btn_login != null)
        {
            btn_login.onClick.RemoveListener(OnLoginClicked);
        }

        if (btn_close_login != null)
        {
            btn_close_login.onClick.RemoveListener(OnCloseLoginClicked);
        }

        if (btn_enter_game != null)
        {
            btn_enter_game.onClick.RemoveListener(OnEnterGameClicked);
        }
    }

    private void OnEnterLoginClicked()
    {
        if (!IsAgreementAccepted)
        {
            OnAgreementRequired?.Invoke();
            return;
        }

        ShowLoginPopup();
    }

    private void OnLoginClicked()
    {
        // 暂不做账号密码校验与本地保存，任意输入均可进入 enter_panel。
        ShowEnterPanel();
    }

    private void OnCloseLoginClicked()
    {
        CloseLoginPopup();
    }

    private void OnEnterGameClicked()
    {
        m_EnterClickSource?.TrySetResult();
    }

    /// <summary>
    /// TMP &lt;link&gt; 点击：按 link ID（URL）打开外部浏览器。
    /// </summary>
    private sealed class TmpHyperlinkClickHandler : MonoBehaviour, IPointerClickHandler
    {
        private TextMeshProUGUI m_Text;

        public void Bind(TextMeshProUGUI text)
        {
            m_Text = text;
        }

        public void Unbind()
        {
            m_Text = null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (m_Text == null)
            {
                return;
            }

            var camera = eventData.pressEventCamera;
            var linkIndex = TMP_TextUtilities.FindIntersectingLink(m_Text, eventData.position, camera);
            if (linkIndex < 0)
            {
                return;
            }

            var linkId = m_Text.textInfo.linkInfo[linkIndex].GetLinkID();
            if (string.IsNullOrWhiteSpace(linkId))
            {
                return;
            }

            Application.OpenURL(linkId);
        }
    }
}
