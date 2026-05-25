using System;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class EditorProvider
    {
        public sealed class InitializeBundleOperationHandle : OperationHandle<BundleHandle>
        {
            public static InitializeBundleOperationHandle Failure(Exception exception)
            {
                var handle = new InitializeBundleOperationHandle();
                handle.SetException(exception);
                return handle;
            }

            public override void Execute(params object[] args)
            {
                SetException(new NotImplementedException());
            }
        }
    }
}
