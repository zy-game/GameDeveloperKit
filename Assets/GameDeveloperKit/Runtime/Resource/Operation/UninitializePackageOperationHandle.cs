using System;
using System.Collections.Generic;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed class UninitializePackageOperationHandle : OperationHandle
    {
        public static UninitializePackageOperationHandle Failure(Exception exception)
        {
            var handle = new UninitializePackageOperationHandle();
            handle.SetException(exception);
            return handle;
        }

        public static UninitializePackageOperationHandle Success()
        {
            var handle = new UninitializePackageOperationHandle();
            handle.SetResult();
            return handle;
        }

        public override void Execute(params object[] args)
        {
            string packageName = args[0] as string;
            List<ProviderBase> providers = args[1] as List<ProviderBase>;
        }
    }
}