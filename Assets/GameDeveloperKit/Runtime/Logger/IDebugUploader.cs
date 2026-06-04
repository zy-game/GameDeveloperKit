using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Logger
{
    public interface IDebugUploader
    {
        UniTask<DebugUploadResult> UploadAsync(DebugBundle bundle);
    }
}
