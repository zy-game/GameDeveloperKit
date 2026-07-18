using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDeveloperKit.File
{
    public partial class FileModule
    {
        internal IReadOnlyList<string> ListPaths(string prefix)
        {
            BeginOperation();
            try
            {
                ValidateVirtualPath(prefix, nameof(prefix));
                EnsureReady();
                return m_Manifest.GetAllEntries()
                    .Where(entry => entry.Usegd && entry.FilePath.StartsWith(prefix, StringComparison.Ordinal))
                    .Select(entry => entry.FilePath)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
            }
            finally
            {
                EndOperation();
            }
        }
    }
}
