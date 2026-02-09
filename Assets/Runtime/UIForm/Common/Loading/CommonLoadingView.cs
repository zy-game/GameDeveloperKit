// 此代码由 UIBindData 自动生成
// 生成时间: 2025-12-05 09:38:08

using GameDeveloperKit.UI;
using UnityEngine;
using UnityEngine.UI;
[UIForm("Resources/Common/CommonLoading", EUILayer.Window, EUIMode.Normal, true)]
public class CommonLoadingView : UIViewBase
{
    #region 组件属性

    public RectTransform Rect_Slider { get; private set; }
    public Slider Sld_Slider { get; private set; }

    #endregion

    public override void OnStartup()
    {
        Rect_Slider = Get<RectTransform>("b_Slider");
        Sld_Slider = Get<Slider>("b_Slider");
    }

    public override void OnClearup()
    {
    }
}
