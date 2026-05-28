using System;
using System.Collections;

namespace GameDeveloperKit.Config
{
    public interface IConfigAsset
    {
        Type RowType { get; }

        IList GetRows();
    }
}
