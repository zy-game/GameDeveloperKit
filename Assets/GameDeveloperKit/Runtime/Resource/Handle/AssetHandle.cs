using System;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源句柄
    /// </summary>
    public class AssetHandle : ResourceHandle
    {
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

        public override void Release()
        {
            Asset = null;
            base.Release();
        }

        public static AssetHandle Success(AssetInfo location, UnityEngine.Object asset)
        {
            return new AssetHandle()
            {
                Info = location,
                Asset = asset,
            };
        }

        public static AssetHandle Failure(Exception error)
        {
            return new AssetHandle()
            {
                Info = null,
                Error = error,
                Asset = null
            };
        }
    }
}