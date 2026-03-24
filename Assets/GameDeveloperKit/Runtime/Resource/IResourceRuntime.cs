using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    public interface IResourceRuntime
    {
        UniTask InitializePackageAsync(ResourcePackageContext context, CancellationToken cancellationToken = default);

        UniTask EnsurePackageReadyAsync(ResourcePackageContext context, CancellationToken cancellationToken = default);

        Object LoadAsset(ResourcePackageContext context, ResourceEntry entry);

        UniTask<Object> LoadAssetAsync(ResourcePackageContext context, ResourceEntry entry, CancellationToken cancellationToken = default);

        string ResolveScenePath(ResourcePackageContext context, ResourceEntry entry);

        string ResolveFilePath(ResourcePackageContext context, ResourceEntry entry);

        IReadOnlyList<ResourceEntry> BuildEntries(ResourcePackageContext context);
    }
}
