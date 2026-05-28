using System;
using System.Collections;
using System.Collections.Generic;

namespace GameDeveloperKit.Config
{
    public sealed class ConfigTable<TRow> : IConfigTable
    {
        private readonly Dictionary<object, TRow> m_RowsByKey;

        internal ConfigTable(string name, Type keyType, IReadOnlyList<TRow> rows, Dictionary<object, TRow> rowsByKey)
        {
            Name = name;
            KeyType = keyType;
            Rows = rows;
            m_RowsByKey = rowsByKey;
        }

        public string Name { get; }

        public Type RowType => typeof(TRow);

        public Type KeyType { get; }

        public IReadOnlyList<TRow> Rows { get; }

        public IEnumerable RowsUntyped => (IEnumerable)Rows;

        public bool TryGet(object key, out TRow row)
        {
            ValidateKey(key);
            return m_RowsByKey.TryGetValue(key, out row);
        }

        public TRow Get(object key)
        {
            if (TryGet(key, out var row))
            {
                return row;
            }

            throw new GameException($"Config table '{Name}' does not contain key '{key}'.");
        }

        private void ValidateKey(object key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.GetType() != KeyType)
            {
                throw new GameException(
                    $"Config table '{Name}' key type mismatch. Expected '{KeyType.FullName}', actual '{key.GetType().FullName}'.");
            }
        }
    }
}
