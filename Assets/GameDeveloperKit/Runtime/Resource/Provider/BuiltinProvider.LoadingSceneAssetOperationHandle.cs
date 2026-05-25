using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class BuiltinProvider
    {
        public sealed class LoadingSceneAssetOperationHandle : OperationHandle<SceneAssetHandle>
        {
            public override void Execute(params object[] args)
            {
            }
        }
    }
}
