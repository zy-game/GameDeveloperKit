namespace GameDeveloperKit.Runtime
{
    /// <summary>
    /// 定义对象池中的 GameObject 需要实现的接口。
    /// </summary>
    /// <remarks>
    /// 实现此接口的 GameObject 在从对象池中生成或释放时会收到通知，
    /// 可以在这些时机执行初始化或清理操作。
    /// </remarks>
    public interface IGameObjectPoolable
    {
        /// <summary>
        /// 在 GameObject 从对象池中生成时调用。
        /// </summary>
        /// <remarks>
        /// 此方法在对象被激活并返回给调用者之前调用，可以用于初始化对象状态，
        /// 设置默认值，或执行其他生成时的逻辑。
        /// </remarks>
        void OnSpawnedFromPool();

        /// <summary>
        /// 在 GameObject 被释放回对象池时调用。
        /// </summary>
        /// <remarks>
        /// 此方法在对象被停用并放回池中之前调用，可以用于清理状态、
        /// 停止协程、取消操作或执行其他释放时的逻辑。
        /// </remarks>
        void OnDespawnedToPool();
    }
}
