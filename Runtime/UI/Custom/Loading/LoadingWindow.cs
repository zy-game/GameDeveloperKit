using Cysharp.Threading.Tasks;
using GameDeveloperKit.UI;
using UnityEngine;

public sealed partial class LoadingWindow : UIWindow, IProcessingWindow
{
    private GameObject m_LoadPanel;
    private GameObject m_EnterPanel;
    private UniTaskCompletionSource m_EnterClickSource;

    public bool IsAgreementAccepted => toggle_agreement != null && toggle_agreement.isOn;

    public override async UniTask OnAwakeAsync()
    {
        await InitializeDesignAsync();
        CachePanels();
        BindButtons();
        ShowLoadPanel();
    }

    public override UniTask OnOpenAsync()
    {
        ShowLoadPanel();
        return UniTask.CompletedTask;
    }

    public override void Release()
    {
        UnbindButtons();
        m_EnterClickSource?.TrySetCanceled();
        m_EnterClickSource = null;
        m_LoadPanel = null;
        m_EnterPanel = null;
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
        if (m_LoadPanel != null)
        {
            m_LoadPanel.SetActive(true);
        }

        if (m_EnterPanel != null)
        {
            m_EnterPanel.SetActive(false);
        }
    }

    public void ShowEnterPanel()
    {
        if (m_LoadPanel != null)
        {
            m_LoadPanel.SetActive(false);
        }

        if (m_EnterPanel != null)
        {
            m_EnterPanel.SetActive(true);
        }
    }

    public UniTask WaitForEnterButtonClickAsync()
    {
        m_EnterClickSource?.TrySetCanceled();
        m_EnterClickSource = new UniTaskCompletionSource();
        return m_EnterClickSource.Task;
    }

    private void CachePanels()
    {
        var root = Document != null ? Document.transform : null;
        if (root == null)
        {
            return;
        }

        var loadTransform = root.Find("load_panel");
        if (loadTransform != null)
        {
            m_LoadPanel = loadTransform.gameObject;
        }
        else if (slider_slider != null)
        {
            m_LoadPanel = slider_slider.transform.parent.gameObject;
        }

        var enterTransform = root.Find("enter_panel");
        if (enterTransform != null)
        {
            m_EnterPanel = enterTransform.gameObject;
        }
        else if (btn_enter_game != null)
        {
            m_EnterPanel = btn_enter_game.transform.parent.gameObject;
        }
    }

    private void BindButtons()
    {
        if (btn_enter_game != null)
        {
            btn_enter_game.onClick.AddListener(OnEnterGameClicked);
        }
    }

    private void UnbindButtons()
    {
        if (btn_enter_game != null)
        {
            btn_enter_game.onClick.RemoveListener(OnEnterGameClicked);
        }
    }

    private void OnEnterGameClicked()
    {
        m_EnterClickSource?.TrySetResult();
    }
}
