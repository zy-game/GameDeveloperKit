using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit;
using GameDeveloperKit.Media;
using GameDeveloperKit.Playable;
using GameDeveloperKit.UI;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class LoadingWindow : UIWindow, IProcessingWindow
{
    /// <summary>
    /// 加载/登录界面背景：Login_1（StreamingAssets 相对路径，master 清单）。
    /// </summary>
    public const string DefaultBackgroundVideoRelativePath =
        "videos/media-928ee30ea99a424d/master.m3u8";

    /// <summary>
    /// 背景固定清晰度（禁用 HLS 自适应切换）。
    /// </summary>
    public const int FixedBackgroundVideoHeight = 2160;

    private GameObject m_LoadPanel;
    private RawImage m_BackgroundRawImage;
    private VideoPlayableHandle m_BackgroundVideo;
    private CancellationTokenSource m_BackgroundVideoCancellation;
    private string m_BackgroundVideoRelativePath;
    private int m_BackgroundVideoRequestVersion;

    public override async UniTask OnAwakeAsync()
    {
        await InitializeDesignAsync();
        CachePanels();
        CacheBackgroundView();
        ShowLoadPanel();
        // 不阻塞窗口初始化：视频缺失/失败时仅记日志，保留预览图静态背景。
        SetBackgroundVideoAsync(DefaultBackgroundVideoRelativePath)
            .Forget(exception => Debug.LogWarning(
                $"[LoadingWindow] Background video load failed: {exception.Message}"));
    }

    public override UniTask OnOpenAsync()
    {
        ShowLoadPanel();
        return UniTask.CompletedTask;
    }

    public override void Release()
    {
        m_LoadPanel = null;
        m_BackgroundVideoRequestVersion++;
        m_BackgroundVideoCancellation?.Cancel();
        m_BackgroundVideoCancellation?.Dispose();
        m_BackgroundVideoCancellation = null;
        StopBackgroundVideo();
        m_BackgroundVideoRelativePath = null;
        m_BackgroundRawImage = null;
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
    }

    /// <summary>
    /// 切换加载界面背景视频。传入相对 StreamingAssets 的路径，例如
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
            Debug.LogError("[LoadingWindow] Background RawImage (b_video) was not found.");
            return;
        }

        var requestVersion = ++m_BackgroundVideoRequestVersion;
        m_BackgroundVideoCancellation?.Cancel();
        m_BackgroundVideoCancellation?.Dispose();
        m_BackgroundVideoCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var linkedToken = m_BackgroundVideoCancellation.Token;

        StopBackgroundVideo();

        VideoPlayableHandle playback = null;
        try
        {
            playback = await App.Playable.Video.PlayAsync(
                CreateBackgroundVideoRequest(relativePath, m_BackgroundRawImage),
                linkedToken);

            linkedToken.ThrowIfCancellationRequested();
            if (requestVersion != m_BackgroundVideoRequestVersion)
            {
                playback.Stop();
                return;
            }

            m_BackgroundVideo = playback;
            m_BackgroundVideoRelativePath = relativePath;
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
        RawImage surface,
        Transform parent = null)
    {
        var relativePath = ResolveFixedBackgroundRelativePath(streamingAssetsRelativePath);
        return new VideoPlayableRequest(
            ResolveMediaUrl(relativePath),
            surface,
            new VideoPlayableOptions
            {
                Loop = true,
                Seekable = false,
                DontDestroyOnLoad = false,
                Parent = parent,
                SupportsAutoQuality = false,
                InitialQuality = new VideoQualitySelection(
                    VideoQualityMode.FixedHeight,
                    FixedBackgroundVideoHeight),
                PreviewPath = "Resources/Images/Loading/loading_preview.png"
            });
    }

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

    private void CacheBackgroundView()
    {
        if (m_BackgroundRawImage != null)
        {
            return;
        }

        if (img_video != null)
        {
            m_BackgroundRawImage = img_video;
            return;
        }

        var videoNode = Document != null
            ? FindChildRecursive(Document.transform, "b_video")
            : null;
        if (videoNode == null)
        {
            return;
        }

        m_BackgroundRawImage = videoNode.GetComponent<RawImage>();
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        var direct = root.Find(name);
        if (direct != null)
        {
            return direct;
        }

        for (var i = 0; i < root.childCount; i++)
        {
            var found = FindChildRecursive(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void StopBackgroundVideo()
    {
        if (m_BackgroundVideo != null)
        {
            m_BackgroundVideo.Stop();
            m_BackgroundVideo = null;
        }
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
}
