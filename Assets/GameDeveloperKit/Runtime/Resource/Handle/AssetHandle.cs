using System;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源句柄
    /// </summary>
    public class AssetHandle : ResourceHandle
    {
        /// <summary>
        /// 加载到的Unity资源对象。
        /// </summary>
        public UnityEngine.Object Asset { get; protected set; }

        /// <summary>
        /// 获取资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <returns>资源对象</returns>
        public T GetAsset<T>() where T : UnityEngine.Object
        {
            return Asset as T;
        }

        /// <summary>
        /// 释放资源句柄，并清理Unity资源引用。
        /// </summary>
        public override void Release()
        {
            Asset = null;
            base.Release();
        }

        /// <summary>
        /// 创建资源加载成功句柄。
        /// </summary>
        /// <param name="location">资源信息。</param>
        /// <param name="asset">Unity资源对象。</param>
        /// <returns>资源句柄。</returns>
        public static AssetHandle Success(AssetInfo location, UnityEngine.Object asset)
        {
            return new AssetHandle()
            {
                Info = location,
                Asset = asset,
                Error =  null,
                Status = ResourceStatus.Succeeded,
            };
        }

        /// <summary>
        /// 创建资源加载失败句柄。
        /// </summary>
        /// <param name="error">错误信息。</param>
        /// <returns>资源句柄。</returns>
        public static AssetHandle Failure(Exception error)
        {
            return new AssetHandle()
            {
                Info = null,
                Error = error,
                Asset = null,
                Status = ResourceStatus.Failed,
            };
        }
    }
}