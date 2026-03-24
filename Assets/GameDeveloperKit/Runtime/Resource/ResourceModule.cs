using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Runtime
{
    public sealed class ResourceModule : IGameFrameworkModule
    {
        private sealed class ResourceModuleDriver : MonoBehaviour
        {
            private ResourceModule _module;

            public void Initialize(ResourceModule module)
            {
                _module = module;
            }

            private void Update()
            {
                _module?.CollectUnused();
            }
        }

        private readonly Dictionary<string, IResourcePackage> _packages = new(StringComparer.Ordinal);
        private readonly ResourceModuleDriver _driver;
        private ResourcePlayMode _playMode;
        private string _defaultPackageName;

        public ResourceModule()
        {
            _playMode = ResourcePlayMode.Offline;
            var driverObject = new GameObject("[GameDeveloperKit.ResourceModule]");
            UnityEngine.Object.DontDestroyOnLoad(driverObject);
            _driver = driverObject.AddComponent<ResourceModuleDriver>();
            _driver.Initialize(this);
        }

        public string DefaultPackageName => _defaultPackageName;

        public ResourcePlayMode PlayMode => _playMode;

        public void Initialize(ResourceSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _packages.Clear();
            _defaultPackageName = null;
            _playMode = settings.PlayMode;

            for (var i = 0; i < settings.Packages.Count; i++)
            {
                var definition = settings.Packages[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.PackageName))
                {
                    continue;
                }

                var options = new ResourcePackageOptions
                {
                    RootPath = definition.PersistentRoot,
                    Entries = definition.Entries
                };

                var context = new ResourcePackageContext(settings.PlayMode, definition);
                var runtime = CreateRuntime(settings.PlayMode);
                var package = new ResourcePackage(definition.PackageName, options, runtime, context);
                _packages[definition.PackageName] = package;

                if (definition.IsDefault || string.IsNullOrWhiteSpace(_defaultPackageName))
                {
                    _defaultPackageName = definition.PackageName;
                }
            }
        }

        public void InitializePackage(string packageName, ResourcePackageOptions options, bool setAsDefault = false)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentException("Package name can not be empty.", nameof(packageName));
            }

            var definition = new ResourcePackageDefinition
            {
                PackageName = packageName,
                IsDefault = setAsDefault || string.IsNullOrWhiteSpace(_defaultPackageName),
                Role = ResourcePackageRole.Builtin,
                PersistentRoot = options?.RootPath,
                Entries = options?.Entries == null ? new List<ResourceEntry>() : new List<ResourceEntry>(options.Entries)
            };

            var context = new ResourcePackageContext(_playMode, definition);
            var package = new ResourcePackage(packageName, options, CreateRuntime(_playMode), context);
            _packages[packageName] = package;

            if (setAsDefault || string.IsNullOrWhiteSpace(_defaultPackageName))
            {
                _defaultPackageName = packageName;
            }
        }

        public void SetDefaultPackage(string packageName)
        {
            if (!HasPackage(packageName))
            {
                throw new InvalidOperationException($"Package '{packageName}' is not initialized.");
            }

            _defaultPackageName = packageName;
        }

        public bool HasPackage(string packageName)
        {
            return !string.IsNullOrWhiteSpace(packageName) && _packages.ContainsKey(packageName);
        }

        public bool TryGetPackage(string packageName, out IResourcePackage package)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                package = null;
                return false;
            }

            return _packages.TryGetValue(packageName, out package);
        }

        public IResourcePackage GetPackage(string packageName)
        {
            if (!TryGetPackage(packageName, out var package))
            {
                throw new InvalidOperationException($"Package '{packageName}' is not initialized.");
            }

            return package;
        }

        public ResourcePackageState GetPackageState(string packageName)
        {
            return GetPackage(packageName).State;
        }

        public string GetPackageLastError(string packageName)
        {
            return GetPackage(packageName).LastError;
        }

        public ResourceUpdateReport GetPackageUpdateReport(string packageName)
        {
            return GetPackage(packageName).LastUpdateReport;
        }

        public bool RemovePackage(string packageName)
        {
            if (!_packages.Remove(packageName, out var package))
            {
                return false;
            }

            package.CollectUnused(true);

            if (string.Equals(_defaultPackageName, packageName, StringComparison.Ordinal))
            {
                _defaultPackageName = null;
            }

            return true;
        }

        public AssetHandle LoadAsset(string name)
        {
            return DefaultPackage.LoadAsset(new ResourceLocation { Name = name });
        }

        public AssetHandle LoadAsset<TAsset>(string name)
            where TAsset : UnityEngine.Object
        {
            return DefaultPackage.LoadAsset(new ResourceLocation { Name = name, AssetType = typeof(TAsset) });
        }

        public UniTask<AssetHandle> LoadAssetAsync(string name, CancellationToken cancellationToken = default)
        {
            return DefaultPackage.LoadAssetAsync(new ResourceLocation { Name = name }, cancellationToken);
        }

        public UniTask<AssetHandle> LoadAssetAsync<TAsset>(string name, CancellationToken cancellationToken = default)
            where TAsset : UnityEngine.Object
        {
            return DefaultPackage.LoadAssetAsync(new ResourceLocation { Name = name, AssetType = typeof(TAsset) }, cancellationToken);
        }

        public IReadOnlyList<AssetHandle> LoadAssetsByLabel(string label)
        {
            return DefaultPackage.LoadAssets(new ResourceLocation { Labels = new[] { label } });
        }

        public IReadOnlyList<AssetHandle> LoadAssetsByLabel<TAsset>(string label)
            where TAsset : UnityEngine.Object
        {
            return DefaultPackage.LoadAssets(new ResourceLocation { Labels = new[] { label }, AssetType = typeof(TAsset) });
        }

        public UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label, CancellationToken cancellationToken = default)
        {
            return DefaultPackage.LoadAssetsAsync(new ResourceLocation { Labels = new[] { label } }, cancellationToken);
        }

        public UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync<TAsset>(string label, CancellationToken cancellationToken = default)
            where TAsset : UnityEngine.Object
        {
            return DefaultPackage.LoadAssetsAsync(new ResourceLocation { Labels = new[] { label }, AssetType = typeof(TAsset) }, cancellationToken);
        }

        public IReadOnlyList<AssetHandle> LoadAssetsByType<TAsset>()
            where TAsset : UnityEngine.Object
        {
            return DefaultPackage.LoadAssets(new ResourceLocation { AssetType = typeof(TAsset) });
        }

        public UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByTypeAsync<TAsset>(CancellationToken cancellationToken = default)
            where TAsset : UnityEngine.Object
        {
            return DefaultPackage.LoadAssetsAsync(new ResourceLocation { AssetType = typeof(TAsset) }, cancellationToken);
        }

        public AssetHandle LoadAsset(ResourceLocation location)
        {
            return DefaultPackage.LoadAsset(location);
        }

        public UniTask<AssetHandle> LoadAssetAsync(ResourceLocation location, CancellationToken cancellationToken = default)
        {
            return DefaultPackage.LoadAssetAsync(location, cancellationToken);
        }

        public IReadOnlyList<AssetHandle> LoadAssets(ResourceLocation location)
        {
            return DefaultPackage.LoadAssets(location);
        }

        public UniTask<IReadOnlyList<AssetHandle>> LoadAssetsAsync(ResourceLocation location, CancellationToken cancellationToken = default)
        {
            return DefaultPackage.LoadAssetsAsync(location, cancellationToken);
        }

        public SceneHandle LoadScene(string name, LoadSceneMode loadMode = LoadSceneMode.Single)
        {
            return DefaultPackage.LoadScene(new ResourceLocation { Name = name }, loadMode);
        }

        public UniTask<SceneHandle> LoadSceneAsync(string name, LoadSceneMode loadMode = LoadSceneMode.Single, CancellationToken cancellationToken = default)
        {
            return DefaultPackage.LoadSceneAsync(new ResourceLocation { Name = name }, loadMode, cancellationToken);
        }

        public RawFileHandle LoadRawFile(string fullPath)
        {
            return DefaultPackage.LoadRawFile(new ResourceLocation { FullPath = fullPath });
        }

        public UniTask<RawFileHandle> LoadRawFileAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            return DefaultPackage.LoadRawFileAsync(new ResourceLocation { FullPath = fullPath }, cancellationToken);
        }

        public void CollectUnused(bool force = false)
        {
            foreach (var package in _packages.Values)
            {
                package.CollectUnused(force);
            }
        }

        public IReadOnlyList<ResourceEntry> Find(ResourceLocation location, ResourceEntryKind? kind = null)
        {
            return DefaultPackage.Find(location, kind);
        }

        public void RegisterEntry(ResourceEntry entry)
        {
            DefaultPackage.RegisterEntry(entry);
        }

        public void RegisterEntries(IEnumerable<ResourceEntry> entries)
        {
            DefaultPackage.RegisterEntries(entries);
        }

        public int RemoveEntries(ResourceLocation location, ResourceEntryKind? kind = null)
        {
            return DefaultPackage.RemoveEntries(location, kind);
        }

        public void ClearEntries(ResourceEntryKind? kind = null)
        {
            DefaultPackage.ClearEntries(kind);
        }

        public void Dispose()
        {
            CollectUnused(true);
            _packages.Clear();
            _defaultPackageName = null;
            if (_driver != null)
            {
                UnityEngine.Object.Destroy(_driver.gameObject);
            }
        }

        private static IResourceRuntime CreateRuntime(ResourcePlayMode playMode)
        {
            switch (playMode)
            {
                case ResourcePlayMode.EditorSimulate:
#if UNITY_EDITOR
                    return new EditorSimulateResourceRuntime();
#else
                    return new OfflineResourceRuntime();
#endif
                case ResourcePlayMode.Offline:
                    return new OfflineResourceRuntime();
                case ResourcePlayMode.Host:
                    return new HostResourceRuntime();
                case ResourcePlayMode.Web:
                    return new WebResourceRuntime();
                default:
                    throw new ArgumentOutOfRangeException(nameof(playMode), playMode, null);
            }
        }

        private IResourcePackage DefaultPackage
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_defaultPackageName))
                {
                    throw new InvalidOperationException("Default resource package is not initialized.");
                }

                return GetPackage(_defaultPackageName);
            }
        }
    }
}
