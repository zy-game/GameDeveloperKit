using Cysharp.Threading.Tasks;

namespace GameDeveloperKit
{
    public interface IGameModule : IReference
    {
        UniTask Startup();

        UniTask Shutdown();

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