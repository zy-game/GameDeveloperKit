using System;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class BundleProvider
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
                try
                {
                    var bundle = args.Length > 1 ? args[1] as BundleHandle : null;
                    if (bundle == null)
                    {
                        SetException(new ArgumentNullException(nameof(bundle)));
                        return;
                    }

                    bundle.Release();
                    SetResult();
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }
        }
    }
}
