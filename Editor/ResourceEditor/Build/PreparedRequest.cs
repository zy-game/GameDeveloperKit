using System;

namespace GameDeveloperKit.ResourceEditor.Build
{
    internal sealed class PreparedRequest
    {
        private readonly Workflow m_Owner;
        private bool m_Consumed;

        internal PreparedRequest(
            Workflow owner,
            Context context,
            Plan plan)
        {
            m_Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        }

        internal Context Context { get; }

        internal Plan Plan { get; }

        internal void Consume(Workflow owner)
        {
            if (ReferenceEquals(owner, m_Owner) is false)
            {
                throw new ArgumentException("Resource build request belongs to another workflow.", nameof(owner));
            }
            if (m_Consumed)
            {
                throw new InvalidOperationException("Resource build request can only be executed once.");
            }

            m_Consumed = true;
        }
    }
}
