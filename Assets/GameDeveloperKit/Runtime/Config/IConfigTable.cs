using System;
using System.Collections;

namespace GameDeveloperKit.Config
{
    public interface IConfigTable
    {
        string Name { get; }

        Type RowType { get; }

        Type KeyType { get; }

        IEnumerable RowsUntyped { get; }
    }
}
