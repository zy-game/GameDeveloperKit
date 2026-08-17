using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using GameDeveloperKit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 轻量飘字提示宿主（GDK 公用 UI）。
/// 每条提示由模板生成独立条目，支持同时显示、向上堆叠和自动淡出。
/// 业务侧请通过 <see cref="GameDeveloperKit.UI.UIModule.Toast"/> 调用。
/// </summary>
public sealed partial class ToastWindow : UIWindow
{
    /// <summary>
    /// 默认展示时长（秒）。
    /// </summary>
    public const float DefaultDurationSeconds = 2f;

    private const float EnterSeconds = 0.2f;
    private const float ReflowSeconds = 0.2f;
    private const float FadeOutSeconds = 0.25f;
    private const float BaseRise = 80f;
    private const float ExitRise = 48f;
    private const float EntrySpacing = 12f;
    private const float HorizontalPadding = 64f;
    private const float VerticalPadding = 32f;
    private const float ScreenEdgePadding = 80f;

    private readonly List<ToastEntry> m_Entries = new();
    private int m_NextEntryId;
    private bool m_IsReleasing;

    internal int ActiveToastCount => m_Entries.Count;

    public override async UniTask OnAwakeAsync()
    {
        await InitializeDesignAsync();
        m_IsReleasing = false;
        if (rect_template != null)
        {
            rect_template.gameObject.SetActive(false);
        }
    }

    public override void Release()
    {
        m_IsReleasing = true;
        for (var i = m_Entries.Count - 1; i >= 0; i--)
        {
            DestroyEntry(m_Entries[i], false);
        }

        m_Entries.Clear();
        ReleaseDesign();
        base.Release();
    }

    /// <summary>
    /// 添加一条独立提示。
    /// </summary>
    public void AddToast(string text, float durationSeconds)
    {
        if (m_IsReleasing || rect_template == null)
        {
            return;
        }

        var root = Object.Instantiate(rect_template, rect_template.parent, false);
        root.gameObject.name = $"ToastEntry_{++m_NextEntryId}";
        root.gameObject.SetActive(true);

        var background = root.GetComponentInChildren<Image>(true);
        var content = root.GetComponentInChildren<TMP_Text>(true);
        if (background == null || content == null)
        {
            Debug.LogError("[ToastWindow] b_temp requires an Image and TMP_Text child.");
            Object.Destroy(root.gameObject);
            return;
        }

        var canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = root.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        root.anchoredPosition = Vector2.zero;
        root.localScale = Vector3.zero;

        ConfigureContent(root, background, content, text);

        var entry = new ToastEntry(root, canvasGroup);
        m_Entries.Add(entry);
        ReflowEntries();

        entry.ScaleTween = root
            .DOScale(Vector3.one, EnterSeconds)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetLink(root.gameObject);

        var holdSeconds = Mathf.Max(0f, durationSeconds);
        entry.LifetimeTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(root.gameObject)
            .AppendInterval(EnterSeconds + holdSeconds)
            .OnComplete(() => BeginDismiss(entry));
    }

    private static void ConfigureContent(
        RectTransform root,
        Image background,
        TMP_Text content,
        string text)
    {
        content.text = text ?? string.Empty;
        content.ForceMeshUpdate();

        var canvasRect = content.canvas != null
            ? content.canvas.transform as RectTransform
            : null;
        var maximumWidth = canvasRect != null && canvasRect.rect.width > ScreenEdgePadding
            ? canvasRect.rect.width - ScreenEdgePadding
            : float.PositiveInfinity;
        var backgroundWidth = Mathf.Min(content.preferredWidth + HorizontalPadding, maximumWidth);
        var contentWidth = Mathf.Max(0f, backgroundWidth - HorizontalPadding);

        var contentRect = content.rectTransform;
        var fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
        content.enableWordWrapping = content.preferredWidth > contentWidth;
        content.ForceMeshUpdate();

        var backgroundRect = background.rectTransform;
        var backgroundHeight = Mathf.Max(
            backgroundRect.rect.height,
            content.preferredHeight + VerticalPadding);
        contentRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            Mathf.Max(contentRect.rect.height, content.preferredHeight));
        backgroundRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, backgroundWidth);
        backgroundRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, backgroundHeight);
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, backgroundWidth);
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, backgroundHeight);
    }

    private void ReflowEntries()
    {
        var nextY = BaseRise;
        for (var i = m_Entries.Count - 1; i >= 0; i--)
        {
            var entry = m_Entries[i];
            if (entry.Root == null || entry.IsExiting)
            {
                continue;
            }

            var halfHeight = entry.Root.rect.height * 0.5f;
            var targetY = nextY + halfHeight;
            entry.MoveTween?.Kill();
            entry.MoveTween = entry.Root
                .DOAnchorPosY(targetY, ReflowSeconds)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetLink(entry.Root.gameObject);
            nextY = targetY + halfHeight + EntrySpacing;
        }
    }

    private void BeginDismiss(ToastEntry entry)
    {
        if (m_IsReleasing || entry == null || entry.IsExiting || entry.Root == null)
        {
            return;
        }

        entry.IsExiting = true;
        ReflowEntries();

        entry.MoveTween?.Kill();
        entry.ScaleTween?.Kill();
        entry.ExitTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(entry.Root.gameObject)
            .Join(entry.Root
                .DOAnchorPosY(entry.Root.anchoredPosition.y + ExitRise, FadeOutSeconds)
                .SetEase(Ease.InCubic))
            .Join(entry.CanvasGroup
                .DOFade(0f, FadeOutSeconds)
                .SetEase(Ease.Linear))
            .OnComplete(() => DestroyEntry(entry, true));
    }

    private void DestroyEntry(ToastEntry entry, bool removeFromList)
    {
        if (entry == null)
        {
            return;
        }

        entry.LifetimeTween?.Kill();
        entry.MoveTween?.Kill();
        entry.ScaleTween?.Kill();
        entry.ExitTween?.Kill();
        entry.LifetimeTween = null;
        entry.MoveTween = null;
        entry.ScaleTween = null;
        entry.ExitTween = null;

        if (removeFromList)
        {
            m_Entries.Remove(entry);
        }

        if (entry.Root != null)
        {
            Object.Destroy(entry.Root.gameObject);
        }
    }

    private sealed class ToastEntry
    {
        public ToastEntry(RectTransform root, CanvasGroup canvasGroup)
        {
            Root = root;
            CanvasGroup = canvasGroup;
        }

        public RectTransform Root { get; }
        public CanvasGroup CanvasGroup { get; }
        public Tween LifetimeTween { get; set; }
        public Tween MoveTween { get; set; }
        public Tween ScaleTween { get; set; }
        public Tween ExitTween { get; set; }
        public bool IsExiting { get; set; }
    }
}
