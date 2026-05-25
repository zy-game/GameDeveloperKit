using System;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class BuiltinProvider
    {
        public sealed class UninitializeBundleOperationHandle : OperationHandle
        {
            public static UninitializeBundleOperationHandle Failure(Exception exception)
            {
                var handle = new UninitializeBundleOperationHandle();
                handle.SetException(exception);
                return handle;
            }

            public static UninitializeBundleOperationHandle Sucecess()
            {
                var handle = new UninitializeBundleOperationHandle();
                handle.SetResult();
                return handle;
            }

            public override void Execute(params object[] args)
            {
                SetResult();
            }
        }
    }
}
