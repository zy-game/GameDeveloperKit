using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源提供器接口，定义资源加载和查询的核心功能。
    /// </summary>
    public interface IResourceProvider
    {
        /// <summary>
        /// 同步加载指定名称的资源。
        /// </summary>
        /// <param name="name">资源名称。</param>
        /// <returns>资源句柄。</returns>
        AssetHandle LoadAsset(string name);

        /// <summary>
        /// 同步加载指定名称的指定类型资源。
        /// </summary>
        /// <typeparam name="TAsset">资源类型。</typeparam>
        /// <param name="name">资源名称。</param>
        /// <returns>资源句柄。</returns>
        AssetHandle LoadAsset<TAsset>(string name)
            where TAsset : Object;

        /// <summary>
        /// 异步加载指定名称的资源。
        /// </summary>
        /// <param name="name">资源名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源句柄的异步任务。</returns>
        UniTask<AssetHandle> LoadAssetAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步加载指定名称的指定类型资源。
        /// </summary>
        /// <typeparam name="TAsset">资源类型。</typeparam>
        /// <param name="name">资源名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源句柄的异步任务。</returns>
        UniTask<AssetHandle> LoadAssetAsync<TAsset>(string name, CancellationToken cancellationToken = default)
            where TAsset : Object;

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
        /// 同步加载指定名称的场景。
        /// </summary>
        /// <param name="name">场景名称。</param>
        /// <param name="loadMode">场景加载模式。</param>
        /// <returns>场景句柄。</returns>
        SceneHandle LoadScene(string name, LoadSceneMode loadMode = LoadSceneMode.Single);

        /// <summary>
        /// 异步加载指定名称的场景。
        /// </summary>
        /// <param name="name">场景名称。</param>
        /// <param name="loadMode">场景加载模式。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>场景句柄的异步任务。</returns>
        UniTask<SceneHandle> LoadSceneAsync(string name, LoadSceneMode loadMode = LoadSceneMode.Single, CancellationToken cancellationToken = default);

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
        /// 同步加载指定路径的原始文件。
        /// </summary>
        /// <param name="fullPath">文件完整路径。</param>
        /// <returns>原始文件句柄。</returns>
        RawFileHandle LoadRawFile(string fullPath);

        /// <summary>
        /// 异步加载指定路径的原始文件。
        /// </summary>
        /// <param name="fullPath">文件完整路径。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>原始文件句柄的异步任务。</returns>
        UniTask<RawFileHandle> LoadRawFileAsync(string fullPath, CancellationToken cancellationToken = default);

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
    }
}
