using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed class InitializePackageOperationHandle : OperationHandle<List<ProviderBase>>
    {
        public static InitializePackageOperationHandle Failure(Exception exception)
        {
            InitializePackageOperationHandle handle = new InitializePackageOperationHandle();
            handle.SetException(exception);
            return handle;
        }

        public static InitializePackageOperationHandle Success()
        {
            return new InitializePackageOperationHandle();
        }

        public override void Execute(params object[] args)
        {
            string packageName = args[0] as string;
            List<ProviderBase> providers = args[1] as List<ProviderBase>;
        }
    }
}