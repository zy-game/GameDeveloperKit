using Cysharp.Threading.Tasks;
using GameDeveloperKit;
using GameDeveloperKit.UI;


public class CommonDialogForm : UIFormBase<CommonDialogData, CommonDialogView>, IDialog
{
    private UniTaskCompletionSource<bool> _tcs;
    public void SetInfo(string title, string message, string confirm, string cancel)
    {
        // 绑定标题和内容
        View.Txt_Title.text = title;
        View.Txt_Info.text = message;

        // 绑定按钮文本
        View.Txt_ConfirmText.text = confirm ?? "确定";
        View.Txt_CancelText.text = cancel ?? "取消";

        // 控制取消按钮显示
        View.Rect_Cancel.gameObject.SetActive(string.IsNullOrEmpty(cancel) is false);
    }
    protected override void OnStartup(params object[] args)
    {
        base.OnStartup(args);
        _tcs = new UniTaskCompletionSource<bool>();
        // 绑定按钮事件
        View.Btn_Confirm.onClick.AddListener(OnConfirmClick);
        View.Btn_Cancel.onClick.AddListener(OnCancelClick);
    }

    /// <summary>
    /// 等待用户操作结果
    /// </summary>
    public UniTask<bool> WaitForResultAsync()
    {
        return _tcs.Task;
    }

    private void OnConfirmClick()
    {
        _tcs?.TrySetResult(true);
        Game.UI.CloseForm<CommonDialogForm>();
    }

    private void OnCancelClick()
    {
        _tcs?.TrySetResult(false);
        Game.UI.CloseForm<CommonDialogForm>();
    }

    protected override void OnClearup()
    {
        View.Btn_Confirm.onClick.RemoveListener(OnConfirmClick);
        View.Btn_Cancel.onClick.RemoveListener(OnCancelClick);

        // 确保Task完成
        _tcs?.TrySetResult(false);
        _tcs = null;

        base.OnClearup();
    }


}
