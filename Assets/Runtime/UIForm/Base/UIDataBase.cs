namespace GameDeveloperKit.UI
{
    /// <summary>
    /// UI 数据基类
    /// </summary>
    public abstract class UIDataBase : IReference
    {
        /// <summary>
        /// 初始化时调用（在 UIForm.OnStartup 之前）
        /// </summary>
        public virtual void OnStartup()
        {
        }

        /// <summary>
        /// 清理时调用（实现 IReference 接口，ReferencePool.Release 时自动调用）
        /// </summary>
        public virtual void OnClearup()
        {
        }
    }
}
