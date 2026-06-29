using System;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源句柄
    /// </summary>
    public class AssetHandle : ResourceHandle
    {
        private BundleHandle m_Bundle;

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
        /// 执行最终资源释放，并清理Unity资源引用。
        /// </summary>
        protected override void ReleaseCore()
        {
            var bundle = m_Bundle;
            m_Bundle = null;
            Asset = null;
            bundle?.Release();
            base.ReleaseCore();
        }

        /// <summary>
        /// 创建资源加载成功句柄。
        /// </summary>
        /// <param name="location">资源信息。</param>
        /// <param name="asset">Unity资源对象。</param>
        /// <returns>资源句柄。</returns>
        public static AssetHandle Success(AssetInfo location, UnityEngine.Object asset, BundleHandle bundle = null)
        {
            bundle?.Retain();
            return new AssetHandle()
            {
                Info = location,
                Asset = asset,
                m_Bundle = bundle,
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
