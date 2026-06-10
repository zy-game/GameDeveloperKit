namespace GameDeveloperKit.Logger
{
    /// <summary>
    /// 定义 Profile Handle 类型。
    /// </summary>
    public abstract class ProfileHandle
    {
        public abstract string Name { get; }

        /// <summary>
        /// 绘制 member。
        /// </summary>
        /// <returns>执行结果。</returns>
        protected internal abstract void Draw();
    }
}
