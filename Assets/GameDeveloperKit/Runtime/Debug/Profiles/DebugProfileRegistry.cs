using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Logger
{
    public sealed class DebugProfileRegistry
    {
        private readonly List<ProfileState> m_States = new List<ProfileState>();

        public bool RedactionEnabled { get; set; } = true;

        public void Register(ProfileHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            foreach (var state in m_States)
            {
                if (ReferenceEquals(state.Handle, handle))
                {
                    return;
                }
            }

            var newState = new ProfileState(handle);
            Refresh(newState);
            m_States.Add(newState);
        }

        public bool Unregister(ProfileHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            for (var i = 0; i < m_States.Count; i++)
            {
                if (!ReferenceEquals(m_States[i].Handle, handle))
                {
                    continue;
                }

                m_States.RemoveAt(i);
                return true;
            }

            return false;
        }

        public void Clear()
        {
            m_States.Clear();
        }

        public void Refresh(float deltaTime)
        {
            foreach (var state in m_States)
            {
                if (!state.Handle.Enabled)
                {
                    continue;
                }

                state.Elapsed += deltaTime;
                if (state.Table.Handle == null || state.Elapsed >= state.Handle.RefreshInterval)
                {
                    Refresh(state);
                }
            }
        }

        public IReadOnlyList<ProfileTable> Snapshot()
        {
            var tables = new List<ProfileTable>();
            foreach (var state in m_States)
            {
                tables.Add(state.Table);
            }

            return tables;
        }

        private void Refresh(ProfileState state)
        {
            try
            {
                state.Table = new ProfileTable(
                    state.Handle,
                    state.Handle.Columns,
                    state.Handle.Enabled ? RedactRows(state.Handle.Snapshot()) : Array.Empty<ProfileRow>());
            }
            catch (Exception exception)
            {
                IReadOnlyList<ProfileColumn> columns;
                try
                {
                    columns = state.Handle.Columns;
                }
                catch
                {
                    columns = Array.Empty<ProfileColumn>();
                }

                state.Table = new ProfileTable(state.Handle, columns, Array.Empty<ProfileRow>(), exception);
            }

            state.Elapsed = 0f;
        }

        private IReadOnlyList<ProfileRow> RedactRows(IReadOnlyList<ProfileRow> rows)
        {
            if (!RedactionEnabled || rows == null)
            {
                return rows ?? Array.Empty<ProfileRow>();
            }

            var redactedRows = new List<ProfileRow>(rows.Count);
            foreach (var row in rows)
            {
                var values = new Dictionary<string, object>();
                if (row.Values != null)
                {
                    foreach (var value in row.Values)
                    {
                        values[value.Key] = DebugRedactionUtility.RedactValue(value.Key, value.Value);
                    }
                }

                redactedRows.Add(new ProfileRow(values));
            }

            return redactedRows;
        }

        private sealed class ProfileState
        {
            public ProfileState(ProfileHandle handle)
            {
                Handle = handle;
            }

            public ProfileHandle Handle { get; }

            public float Elapsed { get; set; }

            public ProfileTable Table { get; set; }
        }
    }
}
