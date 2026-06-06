using System.Collections.Generic;

namespace GameDeveloperKit.Logger
{
    public readonly struct ProfileRow
    {
        public ProfileRow(IReadOnlyDictionary<string, object> values)
        {
            Values = values;
        }

        public IReadOnlyDictionary<string, object> Values { get; }
    }
}
