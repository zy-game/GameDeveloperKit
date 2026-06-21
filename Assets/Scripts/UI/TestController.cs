using Cysharp.Threading.Tasks;

public sealed partial class TestController
{
    public UniTask OnAwakeAsync(TestWindow window, TestModel model)
    {
        return UniTask.CompletedTask;
    }

    public UniTask OnOpenAsync(TestWindow window)
    {
        return UniTask.CompletedTask;
    }

    public void OnEnable(TestWindow window)
    {
    }

    public void OnDisable(TestWindow window)
    {
    }

    public void Release(TestWindow window)
    {
    }
}
