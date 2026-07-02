using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class ResourceModule
    {
        private sealed class FullReadyOperationHandle : OperationHandle
        {
            public override async void Execute(params object[] args)
            {
                try
                {
                    var module = args.Length > 0 ? args[0] as ResourceModule : null;
                    var setting = args.Length > 1 ? args[1] as ResourceSettings : null;
                    var preserveStartupModes = args.Length > 2 && args[2] is bool value && value;
                    if (module == null)
                    {
                        throw new ArgumentNullException(nameof(module));
                    }

                    if (setting == null)
                    {
                        throw new ArgumentNullException(nameof(setting));
                    }

                    var operation = await App.Operation.WaitCompletionWithKeyAsync<ManifestLoadOperationHandle>(
                        $"{setting.Mode}:manifest:full",
                        setting);
                    if (operation.Status is not OperationStatus.Succeeded || operation.Value == null)
                    {
                        throw new GameException($"Resource manifest load failed. Mode: {setting.Mode}", operation.Error);
                    }

                    await module.ApplyManifestLoadResultAsync(setting, operation.Value, preserveStartupModes);
                    await module.InitializeDefaultPackagesAsync(setting);
                    SetResult();
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }
        }

        private async UniTask ApplyManifestLoadResultAsync(
            ResourceSettings setting,
            ManifestLoadResult result,
            bool preserveStartupModes)
        {
            var builtinMode = preserveStartupModes ? _modes.OfType<BuiltinMode>().FirstOrDefault() : null;
            var localMode = preserveStartupModes ? _modes.OfType<StreamingAssetMode>().FirstOrDefault() : null;
            if (preserveStartupModes)
            {
                ReleaseNonStartupModes();
            }
            else
            {
                ReleaseModes();
            }

            _localPackages.Clear();
            _setting = setting;
            foreach (var package in result.LocalPackages)
            {
                _localPackages.Add(package);
            }

            _manifest = result.Manifest;

            if (builtinMode == null)
            {
                builtinMode = new BuiltinMode(_manifest);
                _modes.Add(builtinMode);
            }

            if (localMode == null)
            {
                localMode = new StreamingAssetMode(_manifest);
                _modes.Add(localMode);
            }

            var selectedMode = CreateModeByType(setting.Mode);
            if (selectedMode == null)
            {
                throw new GameException($"Unsupported resource mode: {setting.Mode}");
            }

            if (_modes.Any(x => x.GetType() == selectedMode.GetType()) is false)
            {
                _modes.Add(selectedMode);
            }

            if (builtinMode.Status is not ResourceStatus.Succeeded && HasBuiltinPackage(_manifest))
            {
                var operation = await InitializePackageAsync(BuiltinMode.BUILTIN_PACKAGE_NAME);
                if (operation.Status is not OperationStatus.Succeeded)
                {
                    throw new GameException($"{BuiltinMode.BUILTIN_PACKAGE_NAME} initialize failed.", operation.Error);
                }
            }
        }

        private async UniTask InitializeDefaultPackagesAsync(ResourceSettings setting)
        {
            if (setting.DefaultPackages == null)
            {
                return;
            }

            for (var i = 0; i < setting.DefaultPackages.Length; i++)
            {
                var package = setting.DefaultPackages[i];
                if (string.IsNullOrWhiteSpace(package))
                {
                    continue;
                }

                if (string.Equals(package, BuiltinMode.BUILTIN_PACKAGE_NAME, StringComparison.Ordinal) is false &&
                    GetModeByPackage(package)?.HasPackage(package) == true)
                {
                    continue;
                }

                var packageOperation = await InitializePackageAsync(package);
                if (packageOperation.Status is not OperationStatus.Succeeded)
                {
                    throw new GameException($"Default package initialize failed: {package}", packageOperation.Error);
                }
            }
        }
    }
}
