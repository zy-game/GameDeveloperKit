using System;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Resource
{
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    /// <summary>
    /// 资源包管理器接口
    /// </summary>
    public interface IPackageManager : IReference
    {
        /// <summary>
        /// 包名
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 包版本
        /// </summary>
        string Version { get; }

        /// <summary>
        /// 异步加载资源包
        /// </summary>
        /// <returns></returns>
        UniTask<bool> Initialization(IResourceManager resourceManager);

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <param name="assetName"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        UniTask<AssetHandle<T>> LoadAssetAsync<T>(string assetName) where T : Object;

        /// <summary>
        /// 异步加载场景
        /// </summary>
        /// <param name="sceneName"></param>
        /// <param name="mode"></param>
        /// <param name="progressHandler"></param>
        /// <returns></returns>
        UniTask<SceneHandle> LoadSceneAsync(string sceneName, LoadSceneMode mode, Action<float> progressHandler = null);

        /// <summary>
        /// 卸载单个资源
        /// </summary>
        /// <param name="handle"></param>
        void Unload(BaseHandle handle);

        /// <summary>
        /// 卸载未使用的资源
        /// </summary>
        void UnloadUnusedAssets();

        /// <summary>
        /// 是否包含某个资源
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        bool Contains(string address);
    }
}