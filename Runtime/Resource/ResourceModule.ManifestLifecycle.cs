using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    public sealed partial class ResourceModule
    {
        private static async UniTask<ResourceManifestIndex> LoadManifestIndexAsync(ResourceSettings setting)
        {
            var operation = await App.Operation.WaitCompletionWithKeyAsync<LoadManifestOperationHandle>(setting, setting);
            if (operation.Status is not OperationStatus.Succeeded)
            {
                if (operation.Error is GameException gameException)
                {
                    throw gameException;
                }

                throw new GameException(
                    $"Resource manifest initialize failed. Mode: {setting.Mode}",
                    operation.Error);
            }

            return operation.Value;
        }

        private async UniTask ApplyManifestIndexAsync(ResourceSettings setting, ResourceManifestIndex manifestIndex)
        {
            if (manifestIndex == null)
            {
                throw new ArgumentNullException(nameof(manifestIndex));
            }

            await _packageLifecycleGate.WaitAsync();
            try
            {
                await ApplyManifestIndexCoreAsync(setting, manifestIndex);
            }
            finally
            {
                _packageLifecycleGate.Release();
            }
        }

        private async UniTask ApplyManifestIndexCoreAsync(ResourceSettings setting, ResourceManifestIndex manifestIndex)
        {
            var stagedProviders = new List<ProviderBase>();
            var stagedSessions = new Dictionary<string, PackageSession>(StringComparer.Ordinal);
            try
            {
                if (manifestIndex.ContainsPackage(ResourceConstants.BUILTIN_PACKAGE_NAME))
                {
                    await InitializePackageOperationHandle.InitializeAsync(
                        ResourceConstants.BUILTIN_PACKAGE_NAME,
                        manifestIndex,
                        stagedProviders,
                        setting.Mode,
                        stagedSessions);
                }
            }
            catch
            {
                ReleaseProviders(stagedProviders);
                throw;
            }

            try
            {
                await _network.StopAndDrainPendingLoadsAsync();
                await StopAndDrainProviderLoadsAsync(_providers);
                await UnloadAllProviderScenesAsync(_providers);
            }
            catch
            {
                _network.ResumeLoadsAfterTeardownFailure();
                ResumeProviderLoads(_providers);
                ReleaseProviders(stagedProviders);
                throw;
            }

            ReleaseProviders();
            _packageSessions.Clear();
            _setting = setting;
            _mode = setting.Mode;
            _manifestIndex = manifestIndex;
            _providers.AddRange(stagedProviders);
            foreach (var session in stagedSessions)
            {
                _packageSessions.Add(session.Key, session.Value);
            }
            _startupError = null;
            await PruneBundleCacheAsync(App.File, manifestIndex);
        }

        internal static async UniTask PruneBundleCacheAsync(
            GameDeveloperKit.File.FileModule fileModule,
            ResourceManifestIndex manifestIndex)
        {
            if (fileModule == null)
            {
                throw new ArgumentNullException(nameof(fileModule));
            }

            if (manifestIndex == null)
            {
                throw new ArgumentNullException(nameof(manifestIndex));
            }

            IReadOnlyList<string> cachedPaths;
            try
            {
                cachedPaths = fileModule.ListPaths(BundleAssetProvider.BundleCachePrefix);
            }
            catch (Exception exception)
            {
                App.Debug.Error(exception, "Unable to enumerate Resource bundle cache paths.", "Resource");
                return;
            }

            var expectedVersions = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var bundle in manifestIndex.CreateRemoteBundleSnapshot())
            {
                expectedVersions.Add(
                    BundleAssetProvider.CreateBundleCacheKey(bundle),
                    BundleAssetProvider.CreateBundleCacheVersion(manifestIndex.Version, bundle));
            }

            foreach (var cachedPath in cachedPaths)
            {
                if (expectedVersions.TryGetValue(cachedPath, out var expectedVersion) &&
                    fileModule.Exists(cachedPath, expectedVersion))
                {
                    continue;
                }

                try
                {
                    await fileModule.DeleteAsync(cachedPath);
                }
                catch (Exception exception)
                {
                    App.Debug.Error(
                        exception,
                        $"Unable to prune stale Resource bundle cache entry: {cachedPath}",
                        "Resource");
                }
            }
        }

        private static ResourceManifestIndex LoadStartupManifestIndex()
        {
            var manifest = LoadManifestOperationHandle.ReadStartupManifest();
            return manifest == null
                ? null
                : ResourceManifestValidator.ValidateAndIndex(manifest, ResourceMode.Offline);
        }

        private void ApplyStartupManifestIndex(ResourceManifestIndex manifestIndex)
        {
            _setting = new ResourceSettings
            {
                Mode = ResourceMode.Offline,
                ManifestName = ResourceSettings.MANIFEST_NAME,
                DefaultPackages = Array.Empty<string>()
            };
            _manifestIndex = manifestIndex ?? throw new ArgumentNullException(nameof(manifestIndex));
            _mode = ResourceMode.Offline;
            _initializeState = ResourceInitializeState.LocalInitialized;
            if (_manifestIndex.ContainsPackage(ResourceConstants.BUILTIN_PACKAGE_NAME))
            {
                var operation = InitializePackageAsync(ResourceConstants.BUILTIN_PACKAGE_NAME).GetAwaiter().GetResult();
                if (operation.Status is not OperationStatus.Succeeded)
                {
                    throw new GameException($"{ResourceConstants.BUILTIN_PACKAGE_NAME} initialize failed.", operation.Error);
                }
            }
        }
    }
}
