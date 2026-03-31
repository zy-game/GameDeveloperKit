using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 资源提供器门面，提供对资源模块的资源加载功能的访问。
    /// </summary>
    public sealed class ResourceProviderFacade : IResourceProvider
    {
        private readonly ResourceModule _module;

        /// <summary>
        /// 初始化资源提供器门面的新实例。
        /// </summary>
        /// <param name="module">资源模块。</param>
        public ResourceProviderFacade(ResourceModule module)
        {
            _module = module;
        }

        /// <summary>
        /// 同步加载资源。
        /// </summary>
        /// <param name="name">资源名称。</param>
        /// <returns>资源句柄。</returns>
        public AssetHandle LoadAsset(string name)
        {
            return _module.LoadAsset(name);
        }

        /// <summary>
        /// 同步加载指定类型的资源。
        /// </summary>
        /// <typeparam name="TAsset">资源类型。</typeparam>
        /// <param name="name">资源名称。</param>
        /// <returns>资源句柄。</returns>
        public AssetHandle LoadAsset<TAsset>(string name)
            where TAsset : Object
        {
            return _module.LoadAsset<TAsset>(name);
        }

        /// <summary>
        /// 异步加载资源。
        /// </summary>
        /// <param name="name">资源名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源句柄。</returns>
        public UniTask<AssetHandle> LoadAssetAsync(string name, CancellationToken cancellationToken = default)
        {
            return _module.LoadAssetAsync(name, cancellationToken);
        }

        /// <summary>
        /// 异步加载指定类型的资源。
        /// </summary>
        /// <typeparam name="TAsset">资源类型。</typeparam>
        /// <param name="name">资源名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源句柄。</returns>
        public UniTask<AssetHandle> LoadAssetAsync<TAsset>(string name, CancellationToken cancellationToken = default)
            where TAsset : Object
        {
            return _module.LoadAssetAsync<TAsset>(name, cancellationToken);
        }

        public AssetHandle LoadByName(string name, string packageName = null)
        {
            return _module.LoadByName(name, packageName);
        }

        public AssetHandle LoadByLabel(string label, string packageName = null)
        {
            return _module.LoadByLabel(label, packageName);
        }

        public AssetHandle LoadByPath(string fullPath, string packageName = null)
        {
            return _module.LoadByPath(fullPath, packageName);
        }

        public IReadOnlyList<AssetHandle> LoadByType<TAsset>(string packageName = null)
            where TAsset : Object
        {
            return _module.LoadByType<TAsset>(packageName);
        }

        /// <summary>
        /// 同步加载指定位置的资源集合。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <returns>资源句柄列表。</returns>
        public IReadOnlyList<AssetHandle> LoadAssets(ResourceLocation location)
        {
            return _module.LoadAssets(location);
        }

        /// <summary>
        /// 异步加载指定位置的资源集合。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>资源句柄列表。</returns>
        public UniTask<IReadOnlyList<AssetHandle>> LoadAssetsAsync(ResourceLocation location, CancellationToken cancellationToken = default)
        {
            return _module.LoadAssetsAsync(location, cancellationToken);
        }

        /// <summary>
        /// 同步加载场景。
        /// </summary>
        /// <param name="name">场景名称。</param>
        /// <param name="loadMode">场景加载模式。</param>
        /// <returns>场景句柄。</returns>
        public SceneHandle LoadScene(string name, LoadSceneMode loadMode = LoadSceneMode.Single)
        {
            return _module.LoadScene(name, loadMode);
        }

        /// <summary>
        /// 异步加载场景。
        /// </summary>
        /// <param name="name">场景名称。</param>
        /// <param name="loadMode">场景加载模式。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>场景句柄。</returns>
        public UniTask<SceneHandle> LoadSceneAsync(string name, LoadSceneMode loadMode = LoadSceneMode.Single, CancellationToken cancellationToken = default)
        {
            return _module.LoadSceneAsync(name, loadMode, cancellationToken);
        }

        /// <summary>
        /// 同步加载指定位置的场景。
        /// </summary>
        /// <param name="location">场景位置。</param>
        /// <param name="loadMode">场景加载模式。</param>
        /// <returns>场景句柄。</returns>
        public SceneHandle LoadScene(ResourceLocation location, LoadSceneMode loadMode = LoadSceneMode.Single)
        {
            return _module.LoadScene(location, loadMode);
        }

        /// <summary>
        /// 异步加载指定位置的场景。
        /// </summary>
        /// <param name="location">场景位置。</param>
        /// <param name="loadMode">场景加载模式。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>场景句柄。</returns>
        public UniTask<SceneHandle> LoadSceneAsync(ResourceLocation location, LoadSceneMode loadMode = LoadSceneMode.Single, CancellationToken cancellationToken = default)
        {
            return _module.LoadSceneAsync(location, loadMode, cancellationToken);
        }

        /// <summary>
        /// 同步加载原始文件。
        /// </summary>
        /// <param name="fullPath">文件完整路径。</param>
        /// <returns>原始文件句柄。</returns>
        public RawFileHandle LoadRawFile(string fullPath)
        {
            return _module.LoadRawFile(fullPath);
        }

        /// <summary>
        /// 异步加载原始文件。
        /// </summary>
        /// <param name="fullPath">文件完整路径。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>原始文件句柄。</returns>
        public UniTask<RawFileHandle> LoadRawFileAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            return _module.LoadRawFileAsync(fullPath, cancellationToken);
        }

        /// <summary>
        /// 同步加载指定位置的原始文件。
        /// </summary>
        /// <param name="location">文件位置。</param>
        /// <returns>原始文件句柄。</returns>
        public RawFileHandle LoadRawFile(ResourceLocation location)
        {
            return _module.LoadRawFile(location);
        }

        /// <summary>
        /// 异步加载指定位置的原始文件。
        /// </summary>
        /// <param name="location">文件位置。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>原始文件句柄。</returns>
        public UniTask<RawFileHandle> LoadRawFileAsync(ResourceLocation location, CancellationToken cancellationToken = default)
        {
            return _module.LoadRawFileAsync(location, cancellationToken);
        }

        /// <summary>
        /// 查找指定位置的资源。
        /// </summary>
        /// <param name="location">资源位置。</param>
        /// <param name="kind">资源类型筛选。</param>
        /// <returns>资源条目列表。</returns>
        public IReadOnlyList<ResourceEntry> Find(ResourceLocation location, ResourceEntryKind? kind = null)
        {
            return _module.Find(location, kind);
        }
    }
}
