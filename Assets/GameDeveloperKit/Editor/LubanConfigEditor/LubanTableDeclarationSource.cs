using System;

namespace GameDeveloperKit.LubanConfigEditor
{
    /// <summary>
    /// 定义 Luban Table Declaration Source 类型。
    /// </summary>
    public abstract class LubanTableDeclarationSource
    {
        public abstract bool CanSave { get; }

        public abstract string ReadOnlyReason { get; }

        /// <summary>
        /// 创建。
        /// </summary>
        /// <param name="table">table 参数。</param>
        /// <returns>执行结果。</returns>
        public static LubanTableDeclarationSource Create(LubanTableDefinition table)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (string.Equals(table.SourceKind, "ExcelInline", StringComparison.OrdinalIgnoreCase))
            {
                return new ExcelInlineTableDeclarationSource();
            }

            return new ReadOnlyTableDeclarationSource("Source is read-only or was not found in current workspace scan.");
        }

        /// <summary>
        /// 保存。
        /// </summary>
        /// <param name="table">table 参数。</param>
        /// <returns>执行结果。</returns>
        public abstract LubanTableDeclarationSaveResult Save(LubanTableDefinition table);
    }

    /// <summary>
    /// 定义 Excel Inline Table Declaration Source 类型。
    /// </summary>
    public sealed class ExcelInlineTableDeclarationSource : LubanTableDeclarationSource
    {
        public override bool CanSave => false;

        public override string ReadOnlyReason => "Excel inline source is read-only in this editor. Open Source and edit # rows in the workbook, then run Check.";

        public override LubanTableDeclarationSaveResult Save(LubanTableDefinition table)
        {
            return LubanTableDeclarationSaveResult.Failure(ReadOnlyReason);
        }
    }

    /// <summary>
    /// 定义 Read Only Table Declaration Source 类型。
    /// </summary>
    public sealed class ReadOnlyTableDeclarationSource : LubanTableDeclarationSource
    {
        private readonly string m_Reason;

        public ReadOnlyTableDeclarationSource(string reason)
        {
            m_Reason = reason;
        }

        public override bool CanSave => false;

        public override string ReadOnlyReason => m_Reason;

        public override LubanTableDeclarationSaveResult Save(LubanTableDefinition table)
        {
            return LubanTableDeclarationSaveResult.Failure(ReadOnlyReason);
        }
    }

    /// <summary>
    /// 定义 Luban Table Declaration Save Result 类型。
    /// </summary>
    public readonly struct LubanTableDeclarationSaveResult
    {
        private LubanTableDeclarationSaveResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public bool Success { get; }

        public string Message { get; }

        /// <summary>
        /// 创建 Success。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <returns>执行结果。</returns>
        public static LubanTableDeclarationSaveResult SuccessResult(string message)
        {
            return new LubanTableDeclarationSaveResult(true, message);
        }

        /// <summary>
        /// 创建 Failure。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <returns>执行结果。</returns>
        public static LubanTableDeclarationSaveResult Failure(string message)
        {
            return new LubanTableDeclarationSaveResult(false, message);
        }
    }
}
