using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Config
{
    /// <summary>
    /// 配置表。
    /// </summary>
    /// <typeparam name="TRow">配置行类型。</typeparam>
    public class Table<TRow> where TRow : IConfig
    {
        /// <summary>
        /// 存储 Rows。
        /// </summary>
        private readonly IReadOnlyList<TRow> m_Rows;
        /// <summary>
        /// 存储 Rows。
        /// </summary>
        public IReadOnlyList<TRow> Rows => m_Rows;

        /// <summary>
        /// 初始化 Table。
        /// </summary>
        /// <param name="rows">rows 参数。</param>
        public Table(List<TRow> rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            ValidateRows(rows);
            m_Rows = new List<TRow>(rows).AsReadOnly();
        }

        /// <summary>
        /// 获取 Row By Key。
        /// </summary>
        /// <param name="key">key 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 查找 member。
        /// </summary>
        /// <param name="predicate">predicate 参数。</param>
        /// <returns>执行结果。</returns>
        public TRow Find(Func<TRow, bool> predicate)
        {
            return FirstOrDefault(predicate);
        }

        /// <summary>
        /// 执行 Where。
        /// </summary>
        /// <param name="predicate">predicate 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 执行 First Or Default。
        /// </summary>
        /// <returns>执行结果。</returns>
        public TRow FirstOrDefault()
        {
            return Rows.Count > 0 ? Rows[0] : default;
        }

        /// <summary>
        /// 执行 First Or Default。
        /// </summary>
        /// <param name="predicate">predicate 参数。</param>
        /// <returns>执行结果。</returns>
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

        /// <summary>
        /// 校验 Rows。
        /// </summary>
        /// <param name="rows">rows 参数。</param>
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
