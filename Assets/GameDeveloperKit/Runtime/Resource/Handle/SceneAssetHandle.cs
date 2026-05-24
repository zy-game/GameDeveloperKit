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
        /// <summary>
        /// 场景资源
        /// </summary>
        public Scene Asset { get; private set; }

        /// <summary>
        /// 场景名
        /// </summary>
        public string SceneName => Info.Location;

        /// <summary>
        /// 激活场景
        /// </summary>
        public void Active()
        {
            if (Asset.isLoaded)
            {
                return;
            }

            SceneManager.SetActiveScene(Asset);
        }

        /// <summary>
        /// 资源释放
        /// </summary>
        public override void Release()
        {
            base.Release();
            Asset = default;
        }

        /// <summary>
        /// 创建场景加载成功的句柄
        /// </summary>
        /// <param name="location">资源信息</param>
        /// <param name="asset">场景资源</param>
        /// <returns></returns>
        public static SceneAssetHandle Success(AssetInfo location, Scene asset)
        {
            return new SceneAssetHandle()
            {
                Info = location,
                Asset = asset,
            };
        }

        /// <summary>
        /// 创建场景资源加载失败句柄
        /// </summary>
        /// <param name="location">资源信息</param>
        /// <param name="error">错误信息</param>
        /// <returns></returns>
        public static SceneAssetHandle Failure(Exception error)
        {
            return new SceneAssetHandle()
            {
                Asset = default,
                Error = error,
                Info = null,
            };
        }
    }
}