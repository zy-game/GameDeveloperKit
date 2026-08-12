using System;
using System.Collections.Generic;

namespace GameDeveloperKit.Config
{
    /// <summary>
    /// 配置表。
    /// </summary>
    /// <remarks>
    /// 约束放宽为 <c>class</c>，行类型不再强制实现 <see cref="IConfig"/>：
    /// <list type="bullet">
    /// <item>实现 <see cref="IConfig"/> 的行仍按 <see cref="IConfig.key"/> 做唯一性校验与 <see cref="GetRowByKey"/> 匹配；</item>
    /// <item>未实现 <see cref="IConfig"/> 的行（如 Luban 生成的 Bean）跳过 key 校验，<see cref="GetRowByKey"/> 在无任何 IConfig 行时抛 <see cref="NotSupportedException"/>。</item>
    /// </list>
    /// </remarks>
    /// <typeparam name="TRow">配置行类型。</typeparam>
    public class Table<TRow> where TRow : class
    {
        private readonly IReadOnlyList<TRow> m_Rows;
        public IReadOnlyList<TRow> Rows => m_Rows;

        /// <summary>
        /// 初始化 Table。
        /// </summary>
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
        /// <remarks>仅对实现 <see cref="IConfig"/> 的行有效；表中无 IConfig 行时抛 <see cref="NotSupportedException"/>。</remarks>
        public TRow GetRowByKey(object key)
        {
            var hasKeyedRow = false;
            foreach (var row in Rows)
            {
                if (row is IConfig keyedRow && keyedRow.key != null)
                {
                    hasKeyedRow = true;
                    if (keyedRow.key.Match(key))
                    {
                        return row;
                    }
                }
            }

            if (hasKeyedRow is false)
            {
                throw new NotSupportedException(
                    $"GetRowByKey is not supported on '{typeof(TRow).Name}' because no row implements {nameof(IConfig)}.");
            }

            return default;
        }

        /// <summary>
        /// 查找 member。
        /// </summary>
        public TRow Find(Func<TRow, bool> predicate)
        {
            return FirstOrDefault(predicate);
        }

        /// <summary>
        /// 执行 Where。
        /// </summary>
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
        public TRow FirstOrDefault()
        {
            return Rows.Count > 0 ? Rows[0] : default;
        }

        /// <summary>
        /// 执行 First Or Default。
        /// </summary>
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
        /// <remarks>
        /// 非 null 行始终校验；实现 <see cref="IConfig"/> 的行额外按 <see cref="IConfig.key"/> 做唯一性校验
        /// （<see cref="IConfig.key"/> 为 null 时仍按既有契约抛 "has no key"，保持向后兼容）。
        /// 未实现 <see cref="IConfig"/> 的行跳过 key 校验。
        /// </remarks>
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

                if (row is IConfig keyedRow)
                {
                    var key = keyedRow.key;
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
}
