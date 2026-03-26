using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 启动流程模板状态，用于游戏启动时的初始化流程
    /// </summary>
    public sealed class StartupProcedureTemplateState : ProcedureStateBase
    {
        /// <summary>
        /// 初始化启动流程模板状态
        /// </summary>
        /// <param name="name">状态名称</param>
        /// <param name="nextStateName">下一个状态名称</param>
        /// <param name="showLoadingOverlay">是否显示加载遮罩</param>
        /// <param name="loadingMessage">加载消息</param>
        public StartupProcedureTemplateState(string name = "Startup", string nextStateName = "Lobby", bool showLoadingOverlay = true, string loadingMessage = "Starting...")
            : base(name)
        {
            NextStateName = nextStateName;
            ShowLoadingOverlay = showLoadingOverlay;
            LoadingMessage = loadingMessage;
        }

        /// <summary>
        /// 获取下一个状态名称
        /// </summary>
        public string NextStateName { get; }

        /// <summary>
        /// 获取是否显示加载遮罩
        /// </summary>
        public bool ShowLoadingOverlay { get; }

        /// <summary>
        /// 获取加载消息
        /// </summary>
        public string LoadingMessage { get; }

        /// <summary>
        /// 状态进入时的异步处理
        /// </summary>
        /// <param name="userData">用户数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public override UniTask OnEnterAsync(object userData = null, CancellationToken cancellationToken = default)
        {
            if (ShowLoadingOverlay && Game.HasModule<UIModule>())
            {
                Game.UI.ShowLoading(LoadingMessage);
            }

            if (!string.IsNullOrWhiteSpace(NextStateName))
            {
                ScheduleNextState(userData);
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 状态退出时的异步处理
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public override UniTask OnExitAsync(CancellationToken cancellationToken = default)
        {
            if (ShowLoadingOverlay && Game.HasModule<UIModule>())
            {
                Game.UI.HideLoading();
            }

            return UniTask.CompletedTask;
        }

        private void ScheduleNextState(object userData)
        {
            if (Game.HasModule<SchedulerModule>())
            {
                Game.Scheduler.PostForProcedure(() => ChangeNextStateAsync(userData).ForgetWithDiagnostics("Procedure.StartupTemplateChangeStateFailed", NextStateName, nameof(StartupProcedureTemplateState)));
                return;
            }

            ChangeNextStateAsync(userData).ForgetWithDiagnostics("Procedure.StartupTemplateChangeStateFailed", NextStateName, nameof(StartupProcedureTemplateState));
        }

        private async UniTask ChangeNextStateAsync(object userData)
        {
            try
            {
                await UniTask.Yield();
                if (!Game.HasModule<ProcedureModule>() || !Game.Procedure.HasState(NextStateName))
                {
                    return;
                }

                await Game.Procedure.ChangeStateFromStartupAsync(NextStateName, Name, userData);
            }
            catch (Exception exception)
            {
                if (Game.HasModule<DiagnosticsModule>())
                {
                    Game.Diagnostics.LogError($"Startup template failed to change state to '{NextStateName}': {exception.Message}", nameof(StartupProcedureTemplateState));
                    return;
                }

                Debug.LogException(exception);
            }
        }
    }
}
