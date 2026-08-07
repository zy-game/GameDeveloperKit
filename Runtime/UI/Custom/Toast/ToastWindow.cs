using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using GameDeveloperKit;
using GameDeveloperKit.UI;
using UnityEngine;

/// <summary>
/// 轻量飘字提示窗口（GDK 公用 UI）。
/// 无按钮、无交互，显示指定时长后自动消失，带淡入淡出动画。
/// 代码在 GDK，prefab 由客户端提供（见 ToastWindow.Design.g.cs 的 UIOption 路径与绑定约定）。
/// 业务侧请通过 <see cref="GameDeveloperKit.UI.UIModule.Toast"/> 边界函数调用，不要直接 OpenAsync 本窗口。
/// </summary>
public sealed partial class ToastWindow : UIWindow
{
    /// <summary>
    /// 默认展示时长（秒）。
    /// </summary>
    public const float DefaultDurationSeconds = 2f;

    private const float FadeInSeconds = 0.15f;
    private const float FadeOutSeconds = 0.25f;
    private const float FadeFromAlpha = 0f;
    private const float FadeToAlpha = 1f;

    private CanvasGroup m_CanvasGroup;
    private Sequence m_Animation;
    private bool m_IsClosing;

    public override async UniTask OnAwakeAsync()
    {
        await InitializeDesignAsync();
        m_CanvasGroup = Document != null ? Document.GetComponent<CanvasGroup>() : null;
        if (m_CanvasGroup != null)
        {
            m_CanvasGroup.alpha = FadeFromAlpha;
            m_CanvasGroup.blocksRaycasts = false;
        }
    }

    public override UniTask OnOpenAsync()
    {
        m_IsClosing = false;
        return UniTask.CompletedTask;
    }

    public override void Release()
    {
        KillAnimation();
        m_CanvasGroup = null;
        ReleaseDesign();
        base.Release();
    }

    /// <summary>
    /// 设置飘字文本（窗口需已打开）。
    /// </summary>
    public void Show(string text)
    {
        if (text_content != null)
        {
            text_content.text = text ?? string.Empty;
        }
    }

    /// <summary>
    /// 完整播放流程：淡入 → 停留 duration 秒 → 淡出，结束后自动关闭窗口。
    /// </summary>
    /// <param name="durationSeconds">展示时长（秒），0 表示仅淡入淡出。</param>
    public async UniTask PlayAndDismissAsync(float durationSeconds)
    {
        if (m_IsClosing)
        {
            return;
        }

        var duration = Mathf.Max(0f, durationSeconds);
        var completion = new UniTaskCompletionSource();
        m_Animation = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(GameObject)
            .Append(FadeTween(FadeToAlpha, FadeInSeconds))
            .AppendInterval(duration)
            .Append(FadeTween(FadeFromAlpha, FadeOutSeconds))
            .OnComplete(() => completion.TrySetResult())
            .OnKill(() => completion.TrySetResult());
        await completion.Task;
        await CloseSelfAsync();
    }

    /// <summary>
    /// 立即淡出并关闭（如被新飘字覆盖时调用）。
    /// </summary>
    public async UniTask DismissNowAsync()
    {
        if (m_IsClosing)
        {
            return;
        }

        var completion = new UniTaskCompletionSource();
        m_Animation = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(GameObject)
            .Append(FadeTween(FadeFromAlpha, FadeOutSeconds))
            .OnComplete(() => completion.TrySetResult())
            .OnKill(() => completion.TrySetResult());
        await completion.Task;
        await CloseSelfAsync();
    }

    private Tween FadeTween(float targetAlpha, float durationSeconds)
    {
        if (m_CanvasGroup == null)
        {
            return DOTween.Sequence().SetUpdate(true).SetLink(GameObject);
        }

        return m_CanvasGroup
            .DOFade(targetAlpha, durationSeconds)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .SetLink(GameObject);
    }

    private async UniTask CloseSelfAsync()
    {
        if (m_IsClosing)
        {
            return;
        }

        m_IsClosing = true;
        await App.UI.CloseAsync<ToastWindow>();
    }

    private void KillAnimation()
    {
        if (m_Animation != null)
        {
            m_Animation.Kill();
            m_Animation = null;
        }

        if (m_CanvasGroup != null)
        {
            m_CanvasGroup.DOKill();
            m_CanvasGroup.alpha = FadeToAlpha;
        }
    }
}
