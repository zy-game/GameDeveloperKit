using Cysharp.Threading.Tasks;
using GameDeveloperKit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed partial class LoadingWindow : UIWindow, IProcessingWindow
{
    private GameObject m_LoadPanel;
    private GameObject m_EnterPanel;
    private UniTaskCompletionSource m_EnterClickSource;
    private TextMeshProUGUI m_AgreementText;
    private TmpHyperlinkClickHandler m_AgreementLinkHandler;

    public bool IsAgreementAccepted => toggle_agreement != null && toggle_agreement.isOn;

    public override async UniTask OnAwakeAsync()
    {
        await InitializeDesignAsync();
        CachePanels();
        BindAgreementLinks();
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
        UnbindAgreementLinks();
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
            Object.Destroy(m_AgreementLinkHandler);
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
