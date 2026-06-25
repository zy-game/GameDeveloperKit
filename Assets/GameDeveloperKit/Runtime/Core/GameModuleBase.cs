using GameDeveloperKit.Debugger;

namespace GameDeveloperKit
{
    /// <summary>
    /// 模块基类
    /// </summary>
    public abstract class GameModuleBase : IGameModule
    {
        public abstract void Startup();

        public abstract void Shutdown();

        /// <summary>
        /// 当 DebugModule 已注册时注册模块 Profile。
        /// </summary>
        protected void TryRegisterDebugProfile(ProfileHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            if (App.TryGetRegistered<DebugModule>(out var debug))
            {
                debug.RegisterProfile(handle);
            }
        }

        /// <summary>
        /// 当 DebugModule 已注册时注销模块 Profile。
        /// </summary>
        protected void TryUnregisterDebugProfile(ProfileHandle handle)
        {
            if (handle == null)
            {
                return;
            }

            if (App.TryGetRegistered<DebugModule>(out var debug))
            {
                debug.UnregisterProfile(handle);
            }
        }
    }
}
