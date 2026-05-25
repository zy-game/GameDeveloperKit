using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class BuiltinProvider
    {
        public sealed class LoadingAssetOperationHandle : OperationHandle<AssetHandle>
        {
            public override void Execute(params object[] args)
            {
            }
        }
    }
}
