namespace GameDeveloperKit.Debugger
{
    public abstract class ProfileHandle
    {
        public abstract string Name { get; }

        /// <summary>
        /// 绘制 member。
        /// </summary>
        protected internal abstract void Draw();
    }
}
