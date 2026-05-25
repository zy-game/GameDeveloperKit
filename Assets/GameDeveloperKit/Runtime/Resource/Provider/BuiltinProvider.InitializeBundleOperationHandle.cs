using System;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class BuiltinProvider
    {
        public sealed class InitializeBundleOperationHandle : OperationHandle<BundleHandle>
        {
            public static InitializeBundleOperationHandle Failure(Exception exception)
            {
                var handle = new InitializeBundleOperationHandle();
                handle.SetException(exception);
                return handle;
            }

            public static InitializeBundleOperationHandle Success(BundleInfo bundleInfo = null)
            {
                var handle = new InitializeBundleOperationHandle();
                handle.SetResult(BundleHandle.Success(bundleInfo, null));
                return handle;
            }

            public override void Execute(params object[] args)
            {
                var bundleInfo = args.Length > 0 ? args[0] as BundleInfo : null;
                SetResult(BundleHandle.Success(bundleInfo, null));
            }
        }
    }
}
