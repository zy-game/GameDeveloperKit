using System;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class BuiltinMode
    {
        public sealed class InitializePackageOperationHandle : OperationHandle
        {
            public static InitializePackageOperationHandle Failure(Exception exception)
            {
                var handle = new InitializePackageOperationHandle();
                handle.SetException(exception);
                return handle;
            }

            public static InitializePackageOperationHandle Success()
            {
                var handle = new InitializePackageOperationHandle();
                handle.SetResult();
                return handle;
            }

            public override async void Execute(params object[] args)
            {
                try
                {
                    var packageName = args[0] as string;
                    var mode = args.Length > 1 ? args[1] as BuiltinMode : null;
                    var manifest = args.Length > 2 ? args[2] as ManifestInfo : null;
                    Validate(packageName, mode, manifest);

                    var builtinBundle = manifest.GetBundle(BUILTIN_PACKAGE_NAME);
                    if (builtinBundle == null)
                    {
                        SetException(new GameException($"{packageName} not found"));
                        return;
                    }

                    var provider = new BuiltinProvider(builtinBundle);
                    var operation = await provider.InitializeProviderAsync();
                    if (operation.Status is not OperationStatus.Succeeded)
                    {
                        provider.Release();
                        SetException(operation.Error ?? new GameException($"{packageName} initialize failed"));
                        return;
                    }

                    mode._provider = provider;
                    SetResult();
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }

            private static void Validate(string packageName, BuiltinMode mode, ManifestInfo manifest)
            {
                if (packageName == null)
                {
                    throw new ArgumentNullException(nameof(packageName));
                }

                if (packageName is not BUILTIN_PACKAGE_NAME)
                {
                    throw new ArgumentException($"Invalid package: {packageName}", nameof(packageName));
                }

                if (mode == null)
                {
                    throw new ArgumentNullException(nameof(mode));
                }

                if (manifest == null)
                {
                    throw new ArgumentNullException(nameof(manifest));
                }
            }
        }
    }
}
