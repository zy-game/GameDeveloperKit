using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Logger
{
    public readonly struct ProfileTable
    {
        public ProfileTable(
            ProfileHandle handle,
            IReadOnlyList<ProfileColumn> columns,
            IReadOnlyList<ProfileRow> rows,
            Exception exception = null)
        {
            Handle = handle;
            Name = handle.Name;
            Category = handle.Category;
            Columns = columns;
            Rows = rows;
            Exception = exception;
        }

        public ProfileHandle Handle { get; }

        public string Name { get; }

        public string Category { get; }

        public IReadOnlyList<ProfileColumn> Columns { get; }

        public IReadOnlyList<ProfileRow> Rows { get; }

        public Exception Exception { get; }

        public bool HasError => Exception != null;
    }
}
