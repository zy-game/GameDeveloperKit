using System.Collections.Generic;

namespace GameDeveloperKit.Logger
{
    public abstract class ProfileHandle
    {
        public abstract string Name { get; }

        public virtual string Category => "Runtime";

        public virtual float RefreshInterval => 0.5f;

        public virtual bool Enabled { get; set; } = true;

        public abstract IReadOnlyList<ProfileColumn> Columns { get; }

        public abstract IReadOnlyList<ProfileRow> Snapshot();
    }
}
