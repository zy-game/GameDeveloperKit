using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 场景句柄，用于管理已加载的场景。
    /// </summary>
    public sealed class SceneHandle
    {
        private readonly string _scenePath;
        private bool _released;

        /// <summary>
        /// 初始化场景句柄的新实例。
        /// </summary>
        /// <param name="packageName">包名称。</param>
        /// <param name="location">资源位置。</param>
        /// <param name="scene">场景对象。</param>
        /// <param name="scenePath">场景路径。</param>
        internal SceneHandle(string packageName, ResourceLocation location, Scene scene, string scenePath)
        {
            PackageName = packageName;
            Location = location?.Clone() ?? new ResourceLocation();
            Scene = scene;
            _scenePath = scenePath;
        }

        /// <summary>
        /// 获取包名称。
        /// </summary>
        public string PackageName { get; }

        /// <summary>
        /// 获取资源位置。
        /// </summary>
        public ResourceLocation Location { get; }

        /// <summary>
        /// 获取场景对象。
        /// </summary>
        public Scene Scene { get; }

        /// <summary>
        /// 异步卸载场景。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的UniTask。</returns>
        public async UniTask UnloadAsync(CancellationToken cancellationToken = default)
        {
            if (_released)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (Scene.IsValid() && Scene.isLoaded)
            {
                await SceneManager.UnloadSceneAsync(Scene);
            }
            else if (!string.IsNullOrWhiteSpace(_scenePath))
            {
                await SceneManager.UnloadSceneAsync(_scenePath);
            }

            _released = true;
        }
    }
}
