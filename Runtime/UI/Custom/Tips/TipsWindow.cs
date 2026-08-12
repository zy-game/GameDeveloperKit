using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using GameDeveloperKit;
using GameDeveloperKit.UI;
using UnityEngine;

/// <summary>
/// 模态提示弹窗（GDK 公用 UI）。
/// 支持三种模式：仅确认、确认+取消、自动确认（超时后自动视为确认）。
/// 结果通过 <see cref="WaitForCloseAsync"/> 返回：true=确认，false=取消。
/// 代码在 GDK，prefab 由客户端提供（见 TipsWindow.Design.g.cs 的 UIOption 路径与绑定约定）。
/// 业务侧请通过 <see cref="GameDeveloperKit.UI.UIModule.TipsAsync"/> 边界函数调用，不要直接 OpenAsync 本窗口。
/// </summary>
public sealed partial class TipsWindow : UIWindow
{
    private const float PanelFromScale = 0.85f;
    private const float PanelDurationSeconds = 0.22f;

    private readonly Options m_Options = new Options();
    private UniTaskCompletionSource<bool> m_CompletionSource;
    private bool m_IsClosing;

    /// <summary>
    /// 配置提示内容与按钮（窗口需已打开，内部由 UIModule.TipsAsync 调用）。
    /// </summary>
    public void Configure(
        string content,
        string confirmText,
        string cancelText,
        float autoConfirmTimeSeconds)
    {
        Configure(null, content, confirmText, cancelText, autoConfirmTimeSeconds);
    }

    /// <summary>
    /// 配置提示内容与按钮（窗口需已打开）。title 为空时隐藏标题节点。
    /// </summary>
    public void Configure(
        string title,
        string content,
        string confirmText,
        string cancelText,
        float autoConfirmTimeSeconds)
    {
        m_Options.Title = title;
        m_Options.Content = content;
        m_Options.ConfirmText = confirmText;
        m_Options.CancelText = cancelText;
        m_Options.AutoConfirmTimeSeconds = Mathf.Max(0f, autoConfirmTimeSeconds);

        var hasTitle = !string.IsNullOrEmpty(title);
        if (text_title != null)
        {
            text_title.gameObject.SetActive(hasTitle);
            if (hasTitle)
            {
                text_title.text = title;
            }
        }

        if (text_content != null)
        {
            text_content.text = content ?? string.Empty;
        }

        if (text_confirm != null)
        {
            text_confirm.text = confirmText ?? "确定";
        }

        var hasCancel = !string.IsNullOrEmpty(cancelText);
        if (text_cancel != null)
        {
            text_cancel.text = cancelText ?? "取消";
        }

        if (btn_cancel != null)
        {
            btn_cancel.gameObject.SetActive(hasCancel);
        }

        if (text_countdown != null)
        {
            text_countdown.gameObject.SetActive(m_Options.AutoConfirmTimeSeconds > 0f);
        }
    }

    public override async UniTask OnAwakeAsync()
    {
        await InitializeDesignAsync();
        BindButtons();
    }

    public override UniTask OnOpenAsync()
    {
        m_CompletionSource = null;
        m_IsClosing = false;
        PlayOpenAnimation();
        return UniTask.CompletedTask;
    }

    public override void Release()
    {
        UnbindButtons();
        m_CompletionSource = null;
        m_IsClosing = false;
        KillPanelAnimation();
        ReleaseDesign();
        base.Release();
    }

    /// <summary>
    /// 等待窗口关闭并返回结果：true=确认，false=取消（自动确认视为 true）。
    /// </summary>
    public UniTask<bool> WaitForCloseAsync()
    {
        if (m_CompletionSource != null)
        {
            m_CompletionSource.TrySetResult(false);
        }

        m_CompletionSource = new UniTaskCompletionSource<bool>();
        return m_CompletionSource.Task;
    }

    /// <summary>
    /// 启动自动确认倒计时（Configure 后调用，autoConfirmTimeSeconds &gt; 0 时启用）。
    /// </summary>
    public void StartAutoConfirmCountdown()
    {
        if (m_Options.AutoConfirmTimeSeconds <= 0f)
        {
            return;
        }

        RunAutoConfirmCountdownAsync(m_Options.AutoConfirmTimeSeconds).Forget(Debug.LogException);
    }

    private async UniTask RunAutoConfirmCountdownAsync(float totalSeconds)
    {
        var remaining = totalSeconds;
        while (remaining > 0f)
        {
            if (m_IsClosing)
            {
                return;
            }

            if (text_countdown != null)
            {
                text_countdown.text = Mathf.CeilToInt(remaining).ToString();
            }

            await UniTask.Delay(
                TimeSpan.FromSeconds(Mathf.Min(1f, remaining)),
                DelayType.UnscaledDeltaTime);
            remaining -= 1f;
        }

        if (!m_IsClosing)
        {
            CompleteAndCloseAsync(true).Forget(Debug.LogException);
        }
    }

    private void BindButtons()
    {
        if (btn_confirm != null)
        {
            btn_confirm.onClick.AddListener(OnConfirmClicked);
        }

        if (btn_cancel != null)
        {
            btn_cancel.onClick.AddListener(OnCancelClicked);
        }
    }

    private void UnbindButtons()
    {
        if (btn_confirm != null)
        {
            btn_confirm.onClick.RemoveListener(OnConfirmClicked);
        }

        if (btn_cancel != null)
        {
            btn_cancel.onClick.RemoveListener(OnCancelClicked);
        }
    }

    private void OnConfirmClicked()
    {
        CompleteAndCloseAsync(true).Forget(Debug.LogException);
    }

    private void OnCancelClicked()
    {
        CompleteAndCloseAsync(false).Forget(Debug.LogException);
    }

    private async UniTask CompleteAndCloseAsync(bool result)
    {
        if (m_IsClosing)
        {
            return;
        }

        m_IsClosing = true;
        await PlayCloseAnimationAsync();
        m_CompletionSource?.TrySetResult(result);
        m_CompletionSource = null;
        await App.UI.CloseAsync<TipsWindow>();
    }

    private void PlayOpenAnimation()
    {
        var panel = ResolvePanel();
        if (panel == null)
        {
            return;
        }

        panel.DOKill();
        panel.localScale = Vector3.one * PanelFromScale;
        panel.DOScale(Vector3.one, PanelDurationSeconds)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetLink(GameObject);
    }

    private async UniTask PlayCloseAnimationAsync()
    {
        var panel = ResolvePanel();
        if (panel == null)
        {
            return;
        }

        var completion = new UniTaskCompletionSource();
        panel.DOKill();
        panel.DOScale(Vector3.one * PanelFromScale, PanelDurationSeconds)
            .SetEase(Ease.InCubic)
            .SetUpdate(true)
            .SetLink(GameObject)
            .OnComplete(() => completion.TrySetResult())
            .OnKill(() => completion.TrySetResult());
        await completion.Task;
    }

    private void KillPanelAnimation()
    {
        var panel = ResolvePanel();
        if (panel != null)
        {
            panel.DOKill();
            panel.localScale = Vector3.one;
        }
    }

    private Transform ResolvePanel()
    {
        if (GameObject == null)
        {
            return null;
        }

        var direct = GameObject.transform.Find("Panel");
        if (direct != null)
        {
            return direct;
        }

        return Document != null ? Document.transform.Find("Panel") : null;
    }

    private sealed class Options
    {
        public string Title;
        public string Content;
        public string ConfirmText;
        public string CancelText;
        public float AutoConfirmTimeSeconds;
    }
}
