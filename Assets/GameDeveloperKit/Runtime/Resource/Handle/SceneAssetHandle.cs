using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 场景资源句柄
    /// </summary>
    public sealed class SceneAssetHandle : ResourceHandle
    {
        private BundleHandle m_Bundle;

        /// <summary>
        /// 场景资源
        /// </summary>
        public Scene Asset { get; private set; }

        /// <summary>
        /// 场景名
        /// </summary>
        public string SceneName => Info?.Location ?? Asset.name;

        /// <summary>
        /// 激活场景
        /// </summary>
        public void Active()
        {
            if (!Asset.isLoaded)
            {
                throw new GameException($"Scene is not loaded: {SceneName}");
            }

            if (SceneManager.SetActiveScene(Asset) is false)
            {
                throw new GameException($"Set active scene failed: {SceneName}");
            }
        }

        /// <summary>
        /// 执行最终资源释放。
        /// </summary>
        protected override void ReleaseCore()
        {
            var bundle = m_Bundle;
            m_Bundle = null;
            Asset = default;
            bundle?.Release();
            base.ReleaseCore();
        }

        /// <summary>
        /// 创建场景加载成功的句柄
        /// </summary>
        /// <param name="location">资源信息</param>
        /// <param name="asset">场景资源</param>
        public static SceneAssetHandle Success(AssetInfo location, Scene asset, BundleHandle bundle = null)
        {
            bundle?.Retain();
            return new SceneAssetHandle()
            {
                Info = location,
                Asset = asset,
                m_Bundle = bundle,
                Error = null,
                Status = ResourceStatus.Succeeded
            };
        }

        /// <summary>
        /// 创建场景资源加载失败句柄
        /// </summary>
        /// <param name="error">错误信息</param>
        public static SceneAssetHandle Failure(Exception error)
        {
            return new SceneAssetHandle()
            {
                Asset = default,
                Error = error,
                Info = null,
                Status = ResourceStatus.Failed
            };
        }
    }
}
