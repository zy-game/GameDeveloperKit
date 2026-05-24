using System;

namespace GameDeveloperKit.Event
{
    public sealed class Subscription : IReference
    {
        private EventModule m_Module;
        private Listener m_Listener;

        internal Subscription(EventModule module, Listener listener)
        {
            m_Module = module ?? throw new ArgumentNullException(nameof(module));
            m_Listener = listener ?? throw new ArgumentNullException(nameof(listener));
        }

        public bool IsActive => m_Listener != null && m_Listener.IsActive;

        public void Cancel()
        {
            if (m_Module == null || m_Listener == null)
            {
                return;
            }

            m_Module.Unsubscribe(m_Listener);
            m_Module = null;
            m_Listener = null;
        }

        public void Release()
        {
            Cancel();
        }
    }
}
