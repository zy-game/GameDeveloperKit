using Cysharp.Threading.Tasks;

public sealed partial class LoadingController
{
    public UniTask OnAwakeAsync(LoadingWindow window, LoadingModel model)
    {
        return UniTask.CompletedTask;
    }

    public UniTask OnOpenAsync(LoadingWindow window)
    {
        return UniTask.CompletedTask;
    }

    public void OnEnable(LoadingWindow window)
    {
    }

    public void OnDisable(LoadingWindow window)
    {
    }

    public void Release(LoadingWindow window)
    {
    }
}
