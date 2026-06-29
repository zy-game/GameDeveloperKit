using System;

namespace GameDeveloperKit.Event
{
    /// <summary>
    /// 事件绑定生成入口，用于调用源生成器生成的事件注册代码。
    /// </summary>
    internal static partial class BindingGenerated
    {
        /// <summary>
        /// 向事件模块注册所有生成的事件绑定。
        /// </summary>
        /// <param name="module">事件模块。</param>
        /// <exception cref="ArgumentNullException">事件模块为空时抛出。</exception>
        internal static void RegisterAll(EventModule module)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            RegisterAllGenerated(module);
        }

        /// <summary>
        /// 源生成器实现的事件绑定注册入口。
        /// </summary>
        /// <param name="module">事件模块。</param>
        static partial void RegisterAllGenerated(EventModule module);
    }
}
