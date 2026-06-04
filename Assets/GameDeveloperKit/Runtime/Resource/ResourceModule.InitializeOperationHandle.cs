using System;
using System.Linq;
using GameDeveloperKit.Operation;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    public sealed partial class ResourceModule
    {
        sealed class InitializeOperationHandle : OperationHandle<ManifestInfo>
        {
            public override async void Execute(params object[] args)
            {
                try
                {
                    Super.Debug.Assert(args is { Length: > 1 });
                    var module = (ResourceModule)args[0];
                    ResourceSettings _setting = (ResourceSettings)args[1];

                    Super.Debug.Info($"Resource settings loaded. ServerUrl: {_setting.ServerUrl}, Mode: {_setting.Mode}");
                    string manifestLocation = string.Empty;
                    switch (_setting.Mode)
                    {
                        case ResourceMode.EditorSimulator:
                            manifestLocation = $"Assets/GameDeveloperKit/Runtime/Resource/{_setting.ManifestName}";
                            break;
                        case ResourceMode.Offline:
                            manifestLocation = $"{Application.streamingAssetsPath}/{_setting.ManifestName}";
                            break;
                        case ResourceMode.Online:
                        case ResourceMode.Web:
                            if (string.IsNullOrWhiteSpace(_setting.ServerUrl))
                            {
                                throw new GameException("Server URL cannot be empty for online or web resource mode.");
                            }

                            manifestLocation = _setting.GetManifestAddress(string.Empty);
                            break;
                        default:
                            throw new GameException($"Unsupported resource mode: {_setting.Mode}");
                    }

                    var publishLocation = _setting.GetPublishAddress();
                    var versionHandle = await Super.Operation.WaitCompletionWithKeyAsync<PublishVersionOperationHandle>(publishLocation, publishLocation);
                    if (versionHandle.Status is not OperationStatus.Succeeded || string.IsNullOrWhiteSpace(versionHandle.Value))
                    {
                        throw new GameException($"Failed to load resource publish version: {publishLocation}", versionHandle.Error);
                    }

                    module._currentVersion = versionHandle.Value;
                    manifestLocation = _setting.GetManifestAddress(module._currentVersion);

                    var operationHandle = await Super.Operation.WaitCompletionWithKeyAsync<ManifestOperationHandle>(manifestLocation, manifestLocation);
                    if (operationHandle.Status is not OperationStatus.Succeeded || operationHandle.Value == null)
                    {
                        throw new GameException($"Failed to load resource manifest: {manifestLocation}", operationHandle.Error);
                    }

                    module._manifest = operationHandle.Value;
                    BuiltinMode builtinMode = null;
                    module.modes.Clear();
                    module.modes.Add(new StreamingAssetMode(module._manifest));
                    module.modes.Add(builtinMode = new BuiltinMode(module._manifest));
                    var settingMode = module.CreateModeByType(_setting.Mode);
                    if (settingMode != null && module.modes.Any(x => x.GetType() == settingMode.GetType()) is false)
                    {
                        module.modes.Add(settingMode);
                    }

                    if (module._manifest.GetBundle(BuiltinMode.BUILTIN_PACKAGE_NAME) != null)
                    {
                        var builtinOperation = await builtinMode.InitializePackageAsync(BuiltinMode.BUILTIN_PACKAGE_NAME);
                        if (builtinOperation.Status is not OperationStatus.Succeeded)
                        {
                            throw new GameException($"{BuiltinMode.BUILTIN_PACKAGE_NAME} initialize failed.", builtinOperation.Error);
                        }
                    }

                    if (_setting.DefaultPackages == null || _setting.DefaultPackages.Length == 0)
                    {
                        SetResult(module._manifest);
                        return;
                    }

                    for (int i = 0; i < _setting.DefaultPackages.Length; i++)
                    {
                        var packageOperation = await module.InitializePackageAsync(_setting.DefaultPackages[i]);
                        if (packageOperation.Status is not OperationStatus.Succeeded)
                        {
                            throw new GameException($"Default package initialize failed: {_setting.DefaultPackages[i]}", packageOperation.Error);
                        }
                    }

                    SetResult(module._manifest);
                }
                catch (Exception e)
                {
                    SetException(e);
                }
            }
        }
    }
}
