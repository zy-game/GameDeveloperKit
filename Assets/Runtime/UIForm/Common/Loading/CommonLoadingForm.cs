// 此代码由 UIBindData 自动生成
// 生成时间: 2025-12-05 09:38:08

using GameDeveloperKit.UI;


public class CommonLoadingForm : UIFormBase<CommonLoadingData, CommonLoadingView>, ILoading
{
    protected override void OnStartup(params object[] args)
    {
        base.OnStartup(args);
    }

    protected override void OnClearup()
    {
        base.OnClearup();
    }

    public void SetInfo(string text)
    {
    }

    public void SetVersion(string text)
    {
    }

    public void SetProgress(float process)
    {
        if (View is null) return;
        View.Sld_Slider.SetValueWithoutNotify(process);
    }
}
