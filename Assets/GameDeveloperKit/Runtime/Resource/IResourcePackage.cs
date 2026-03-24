using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Runtime
{
    public interface IResourcePackage
    {
        string PackageName { get; }

        ResourcePackageState State { get; }

        string LastError { get; }

        bool IsReady { get; }

        ResourceUpdateReport LastUpdateReport { get; }

        IReadOnlyList<ResourceEntry> Entries { get; }

        void RegisterEntry(ResourceEntry entry);

        void RegisterEntries(IEnumerable<ResourceEntry> entries);

        int RemoveEntries(ResourceLocation location, ResourceEntryKind? kind = null);

        void ClearEntries(ResourceEntryKind? kind = null);

        AssetHandle LoadAsset(ResourceLocation location);

        UniTask<AssetHandle> LoadAssetAsync(ResourceLocation location, CancellationToken cancellationToken = default);

        IReadOnlyList<AssetHandle> LoadAssets(ResourceLocation location);

        UniTask<IReadOnlyList<AssetHandle>> LoadAssetsAsync(ResourceLocation location, CancellationToken cancellationToken = default);

        SceneHandle LoadScene(ResourceLocation location, LoadSceneMode loadMode = LoadSceneMode.Single);

        UniTask<SceneHandle> LoadSceneAsync(ResourceLocation location, LoadSceneMode loadMode = LoadSceneMode.Single, CancellationToken cancellationToken = default);

        RawFileHandle LoadRawFile(ResourceLocation location);

        UniTask<RawFileHandle> LoadRawFileAsync(ResourceLocation location, CancellationToken cancellationToken = default);

        IReadOnlyList<ResourceEntry> Find(ResourceLocation location, ResourceEntryKind? kind = null);

        void CollectUnused(bool force = false);
    }
}
