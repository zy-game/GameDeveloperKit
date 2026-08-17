using System;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 资源包句柄，用于保存已加载的AssetBundle和对应资源包信息。
    /// </summary>
    public class BundleHandle : ResourceHandle<BundleInfo>
    {
        private IDisposable m_LoadSource;
        private IWebAssetBundleStrategy m_WebStrategy;

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
            var loadSource = m_LoadSource;
            var webStrategy = m_WebStrategy;
            Asset = null;
            m_LoadSource = null;
            m_WebStrategy = null;
            base.ReleaseCore();
            try
            {
                if (webStrategy == null)
                {
                    bundle?.Unload(true);
                }
                else
                {
                    webStrategy.UnloadAssetBundle(bundle, true);
                }
            }
            finally
            {
                loadSource?.Dispose();
            }
        }

        /// <summary>
        /// 创建资源包加载成功句柄。
        /// </summary>
        /// <param name="info">资源包信息。</param>
        /// <param name="bundle">AssetBundle实例。</param>
        /// <returns>资源包句柄。</returns>
        public static BundleHandle Success(BundleInfo info, AssetBundle bundle)
        {
            return Success(info, bundle, null);
        }

        internal static BundleHandle Success(
            BundleInfo info,
            AssetBundle bundle,
            IDisposable loadSource,
            IWebAssetBundleStrategy webStrategy = null)
        {
            return new BundleHandle()
            {
                Asset = bundle,
                m_LoadSource = loadSource,
                m_WebStrategy = webStrategy,
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
