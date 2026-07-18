using System;

namespace GameDeveloperKit.ResourceEditor
{
    internal sealed class ResourceBuildPreparedRequest
    {
        private readonly ResourceBuildWorkflow m_Owner;
        private bool m_Consumed;

        internal ResourceBuildPreparedRequest(
            ResourceBuildWorkflow owner,
            ResourceBuildContext context,
            ResourceBuildPlan plan)
        {
            m_Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        }

        internal ResourceBuildContext Context { get; }

        internal ResourceBuildPlan Plan { get; }

        internal void Consume(ResourceBuildWorkflow owner)
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
