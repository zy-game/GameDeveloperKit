namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 定义事件绑定注册器的契约。
    /// </summary>
    public interface IEventBindingProvider
    {
        /// <summary>
        /// 向事件模块注册事件绑定。
        /// </summary>
        /// <param name="module">要注册绑定的事件模块。</param>
        void Register(EventModule module);
    }
}
