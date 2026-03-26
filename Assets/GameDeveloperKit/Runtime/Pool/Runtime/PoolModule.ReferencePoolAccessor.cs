namespace GameDeveloperKit.Runtime
{
    public sealed partial class PoolModule
    {
        /// <summary>
        /// 提供对引用对象池的访问接口。
        /// </summary>
        /// <remarks>
        /// 此类是 PoolModule 的内部辅助类，用于提供类型安全的对象池访问方法。
        /// 所有操作都委托给关联的 PoolModule 实例。
        /// </remarks>
        public sealed class ReferencePoolAccessor
        {
            private readonly PoolModule _module;

            /// <summary>
            /// 初始化 ReferencePoolAccessor 的新实例。
            /// </summary>
            /// <param name="module">关联的 PoolModule 实例。</param>
            internal ReferencePoolAccessor(PoolModule module)
            {
                _module = module;
            }

            /// <summary>
            /// 从对象池中获取一个指定类型的实例。
            /// </summary>
            /// <typeparam name="T">要获取的对象类型，必须为引用类型且有无参构造函数。</typeparam>
            /// <returns>从对象池中获取的实例。如果池中没有可用对象，则创建新实例。</returns>
            /// <remarks>
            /// 此方法首先尝试从池中获取已存在的实例。如果池为空，则创建新实例并返回。
            /// 获取到的对象会被激活并准备好使用。
            /// </remarks>
            public T Acquire<T>()
                where T : class, new()
            {
                return _module.Acquire<T>();
            }

            /// <summary>
            /// 将指定的实例释放回对象池。
            /// </summary>
            /// <typeparam name="T">实例的对象类型，必须为引用类型。</typeparam>
            /// <param name="instance">要释放回池中的实例。</param>
            /// <remarks>
            /// 如果实例实现了 IReferencePoolable 接口，会调用其 ResetForPool 方法进行重置。
            /// 重置后的实例会被放回池中，供后续重复使用。
            /// </remarks>
            public void Release<T>(T instance)
                where T : class
            {
                _module.Release(instance);
            }

            /// <summary>
            /// 预热对象池，提前创建指定数量的实例。
            /// </summary>
            /// <typeparam name="T">要预热池的对象类型，必须为引用类型且有无参构造函数。</typeparam>
            /// <param name="count">要创建的实例数量。</param>
            /// <remarks>
            /// 此方法会在池中提前创建指定数量的对象，以便后续获取时无需等待实例创建。
            /// 如果池中已有足够数量的对象，则不会创建新实例。
            /// </remarks>
            public void Warmup<T>(int count)
                where T : class, new()
            {
                _module.WarmupReferencePool<T>(count);
            }

            /// <summary>
            /// 清空指定类型的对象池。
            /// </summary>
            /// <typeparam name="T">要清空池的对象类型，必须为引用类型。</typeparam>
            /// <remarks>
            /// 此方法会清除池中所有指定类型的实例。被清除的实例将不再可用。
            /// 如果这些实例实现了 IDisposable 接口，应该在被清除前释放资源。
            /// </remarks>
            public void Clear<T>()
                where T : class
            {
                _module.ClearReferencePool<T>();
            }
        }
    }
}
