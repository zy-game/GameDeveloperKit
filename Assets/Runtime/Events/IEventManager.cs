using System;

namespace GameDeveloperKit.Events
{
    public interface IEventManager : IModule
    {
        /// <summary>
        /// 订阅事件（按类型自动推断ID）
        /// </summary>
        IDisposable Subscribe<T>(Action<T> handler) where T : GameEventArgs;

        /// <summary>
        /// 订阅事件（指定ID）
        /// </summary>
        IDisposable Subscribe<T>(int id, Action<T> handler) where T : GameEventArgs;

        /// <summary>
        /// 触发事件（下一帧）
        /// </summary>
        void Fire(object sender, GameEventArgs e);

        /// <summary>
        /// 立即触发事件
        /// </summary>
        void FireNow(object sender, GameEventArgs e);
    }
}