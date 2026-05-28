namespace GameDeveloperKit.Timer
{
    public abstract class TimerHandle : IReference
    {
        public abstract void Execute(float deltaTime);

        public void Release()
        {
        }

    }
}