using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class EditorProvider
    {
        public sealed class LoadingRawAssetOperationHandle : OperationHandle<RawAssetHandle>
        {
            public override void Execute(params object[] args)
            {
                throw new System.NotImplementedException();
            }
        }
    }
}
