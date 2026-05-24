namespace GameDeveloperKit.Event
{
    public abstract class ArgsBase : IEventArgs
    {
        private bool m_HasUse;

        public void Use()
        {
            m_HasUse = true;
        }

        public bool HasUse()
        {
            return m_HasUse;
        }
    }
}
