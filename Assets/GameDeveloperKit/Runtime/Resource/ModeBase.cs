using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 模式抽象类
    /// </summary>
    public abstract class ModeBase : IReference
    {
        /// <summary>
        /// 资源清单
        /// </summary>
        public ManifestInfo Manifest { get; }

        public ModeBase(ManifestInfo manifest)
        {
            this.Manifest = manifest;
        }

        /// <summary>
        /// 检查是否存在资源
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public abstract bool HasAsset(string location);

        /// <summary>
        /// 检查是否存在资源包
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        public abstract bool HasPackage(string package);

        /// <summary>
        /// 初始化资源包
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        public abstract UniTask<OperationHandle> InitializePackageAsync(string package);

        /// <summary>
        /// 卸载资源包
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        public abstract UniTask<OperationHandle> UninitializePackageAsync(string package);

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public abstract UniTask<AssetHandle> LoadAssetAsync(string location);

        /// <summary>
        /// 基于资源标签加载资源
        /// </summary>
        /// <param name="label"></param>
        /// <returns></returns>
        public abstract UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByLabelAsync(string label);

        /// <summary>
        /// 基于资源类型加载资源
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public abstract UniTask<IReadOnlyList<AssetHandle>> LoadAssetsByTypeAsync<T>() where T : UnityEngine.Object;

        /// <summary>
        /// 加载二进制资源
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public abstract UniTask<RawAssetHandle> LoadRawAssetAsync(string location);

        /// <summary>
        /// 基于资源标签加载二进制资源
        /// </summary>
        /// <param name="label"></param>
        /// <returns></returns>
        public abstract UniTask<IReadOnlyList<RawAssetHandle>> LoadRawAssetsByLabelAsync(string label);

        /// <summary>
        /// 加载场景资源
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public abstract UniTask<SceneAssetHandle> LoadSceneAssetAsync(string name);

        /// <summary>
        /// 卸载未使用的资源
        /// </summary>
        /// <returns></returns>
        public abstract UniTask UnloadUnusedAssetAsync();

        /// <summary>
        /// 卸载资源句柄
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public abstract UniTask UnloadAsset(AssetHandle handle);

        /// <summary>
        /// 卸载资源模式
        /// </summary>
        public abstract void Release();
    }
}
