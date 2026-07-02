using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.UI;

public sealed partial class LoadingWindow : UIWindow, IProcessingWindow
{
    public override async UniTask OnAwakeAsync()
    {
        await InitializeDesignAsync();
    }

    public override UniTask OnOpenAsync()
    {
        return UniTask.CompletedTask;
    }

    public override void Release()
    {
        ReleaseDesign();
        base.Release();
    }

    public void UpdateProcessing(string message, float progress)
    {
        this.text_info.SetText(message);
        this.slider_slider.SetValueWithoutNotify(progress);
    }
}
