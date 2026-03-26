namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 定义对象池中的引用类型对象需要实现的接口。
    /// </summary>
    /// <remarks>
    /// 实现此接口的对象在被释放回对象池时，会调用 ResetForPool 方法进行重置，
    /// 以便后续重复使用时保持干净的状态。
    /// </remarks>
    public interface IReferencePoolable
    {
        /// <summary>
        /// 在对象被释放回对象池之前调用，用于重置对象状态。
        /// </summary>
        /// <remarks>
        /// 此方法应在对象准备被放回池中时调用，用于清除对象的内部状态，
        /// 确保下次使用时不会受到上一次使用的影响。
        /// </remarks>
        void ResetForPool();
    }
}
