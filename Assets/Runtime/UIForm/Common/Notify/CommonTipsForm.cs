using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit;
using GameDeveloperKit.UI;


public class CommonTipsForm : UIFormBase<CommonTipsData, CommonTipsView>, INotify
{
    protected override void OnStartup(params object[] args)
    {
        base.OnStartup(args);

        // 解析参数
        Data.Message = args.Length > 0 ? args[0] as string : "";
        Data.Duration = args.Length > 1 && args[1] is float duration ? duration : 2f;

        // 启动自动关闭
        AutoCloseAsync().Forget();
    }

    private async UniTaskVoid AutoCloseAsync()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(Data.Duration));
        Game.UI.CloseForm<CommonTipsForm>();
    }

    protected override void OnClearup()
    {
        base.OnClearup();
    }

    public void SetInfo(string info)
    {
        View.Txt_Info.SetText(info);
    }
}
