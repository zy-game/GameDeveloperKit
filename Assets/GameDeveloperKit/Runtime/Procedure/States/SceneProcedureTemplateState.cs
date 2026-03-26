using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 场景流程模板状态，用于需要加载场景的流程状态
    /// </summary>
    public abstract class SceneProcedureTemplateState : ProcedureStateBase
    {
        /// <summary>
        /// 初始化场景流程模板状态
        /// </summary>
        /// <param name="name">状态名称</param>
        /// <param name="sceneName">场景名称</param>
        /// <param name="packageName">资源包名称</param>
        /// <param name="rememberScene">是否记住场景</param>
        protected SceneProcedureTemplateState(string name, string sceneName = null, string packageName = null, bool rememberScene = true)
            : base(name)
        {
            SceneName = sceneName;
            PackageName = packageName;
            RememberScene = rememberScene;
        }

        /// <summary>
        /// 获取场景名称
        /// </summary>
        public string SceneName { get; }

        /// <summary>
        /// 获取资源包名称
        /// </summary>
        public string PackageName { get; }

        /// <summary>
        /// 获取是否记住场景
        /// </summary>
        public bool RememberScene { get; }

        /// <summary>
        /// 确保场景已准备就绪，如果未加载则加载场景
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        protected async UniTask EnsureSceneReadyAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(SceneName) || !Game.HasModule<SceneModule>())
            {
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid()
                && (string.Equals(activeScene.name, SceneName, StringComparison.Ordinal)
                    || string.Equals(activeScene.path, SceneName, StringComparison.Ordinal)))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(PackageName))
            {
                await Game.Scene.SwitchAsync(SceneName, RememberScene, cancellationToken);
                return;
            }

            await Game.Scene.SwitchFromResourceAsync(SceneName, PackageName, RememberScene, cancellationToken);
        }
    }
}
