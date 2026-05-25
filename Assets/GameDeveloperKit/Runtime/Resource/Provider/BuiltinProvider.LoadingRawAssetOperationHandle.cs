using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class BuiltinProvider
    {
        public sealed class LoadingRawAssetOperationHandle : OperationHandle<RawAssetHandle>
        {
            public override void Execute(params object[] args)
            {
            }
        }
    }
}
