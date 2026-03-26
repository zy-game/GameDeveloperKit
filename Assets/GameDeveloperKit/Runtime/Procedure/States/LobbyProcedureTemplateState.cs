using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 大厅流程模板状态，用于游戏大厅场景
    /// </summary>
    public sealed class LobbyProcedureTemplateState : SceneProcedureTemplateState
    {
        /// <summary>
        /// 初始化大厅流程模板状态
        /// </summary>
        /// <param name="name">状态名称</param>
        /// <param name="sceneName">场景名称</param>
        /// <param name="packageName">资源包名称</param>
        /// <param name="rememberScene">是否记住场景</param>
        public LobbyProcedureTemplateState(string name = "Lobby", string sceneName = null, string packageName = null, bool rememberScene = true)
            : base(name, sceneName, packageName, rememberScene)
        {
        }

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
    }
}
