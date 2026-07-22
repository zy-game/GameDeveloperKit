using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;
using UnityEngine;

namespace GameDeveloperKit.Localization
{
    internal interface ILocalizationAssetLoader
    {
        UniTask<LocalizationAssetLease> LoadAsync(string location);
    }

    internal sealed class LocalizationAssetLease
    {
        private readonly Func<UniTask> m_Release;
        private int m_Released;

        public LocalizationAssetLease(string location, UnityEngine.Object asset, Func<UniTask> release)
        {
            Location = location ?? throw new ArgumentNullException(nameof(location));
            Asset = asset;
            m_Release = release ?? throw new ArgumentNullException(nameof(release));
        }

        public string Location { get; }

        public UnityEngine.Object Asset { get; }

        public async UniTask ReleaseAsync()
        {
            if (Interlocked.Exchange(ref m_Released, 1) != 0)
            {
                return;
            }

            await m_Release();
        }
    }

    internal sealed class ResourceLocalizationAssetLoader : ILocalizationAssetLoader
    {
        public async UniTask<LocalizationAssetLease> LoadAsync(string location)
        {
            ValidateResourceLocation(location);
            AssetHandle handle = null;
            try
            {
                handle = await App.Resource.LoadAssetAsync(location);
                if (handle == null || handle.Status is not ResourceStatus.Succeeded)
                {
                    throw new GameException($"Localization asset load failed: {location}", handle?.Error);
                }

                return new LocalizationAssetLease(location, handle.Asset, () => ReleaseAsync(handle));
            }
            catch
            {
                handle?.Release();
                throw;
            }
        }

        private static void ValidateResourceLocation(string location)
        {
            if (NetworkAssetProvider.IsNetworkLocation(location))
            {
                throw new GameException(
                    $"Localization asset location must be a Resource manifest address, not a network URL: {location}");
            }

            if (App.Resource.TryResolveAssetAddress(location, out var bundleName, out _) is false)
            {
                throw new GameException($"Localization asset location is not registered in the Resource manifest: {location}");
            }

            if (App.Resource.IsBundleInitialized(bundleName) is false)
            {
                throw new GameException(
                    $"Localization asset location belongs to an uninitialized Resource bundle: {location}. Bundle: {bundleName}");
            }
        }

        private static async UniTask ReleaseAsync(AssetHandle handle)
        {
            try
            {
                await App.Resource.UnloadAsset(handle);
            }
            catch
            {
                handle.Release();
            }
        }
    }
}
