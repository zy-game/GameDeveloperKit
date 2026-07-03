using System;
using System.Collections.Generic;
using GameDeveloperKit.Operation;
using Newtonsoft.Json;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    public sealed partial class ResourceModule
    {
        private sealed class StartupOperationHandle : OperationHandle
        {
            public override void Execute(params object[] args)
            {
                try
                {
                    var module = args.Length > 0 ? args[0] as ResourceModule : null;
                    if (module == null)
                    {
                        throw new ArgumentNullException(nameof(module));
                    }

                    var manifest = LoadStartupManifest();
                    if (manifest == null)
                    {
                        SetResult();
                        return;
                    }

                    module.ApplyStartupManifest(manifest);
                    SetResult();
                }
                catch (Exception exception)
                {
                    SetException(exception);
                }
            }

            private static ManifestInfo LoadStartupManifest()
            {
                var path = System.IO.Path.Combine(Application.streamingAssetsPath, ResourceSettings.MANIFEST_NAME);
                if (System.IO.File.Exists(path) is false)
                {
                    return null;
                }

                var text = System.IO.File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new GameException($"Startup resource manifest is empty: {path}");
                }

                var manifest = JsonConvert.DeserializeObject<ManifestInfo>(text);
                if (manifest == null)
                {
                    throw new GameException($"Unable to deserialize startup resource manifest: {path}");
                }

                return manifest;
            }
        }

        private void ApplyStartupManifest(ManifestInfo manifest)
        {
            _setting = new ResourceSettings
            {
                Mode = ResourceMode.Offline,
                ManifestName = ResourceSettings.MANIFEST_NAME,
                DefaultPackages = Array.Empty<string>()
            };
            _manifest = manifest;
            _localPackages.Clear();
            foreach (var package in GetPackageNames(manifest))
            {
                _localPackages.Add(package);
            }

            var builtinMode = new BuiltinMode(_manifest);
            _modes.Add(builtinMode);
            _modes.Add(new StreamingAssetMode(_manifest));
            _initializeState = ResourceInitializeState.LocalInitialized;
        }

        private static IEnumerable<string> GetPackageNames(ManifestInfo manifest)
        {
            if (manifest?.Packages == null)
            {
                yield break;
            }

            foreach (var package in manifest.Packages)
            {
                if (package == null || string.IsNullOrWhiteSpace(package.Name))
                {
                    continue;
                }

                yield return package.Name;
            }
        }

    }
}
