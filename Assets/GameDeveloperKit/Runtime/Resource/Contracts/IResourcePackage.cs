using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源包接口，定义资源包的基本属性和操作。
    /// </summary>
    public interface IResourcePackage
    {
        /// <summary>
        /// 获取资源包名称。
        /// </summary>
        string PackageName { get; }

        /// <summary>
        /// 获取资源包状态。
        /// </summary>
        ResourcePackageState State { get; }

        /// <summary>
        /// 获取最后一次错误信息。
        /// </summary>
        string LastError { get; }

        /// <summary>
        /// 获取资源包是否已就绪。
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// 获取资源包是否已准备。
        /// </summary>
        bool IsPrepared { get; }

        /// <summary>
        /// 获取最后一次更新报告。
        /// </summary>
        ResourceUpdateReport LastUpdateReport { get; }

        /// <summary>
        /// 获取资源包中的所有资源条目。
        /// </summary>
        IReadOnlyList<ResourceEntry> Entries { get; }

        /// <summary>
        /// 异步初始化资源包。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        UniTask InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步更新资源包。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源更新结果的异步任务。</returns>
        UniTask<ResourceUpdateResult> UpdateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步准备资源包。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        UniTask PrepareAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 注册单个资源条目。
        /// </summary>
        /// <param name="entry">资源条目。</param>
        void RegisterEntry(ResourceEntry entry);

        /// <summary>
        /// 注册多个资源条目。
        /// </summary>
        /// <param name="entries">资源条目集合。</param>
        void RegisterEntries(IEnumerable<ResourceEntry> entries);

        /// <summary>
        /// 移除指定位置的资源条目。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="kind">资源条目类型。</param>
        /// <returns>移除的资源条目数量。</returns>
        int RemoveEntries(ResourceLocation location, ResourceEntryKind? kind = null);

        /// <summary>
        /// 清除指定类型的所有资源条目。
        /// </summary>
        /// <param name="kind">资源条目类型。</param>
        void ClearEntries(ResourceEntryKind? kind = null);

        /// <summary>
        /// 同步加载指定位置的资源。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <returns>资源句柄。</returns>
        AssetHandle LoadAsset(ResourceLocation location);

        /// <summary>
        /// 异步加载指定位置的资源。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源句柄的异步任务。</returns>
        UniTask<AssetHandle> LoadAssetAsync(ResourceLocation location, CancellationToken cancellationToken = default);

        /// <summary>
        /// 同步加载指定位置的资源列表。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <returns>资源句柄列表。</returns>
        IReadOnlyList<AssetHandle> LoadAssets(ResourceLocation location);

        /// <summary>
        /// 异步加载指定位置的资源列表。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源句柄列表的异步任务。</returns>
        UniTask<IReadOnlyList<AssetHandle>> LoadAssetsAsync(ResourceLocation location, CancellationToken cancellationToken = default);

        /// <summary>
        /// 同步加载指定位置的场景。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="loadMode">场景加载模式。</param>
        /// <returns>场景句柄。</returns>
        SceneHandle LoadScene(ResourceLocation location, LoadSceneMode loadMode = LoadSceneMode.Single);

        /// <summary>
        /// 异步加载指定位置的场景。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="loadMode">场景加载模式。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>场景句柄的异步任务。</returns>
        UniTask<SceneHandle> LoadSceneAsync(ResourceLocation location, LoadSceneMode loadMode = LoadSceneMode.Single, CancellationToken cancellationToken = default);

        /// <summary>
        /// 同步加载指定位置的原始文件。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <returns>原始文件句柄。</returns>
        RawFileHandle LoadRawFile(ResourceLocation location);

        /// <summary>
        /// 异步加载指定位置的原始文件。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>原始文件句柄的异步任务。</returns>
        UniTask<RawFileHandle> LoadRawFileAsync(ResourceLocation location, CancellationToken cancellationToken = default);

        /// <summary>
        /// 查找指定位置的资源条目列表。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="kind">资源条目类型。</param>
        /// <returns>资源条目列表。</returns>
        IReadOnlyList<ResourceEntry> Find(ResourceLocation location, ResourceEntryKind? kind = null);

        /// <summary>
        /// 收集未使用的资源。
        /// </summary>
        /// <param name="force">是否强制回收。</param>
        void CollectUnused(bool force = false);
    }
}
