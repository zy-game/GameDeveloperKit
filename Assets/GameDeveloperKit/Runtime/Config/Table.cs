using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Config
{
    public class Table<TRow> where TRow : IConfig
    {
        private readonly IReadOnlyList<TRow> m_Rows;
        public IReadOnlyList<TRow> Rows => m_Rows;

        public Table(List<TRow> rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            ValidateRows(rows);
            m_Rows = new List<TRow>(rows).AsReadOnly();
        }

        public TRow GetRowByKey(object key)
        {
            foreach (var row in Rows)
            {
                if (row.key.Match(key))
                {
                    return row;
                }
            }

            return default;
        }

        public TRow Find(Func<TRow, bool> predicate)
        {
            return FirstOrDefault(predicate);
        }

        public IEnumerable<TRow> Where(Func<TRow, bool> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            foreach (var row in Rows)
            {
                if (predicate(row))
                {
                    yield return row;
                }
            }
        }

        public TRow FirstOrDefault()
        {
            return Rows.Count > 0 ? Rows[0] : default;
        }

        public TRow FirstOrDefault(Func<TRow, bool> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            foreach (var row in Rows)
            {
                if (predicate(row))
                {
                    return row;
                }
            }

            return default;
        }

        private static void ValidateRows(List<TRow> rows)
        {
            var keys = new HashSet<object>();
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    throw new GameException($"Config row '{typeof(TRow).Name}' at index {i} is null.");
                }

                var key = row.key;
                if (key == null)
                {
                    throw new GameException($"Config row '{typeof(TRow).Name}' at index {i} has no key.");
                }

                if (!keys.Add(key.Value))
                {
                    throw new GameException($"Config row '{typeof(TRow).Name}' has duplicate key '{key.Value}'.");
                }
            }
        }
    }
}
