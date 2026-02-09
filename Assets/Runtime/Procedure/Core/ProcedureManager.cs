using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Log;

namespace GameDeveloperKit.Procedure
{

    /// <summary>
    /// 流程模块，负责管理全局游戏流程
    /// </summary>
    public class ProcedureManager : IModule, IProcedureManager
    {
        private IStateManager _procedureManager;

        /// <summary>
        /// 获取当前流程
        /// </summary>
        public StateBase CurrentProcedure => _procedureManager?.CurrentProcedure;

        public void OnStartup()
        {
            _procedureManager = new StateManager();
            
            // 注册调试面板
            if (Game.Debug is LoggerModule loggerModule)
            {
                loggerModule.RegisterPanel(new ProcedureDebugPanel());
            }
        }

        /// <summary>
        /// 启动流程链（指定入口流程）
        /// </summary>
        public UniTask StartAsync<T>(CancellationToken cancellationToken = default, params object[] args) where T : StateBase
        {
            return _procedureManager.StartAsync<T>(cancellationToken, args);
        }

        /// <summary>
        /// 等待到达特定流程
        /// </summary>
        public UniTask WaitForAsync<T>() where T : StateBase
        {
            return _procedureManager.WaitForAsync<T>();
        }

        public void OnUpdate(float elapseSeconds)
        {
            // 不再需要Update轮询
        }

        public void OnClearup()
        {
            _procedureManager?.Shutdown();
            _procedureManager = null;
        }
    }
}