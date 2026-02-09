using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Resource
{
    public interface IResourceManager : IModule
    {
        /// <summary>
        /// 设置资源模式
        /// </summary>
        /// <param name="mode">资源模式</param>
        void SetMode(EResourceMode mode);

        /// <summary>
        /// 获取资源模式
        /// </summary>
        EResourceMode GetMode();

        /// <summary>
        /// 设置资源服务器地址
        /// </summary>
        /// <param name="url">资源服务器基础 URL</param>
        UniTask<bool> SetResourceServerUrl(string url);

        /// <summary>
        /// 获取资源服务器地址
        /// </summary>
        string GetResourceServerUrl();

        /// <summary>
        /// 异步加载一个资源包
        /// </summary>
        /// <param name="packageName">要加载的包名</param>
        /// <param name="version">资源包版本</param>
        /// <returns>加载的包的包管理器</returns>
        UniTask<IPackageManager> LoadPackageAsync(string packageName, string version = "");

        /// <summary>
        /// 卸载一个资源包
        /// </summary>
        /// <param name="packageName">要卸载的包名</param>
        void UnloadPackage(string packageName);

        /// <summary>
        /// 异步加载一个资源
        /// </summary>
        /// <typeparam name="T">资源的类型</typeparam>
        /// <param name="address">资源的地址</param>
        /// <returns>加载的资源的句柄</returns>
        UniTask<AssetHandle<T>> LoadAssetAsync<T>(string address) where T : UnityEngine.Object;

        /// <summary>
        /// 异步加载一个场景
        /// </summary>
        /// <param name="name">场景的名称</param>
        /// <param name="mode"></param>
        /// <param name="progressHandle"></param>
        /// <returns>加载的场景的句柄</returns>
        UniTask<SceneHandle> LoadSceneAsync(string name, LoadSceneMode mode = LoadSceneMode.Additive, Action<float> progressHandle = default);

        /// <summary>
        /// 卸载一个资源
        /// </summary>
        /// <param name="handle">要卸载的资源的句柄</param>
        void Unload(BaseHandle handle);

        /// <summary>
        /// 卸载所有未使用的资源
        /// </summary>
        void UnloadUnusedAssets();

        /// <summary>
        /// 设置自动卸载未使用资源的间隔时间
        /// </summary>
        /// <param name="minutes">间隔分钟数（设置为 0 禁用自动卸载）</param>
        void SetAutoUnloadInterval(float minutes);

        /// <summary>
        /// 通过 Label 批量加载资源
        /// </summary>
        UniTask<List<AssetHandle<T>>> LoadAssetsByLabelAsync<T>(string label)
            where T : UnityEngine.Object;

        /// <summary>
        /// 通过多个 Label 批量加载资源（资源必须同时包含所有指定 Label）
        /// </summary>
        UniTask<List<AssetHandle<T>>> LoadAssetsByLabelsAsync<T>(params string[] labels)
            where T : UnityEngine.Object;

        /// <summary>
        /// 加载 Bundle 中的所有资源
        /// </summary>
        UniTask<List<AssetHandle<T>>> LoadAllAssetsInBundleAsync<T>(string bundleName)
            where T : UnityEngine.Object;

        /// <summary>
        /// 加载子资源（如 Atlas 中的 Sprite）
        /// </summary>
        UniTask<AssetHandle<T>> LoadSubAssetAsync<T>(string address, string subAssetName)
            where T : UnityEngine.Object;

        /// <summary>
        /// 通过类型批量加载资源（支持继承类型匹配）
        /// </summary>
        UniTask<List<AssetHandle<T>>> LoadAssetsByTypeAsync<T>()
            where T : UnityEngine.Object;
    }
}