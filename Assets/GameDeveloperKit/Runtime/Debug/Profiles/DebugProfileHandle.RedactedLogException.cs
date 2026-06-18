using System;

namespace GameDeveloperKit.Logger
{
    public sealed partial class DebugProfileHandle
    {
        /// <summary>
        /// 定义 Redacted Log Exception 类型。
        /// </summary>
        private sealed class RedactedLogException : Exception
        {
            /// <summary>
            /// 存储 Text。
            /// </summary>
            private readonly string m_Text;

            /// <summary>
            /// 初始化 Redacted Log Exception。
            /// </summary>
            /// <param name="text">text 参数。</param>
            public RedactedLogException(string text) : base(text)
            {
                m_Text = text;
            }

            /// <summary>
            /// 执行 To String。
            /// </summary>
            /// <returns>执行结果。</returns>
            public override string ToString()
            {
                return m_Text;
            }
        }
    }
}
