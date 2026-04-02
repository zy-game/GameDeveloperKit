using System;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Runtime
{
    public sealed partial class SceneModule
    {
        private SceneHandle LoadInternal(ResourceLocation location, LoadSceneMode loadMode, string packageName, bool remember, bool useResource)
        {
            var copiedLocation = PrepareSceneLocation(location, packageName);
            NotifySceneLoadStarted(new SceneLoadInfo(packageName, copiedLocation, loadMode, false));

            var handle = useResource
                ? LoadResourceScene(copiedLocation, loadMode, packageName)
                : LoadDirectScene(copiedLocation, loadMode);
            RegisterHistory(CreateSceneHistoryEntry(handle, useResource), loadMode, remember);
            return handle;
        }

        private async UniTask<SceneHandle> LoadAsyncInternal(
            ResourceLocation location,
            LoadSceneMode loadMode,
            string packageName,
            bool remember,
            bool useResource,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var copiedLocation = PrepareSceneLocation(location, packageName);
            NotifySceneLoadStarted(new SceneLoadInfo(packageName, copiedLocation, loadMode, true));

            var transitionInfo = new SceneTransitionInfo(packageName, copiedLocation, loadMode, loadMode, true, false);
            NotifySceneTransitionProgress(transitionInfo, 0.1f);
            var handle = useResource
                ? await LoadResourceSceneAsync(copiedLocation, loadMode, packageName, cancellationToken)
                : await LoadDirectSceneAsync(copiedLocation, loadMode, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            NotifySceneTransitionProgress(transitionInfo, 1f);
            RegisterHistory(CreateSceneHistoryEntry(handle, useResource), loadMode, remember);
            return handle;
        }

        private UniTask<SceneHandle> LoadFromHistoryAsync(SceneHistoryEntry entry, CancellationToken cancellationToken)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            return LoadAsyncInternal(entry.Location, LoadSceneMode.Single, entry.PackageName, true, entry.UseResource, cancellationToken);
        }

        private async UniTask<SceneHandle> SwitchAsyncInternal(
            ResourceLocation location,
            string packageName,
            bool remember,
            bool useResource,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var copiedLocation = PrepareSceneLocation(location, packageName);
            var transitionInfo = new SceneTransitionInfo(packageName, copiedLocation, LoadSceneMode.Single, LoadSceneMode.Additive, true, _persistentScenes.Count > 0);
            NotifySceneTransitionStarted(transitionInfo);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await ChangeProcedureStateIfConfiguredAsync(BeforeSwitchProcedureStateName, copiedLocation, cancellationToken);
                NotifySceneTransitionProgress(transitionInfo, 0.1f);
                var handle = await LoadAsyncInternal(copiedLocation, LoadSceneMode.Additive, packageName, remember, useResource, cancellationToken);
                NotifySceneTransitionProgress(transitionInfo, 0.7f);
                await UnloadNonPersistentScenesAsync(GetSceneKey(handle.Scene), cancellationToken);
                NotifySceneTransitionProgress(transitionInfo, 0.9f);
                SetActiveScene(handle.Scene);
                await ChangeProcedureStateIfConfiguredAsync(AfterSwitchProcedureStateName, copiedLocation, cancellationToken);

                stopwatch.Stop();
                _transitionCount++;
                _lastTransitionDurationMilliseconds = stopwatch.ElapsedMilliseconds;
                NotifySceneTransitionProgress(transitionInfo, 1f);
                NotifySceneTransitionCompleted(transitionInfo.Complete(_lastTransitionDurationMilliseconds, GetSceneKey(handle.Scene)));
                return handle;
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                _transitionFailureCount++;
                _lastTransitionDurationMilliseconds = stopwatch.ElapsedMilliseconds;
                NotifySceneTransitionFailed(transitionInfo.Complete(_lastTransitionDurationMilliseconds, null), exception);
                throw;
            }
        }

        private static ResourceLocation CreateSceneLocation(string sceneNameOrPath)
        {
            if (sceneNameOrPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                || sceneNameOrPath.IndexOf('/') >= 0
                || sceneNameOrPath.IndexOf('\\') >= 0)
            {
                return new ResourceLocation
                {
                    FullPath = sceneNameOrPath
                };
            }

            return new ResourceLocation
            {
                Name = sceneNameOrPath
            };
        }

        private static ResourceLocation PrepareSceneLocation(ResourceLocation location, string packageName)
        {
            var copiedLocation = location?.Clone() ?? throw new ArgumentNullException(nameof(location));
            copiedLocation.PackageName = string.IsNullOrWhiteSpace(packageName) ? null : packageName;
            return copiedLocation;
        }

        private static SceneHandle LoadDirectScene(ResourceLocation location, LoadSceneMode loadMode)
        {
            var sceneIdentifier = GetSceneIdentifier(location);
            SceneManager.LoadScene(sceneIdentifier, loadMode);
            return CreateDirectSceneHandle(location, sceneIdentifier);
        }

        private static async UniTask<SceneHandle> LoadDirectSceneAsync(ResourceLocation location, LoadSceneMode loadMode, CancellationToken cancellationToken)
        {
            var sceneIdentifier = GetSceneIdentifier(location);
            var operation = SceneManager.LoadSceneAsync(sceneIdentifier, loadMode);
            if (operation == null)
            {
                throw new InvalidOperationException($"Failed to start loading scene '{sceneIdentifier}'.");
            }

            await operation.ToUniTask(cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return CreateDirectSceneHandle(location, sceneIdentifier);
        }

        private static SceneHandle CreateDirectSceneHandle(ResourceLocation location, string sceneIdentifier)
        {
            var scene = ResolveLoadedSceneOrThrow(sceneIdentifier);
            return new SceneHandle(null, location, scene, sceneIdentifier);
        }

        private static SceneHandle LoadResourceScene(ResourceLocation location, LoadSceneMode loadMode, string packageName)
        {
            return string.IsNullOrWhiteSpace(packageName)
                ? Game.Resource.LoadScene(location, loadMode)
                : GetResourcePackage(packageName).LoadScene(location, loadMode);
        }

        private static async UniTask<SceneHandle> LoadResourceSceneAsync(
            ResourceLocation location,
            LoadSceneMode loadMode,
            string packageName,
            CancellationToken cancellationToken)
        {
            return string.IsNullOrWhiteSpace(packageName)
                ? await Game.Resource.LoadSceneAsync(location, loadMode, cancellationToken)
                : await GetResourcePackage(packageName).LoadSceneAsync(location, loadMode, cancellationToken);
        }

        private static string GetSceneIdentifier(ResourceLocation location)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            if (!string.IsNullOrWhiteSpace(location.FullPath))
            {
                return location.FullPath;
            }

            if (!string.IsNullOrWhiteSpace(location.Name))
            {
                return location.Name;
            }

            throw new InvalidOperationException("Scene location must contain either FullPath or Name.");
        }

        private static SceneHistoryEntry CreateSceneHistoryEntry(SceneHandle handle, bool useResource)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            return new SceneHistoryEntry(GetSceneKey(handle.Scene), handle.PackageName, handle.Location, useResource);
        }

        private static IResourcePackage GetResourcePackage(string packageName)
        {
            if (!Game.HasModule<ResourceModule>())
            {
                throw new InvalidOperationException("Resource module is not available.");
            }

            return Game.Resource.GetPackage(packageName);
        }

        private static Scene ResolveLoadedSceneOrThrow(string sceneNameOrPath)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (IsSceneMatch(scene, sceneNameOrPath))
                {
                    return scene;
                }
            }

            throw new InvalidOperationException($"Scene '{sceneNameOrPath}' is not loaded.");
        }
    }
}
