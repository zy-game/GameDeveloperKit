using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 编辑器资源模式
    /// </summary>
    public sealed partial class EditorSimulatorMode : ModeBase
    {
        private readonly List<ProviderBase> _providers;

        public EditorSimulatorMode(ManifestInfo manifest) : base(manifest)
        {
            this._providers = new List<ProviderBase>();
        }

        public override bool HasAsset(string location)
        {
            return this._providers.Any(x => x.HasAsset(location));
        }

        public override bool HasPackage(string package)
        {
            return this._providers.Any(x => x.Info != null && x.Info.Name == package);
        }

        public override async UniTask<OperationHandle> InitializePackageAsync(string package)
        {
            ValidateKey(package, nameof(package));
            if (package == BuiltinMode.BUILTIN_PACKAGE_NAME)
            {
                return InitializePackageOperationHandle.Failure(new GameException($"Package not found: {BuiltinMode.BUILTIN_PACKAGE_NAME}"));
            }

            var operation = await Super.Operation.WaitCompletionAsync<InitializePackageOperationHandle>(this, package, this._providers, Manifest);
            return operation;
        }

        public override async UniTask<OperationHandle> UninitializePackageAsync(string package)
        {
            ValidateKey(package, nameof(package));
            if (package == BuiltinMode.BUILTIN_PACKAGE_NAME || HasPackage(package) is false)
            {
                return UninitializePackageOperationHandle.Failure(new GameException($"Package not found: {package}"));
            }

            var operation = await Super.Operation.WaitCompletionAsync<UninitializePackageOperationHandle>(this, package, this._providers, Manifest);
            return operation;
        }

        public override async UniTask<AssetHandle> LoadAssetAsync(string location)
        {
            ValidateKey(location, nameof(location));

            var provider = this._providers.FirstOrDefault(x => x.HasAsset(location));
            if (provider == null)
            {
                return AssetHandle.Failure(new GameException($"Asset not found: {location}"));
            }

            return await provider.LoadAssetAsync(location);
        }

        public override async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label)
        {
            ValidateKey(label, nameof(label));
            var handles = new List<AssetHandle>();
            foreach (var provider in this._providers.Where(x => x.HasAsset(label)))
            {
                handles.Add(await provider.LoadAssetAsync(label));
            }

            return handles;
        }

        public override async UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByTypeAsync<T>()
        {
            var typeName = typeof(T).Name;
            var handles = new List<AssetHandle>();
            foreach (var provider in this._providers.Where(x => x.HasAsset(typeName)))
            {
                handles.AddRange(await provider.LoadAssetsByTypeAsync<T>());
            }

            return handles;
        }

        public override async UniTask<RawAssetHandle> LoadRawAssetAsync(string location)
        {
            ValidateKey(location, nameof(location));
            var provider = this._providers.FirstOrDefault(x => x.HasAsset(location));
            if (provider == null)
            {
                return RawAssetHandle.Failure(new GameException($"Raw asset not found: {location}"));
            }

            return await provider.LoadRawAssetAsync(location);
        }

        public override async UniTask<IReadOnlyList<RawAssetHandle>> LoadRawAssetsByLabelAsync(string label)
        {
            ValidateKey(label, nameof(label));
            var handles = new List<RawAssetHandle>();
            foreach (var provider in this._providers.Where(x => x.HasAsset(label)))
            {
                handles.Add(await provider.LoadRawAssetAsync(label));
            }

            return handles;
        }

        public override async UniTask<SceneAssetHandle> LoadSceneAssetAsync(string name)
        {
            ValidateKey(name, nameof(name));
            var provider = this._providers.FirstOrDefault(x => x.HasAsset(name));
            if (provider == null)
            {
                return SceneAssetHandle.Failure(new GameException($"Scene not found: {name}"));
            }

            return await provider.LoadSceneAssetAsync(name);
        }

        public override async UniTask UnloadUnusedAssetAsync()
        {
            var tasks = this._providers.Select(provider => provider.UnloadUnusedAssetAsync());
            await UniTask.WhenAll(tasks);
        }

        public override async UniTask UnloadAsset(AssetHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (handle.Info == null)
            {
                handle.Release();
                return;
            }

            var provider = this._providers.FirstOrDefault(x => x.HasAsset(handle.Info.Location));
            if (provider == null)
            {
                return;
            }

            await provider.UnloadAsset(handle);
        }

        public override void Release()
        {
            foreach (var provider in _providers.ToArray())
            {
                provider.Release();
            }

            _providers.Clear();
        }

        private static void ValidateKey(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }
        }
    }
}
