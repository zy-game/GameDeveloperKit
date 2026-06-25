using System;

namespace GameDeveloperKit.Debugger
{
    public sealed partial class DebugProfileHandle
    {
        private sealed class RedactedLogException : Exception
        {
            private readonly string m_Text;

            /// <summary>
            /// 初始化 Redacted Log Exception。
            /// </summary>
            public RedactedLogException(string text) : base(text)
            {
                m_Text = text;
            }

            /// <summary>
            /// 执行 To String。
            /// </summary>
            public override string ToString()
            {
                return m_Text;
            }
        }
    }
}
