using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class EditorProvider
    {
        public sealed class LoadingAssetOperationHandle : OperationHandle<AssetHandle>
        {
            public override void Execute(params object[] args)
            {
                throw new System.NotImplementedException();
            }
        }
    }
}
