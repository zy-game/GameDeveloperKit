using System;

namespace GameDeveloperKit.Events
{
    /// <summary>
    /// 游戏事件参数的抽象基类
    /// </summary>
    public abstract class GameEventArgs : EventArgs, IReference
    {
        /// <summary>
        /// 获取事件ID
        /// </summary>
        public abstract int Id { get; }

        /// <summary>
        /// 获取或设置事件发送者
        /// </summary>
        public object Sender { get; set; }

        /// <summary>
        /// 清理引用
        /// </summary>
        public virtual void OnClearup()
        {
        }
    }
}