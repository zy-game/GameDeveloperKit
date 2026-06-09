namespace GameDeveloperKit.Logger
{
    public abstract class ProfileHandle
    {
        public abstract string Name { get; }

        protected internal abstract void Draw();
    }
}
