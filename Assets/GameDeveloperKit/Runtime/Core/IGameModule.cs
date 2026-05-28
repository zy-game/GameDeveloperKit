using Cysharp.Threading.Tasks;

namespace GameDeveloperKit
{
    /// <summary>
    /// 游戏模块接口，定义模块启动和关闭生命周期。
    /// </summary>
    public interface IGameModule : IReference
    {
        /// <summary>
        /// 启动模块。
        /// </summary>
        /// <returns>模块启动任务。</returns>
        UniTask Startup();

        /// <summary>
        /// 关闭模块。
        /// </summary>
        /// <returns>模块关闭任务。</returns>
        UniTask Shutdown();

        /// <summary>
        /// 释放模块，默认异步调用关闭流程。
        /// </summary>
        void IReference.Release()
        {
            Shutdown().Forget();
        }
    }

    /// <summary>
    /// 模块基类
    /// </summary>
    public abstract class GameModuleBase : IGameModule
    {
        /// <summary>
        /// 模块启动
        /// </summary>
        /// <returns></returns>
        public abstract UniTask Startup();

        /// <summary>
        /// 模块关闭
        /// </summary>
        /// <returns></returns>
        public abstract UniTask Shutdown();
    }
}
