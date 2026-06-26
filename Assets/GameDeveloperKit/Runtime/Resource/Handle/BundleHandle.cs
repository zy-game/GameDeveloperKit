using System;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源包句柄，用于保存已加载的AssetBundle和对应资源包信息。
    /// </summary>
    public class BundleHandle : ResourceHandle<BundleInfo>
    {
        /// <summary>
        /// 加载到的AssetBundle实例。
        /// </summary>
        public AssetBundle Asset { get; private set; }

        /// <summary>
        /// 释放资源包句柄，并卸载AssetBundle。
        /// </summary>
        public override void Release()
        {
            if (Status is ResourceStatus.Released)
            {
                return;
            }

            if (ReleaseReference() > 0)
            {
                return;
            }

            var bundle = Asset;
            Asset = null;
            base.ReleaseCore();
            bundle?.Unload(true);
        }

        /// <summary>
        /// 创建资源包加载成功句柄。
        /// </summary>
        /// <param name="info">资源包信息。</param>
        /// <param name="bundle">AssetBundle实例。</param>
        /// <returns>资源包句柄。</returns>
        public static BundleHandle Success(BundleInfo info, AssetBundle bundle)
        {
            return new BundleHandle()
            {
                Asset = bundle,
                Error = null,
                Info = info,
                Status = ResourceStatus.Succeeded,
            };
        }

        /// <summary>
        /// 创建资源包加载失败句柄。
        /// </summary>
        /// <param name="info">资源包信息。</param>
        /// <param name="exception">错误信息。</param>
        /// <returns>资源包句柄。</returns>
        public static BundleHandle Failure(BundleInfo info, Exception exception)
        {
            return new BundleHandle()
            {
                Asset = null,
                Error = exception,
                Info = info,
                Status = ResourceStatus.Failed,
            };
        }
    }
}
