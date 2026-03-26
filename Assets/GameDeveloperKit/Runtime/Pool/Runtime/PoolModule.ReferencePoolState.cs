using System.Collections.Generic;

namespace GameDeveloperKit.Runtime
{
    public sealed partial class PoolModule
    {
        /// <summary>
        /// 表示引用对象池的状态信息。
        /// </summary>
        /// <remarks>
        /// 此类存储引用对象池的内部状态，包括未使用的对象栈和最大容量限制。
        /// 每种类型的对象池都有各自独立的 PoolState 实例。
        /// </remarks>
        private sealed class ReferencePoolState
        {
            /// <summary>
            /// 池中可用的对象栈。
            /// </summary>
            /// <remarks>
            /// 此栈存储当前池中可复用的对象实例。获取对象时从栈顶弹出，
            /// 释放对象时压入栈顶。使用栈结构可以确保最近使用的对象优先被复用。
            /// </remarks>
            public readonly Stack<object> Items = new();

            /// <summary>
            /// 获取或设置池中允许保留的最大对象数量。
            /// </summary>
            /// <remarks>
            /// 当释放对象时，如果池中对象数量达到此限制，多余的对象将被丢弃而不是放回池中。
            /// 这可以防止池过度增长占用过多内存。默认值为 128。
            /// </remarks>
            public int MaxCount = 128;
        }
    }
}
