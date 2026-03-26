using System;
using System.Threading;

namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 定义事件上下文的访问契约。
    /// </summary>
    public interface IEventContext
    {
        /// <summary>
        /// 获取事件发送方。
        /// </summary>
        object Sender { get; }

        /// <summary>
        /// 获取事件键。
        /// </summary>
        object EventKey { get; }

        /// <summary>
        /// 获取事件名称。
        /// </summary>
        string EventName { get; }

        /// <summary>
        /// 获取事件参数列表。
        /// </summary>
        object[] Arguments { get; }

        /// <summary>
        /// 获取事件处理使用的取消令牌。
        /// </summary>
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// 获取或设置事件是否已被处理。
        /// </summary>
        bool Handled { get; set; }

        /// <summary>
        /// 获取强类型事件键。
        /// </summary>
        /// <typeparam name="TKey">事件键类型。</typeparam>
        /// <returns>转换后的事件键。</returns>
        TKey GetEventKey<TKey>();

        /// <summary>
        /// 获取指定索引位置的强类型事件参数。
        /// </summary>
        /// <typeparam name="TArg">参数类型。</typeparam>
        /// <param name="index">参数索引。</param>
        /// <returns>指定索引处的参数值。</returns>
        TArg GetArgument<TArg>(int index);

        /// <summary>
        /// 尝试获取指定索引位置的强类型事件参数。
        /// </summary>
        /// <typeparam name="TArg">参数类型。</typeparam>
        /// <param name="index">参数索引。</param>
        /// <param name="value">输出的参数值。</param>
        /// <returns>如果获取成功则返回 true，否则返回 false。</returns>
        bool TryGetArgument<TArg>(int index, out TArg value);

        /// <summary>
        /// 设置上下文扩展数据。
        /// </summary>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <param name="key">数据键。</param>
        /// <param name="value">要设置的数据值。</param>
        void Set<T>(string key, T value);

        /// <summary>
        /// 尝试获取上下文扩展数据。
        /// </summary>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <param name="key">数据键。</param>
        /// <param name="value">输出的数据值。</param>
        /// <returns>如果获取成功则返回 true，否则返回 false。</returns>
        bool TryGet<T>(string key, out T value);
    }
}
