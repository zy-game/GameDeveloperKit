using Cysharp.Threading.Tasks;
using GameDeveloperKit.UI;

public sealed partial class LoadingWindow : UIWindow
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
}
