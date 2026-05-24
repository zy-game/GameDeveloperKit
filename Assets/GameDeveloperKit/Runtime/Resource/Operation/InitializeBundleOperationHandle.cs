using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using UnityEngine;
using UnityEngine.Networking;

namespace GameDeveloperKit.Resource
{
    public sealed class InitializeBundleOperationHandle : OperationHandle<BundleHandle>
    {
        public static InitializeBundleOperationHandle Failure(Exception exception)
        {
            var handle = new InitializeBundleOperationHandle();
            handle.SetException(exception);
            return handle;
        }

        public static InitializeBundleOperationHandle Success()
        {
            var handle = new InitializeBundleOperationHandle();
            handle.SetResult();
            return handle;
        }

        public override void Execute(params object[] args)
        {
            throw new NotImplementedException();
        }
    }
}