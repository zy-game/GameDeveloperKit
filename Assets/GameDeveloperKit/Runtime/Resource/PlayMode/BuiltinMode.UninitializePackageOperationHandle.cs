using System;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class BuiltinMode
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

            public override async void Execute(params object[] args)
            {
                try
                {
                    var packageName = args[0] as string;
                    var provider = args.Length > 1 ? args[1] as BuiltinProvider : null;
                    Validate(packageName, provider);

                    var operation = await provider.UninitializeProviderAsync();
                    if (operation.Status is not OperationStatus.Succeeded)
                    {
                        SetException(operation.Error ?? new GameException($"{packageName} uninitialize failed"));
                        return;
                    }

                    provider.Release();
                    SetResult();
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }

            private static void Validate(string packageName, BuiltinProvider provider)
            {
                if (packageName == null)
                {
                    throw new ArgumentNullException(nameof(packageName));
                }

                if (packageName is not BUILTIN_PACKAGE_NAME)
                {
                    throw new ArgumentException($"Invalid package: {packageName}", nameof(packageName));
                }

                if (provider == null)
                {
                    throw new ArgumentNullException(nameof(provider));
                }
            }
        }
    }
}
