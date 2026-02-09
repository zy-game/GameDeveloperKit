// 此代码由 UIBindData 自动生成
// 生成时间: 2025-12-04 14:50:54

using GameDeveloperKit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
[UIForm("Resources/Common/CommonDialog", EUILayer.Popup, EUIMode.Normal, false)]
public class CommonDialogView : UIViewBase
{
    #region 组件属性

    public RectTransform Rect_Info { get; private set; }
    public TextMeshProUGUI Txt_Info { get; private set; }
    public RectTransform Rect_Title { get; private set; }
    public TextMeshProUGUI Txt_Title { get; private set; }
    public RectTransform Rect_Cancel { get; private set; }
    public Image Img_Cancel { get; private set; }
    public Button Btn_Cancel { get; private set; }
    public RectTransform Rect_CancelText { get; private set; }
    public TextMeshProUGUI Txt_CancelText { get; private set; }
    public RectTransform Rect_Confirm { get; private set; }
    public Image Img_Confirm { get; private set; }
    public Button Btn_Confirm { get; private set; }
    public RectTransform Rect_ConfirmText { get; private set; }
    public TextMeshProUGUI Txt_ConfirmText { get; private set; }

    #endregion

    public override void OnStartup()
    {
        Rect_Info = Get<RectTransform>("b_info");
        Txt_Info = Get<TextMeshProUGUI>("b_info");
        Rect_Title = Get<RectTransform>("b_title");
        Txt_Title = Get<TextMeshProUGUI>("b_title");
        Rect_Cancel = Get<RectTransform>("b_cancel");
        Img_Cancel = Get<Image>("b_cancel");
        Btn_Cancel = Get<Button>("b_cancel");
        Rect_CancelText = Get<RectTransform>("b_cancelText");
        Txt_CancelText = Get<TextMeshProUGUI>("b_cancelText");
        Rect_Confirm = Get<RectTransform>("b_confirm");
        Img_Confirm = Get<Image>("b_confirm");
        Btn_Confirm = Get<Button>("b_confirm");
        Rect_ConfirmText = Get<RectTransform>("b_confirmText");
        Txt_ConfirmText = Get<TextMeshProUGUI>("b_confirmText");
    }

    public override void OnClearup()
    {
    }
}
