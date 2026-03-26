using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 战斗流程模板状态，用于游戏战斗场景
    /// </summary>
    public sealed class BattleProcedureTemplateState : SceneProcedureTemplateState
    {
        /// <summary>
        /// 初始化战斗流程模板状态
        /// </summary>
        /// <param name="name">状态名称</param>
        /// <param name="sceneName">场景名称</param>
        /// <param name="packageName">资源包名称</param>
        /// <param name="completeStateName">完成后的状态名称</param>
        /// <param name="rememberScene">是否记住场景</param>
        public BattleProcedureTemplateState(string name = "Battle", string sceneName = null, string packageName = null, string completeStateName = "Lobby", bool rememberScene = true)
            : base(name, sceneName, packageName, rememberScene)
        {
            CompleteStateName = completeStateName;
        }

        /// <summary>
        /// 获取完成后的状态名称
        /// </summary>
        public string CompleteStateName { get; }

        /// <summary>
        /// 状态进入时的异步处理
        /// </summary>
        /// <param name="userData">用户数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public override async UniTask OnEnterAsync(object userData = null, CancellationToken cancellationToken = default)
        {
            await EnsureSceneReadyAsync(cancellationToken);
        }

        /// <summary>
        /// 完成战斗并切换到完成状态
        /// </summary>
        /// <param name="userData">用户数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public UniTask CompleteAsync(object userData = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(CompleteStateName) || !Game.HasModule<ProcedureModule>())
            {
                return UniTask.CompletedTask;
            }

            return Game.Procedure.ChangeStateFromSceneAsync(CompleteStateName, SceneName, userData, cancellationToken);
        }
    }
}
