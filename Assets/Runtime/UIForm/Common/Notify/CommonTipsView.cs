// 此代码由 UIBindData 自动生成
// 生成时间: 2025-12-04 14:50:34

using GameDeveloperKit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
[UIForm("Resources/Common/CommonTips", EUILayer.Top, EUIMode.Normal, false)]
public class CommonTipsView : UIViewBase
{
    #region 组件属性

    public RectTransform Rect_Info { get; private set; }
    public TextMeshProUGUI Txt_Info { get; private set; }
    public ContentSizeFitter ContentSizeFitter_Info { get; private set; }

    #endregion

    public override void OnStartup()
    {
        Rect_Info = Get<RectTransform>("b_info");
        Txt_Info = Get<TextMeshProUGUI>("b_info");
        ContentSizeFitter_Info = Get<ContentSizeFitter>("b_info");
    }

    public override void OnClearup()
    {
    }
}
