using System;

namespace GameDeveloperKit.Logger
{
    /// <summary>
    /// 定义 Debug Redaction Utility 类型。
    /// </summary>
    internal static class DebugRedactionUtility
    {
        private static readonly string[] SensitiveTokens =
        {
            "secret",
            "token",
            "password",
            "key",
        };

        /// <summary>
        /// 执行 Redact。
        /// </summary>
        /// <param name="value">value 参数。</param>
        /// <returns>执行结果。</returns>
        public static string Redact(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            foreach (var token in SensitiveTokens)
            {
                if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "[REDACTED]";
                }
            }

            return value;
        }

        /// <summary>
        /// 执行 Redact Value。
        /// </summary>
        /// <param name="key">key 参数。</param>
        /// <param name="value">value 参数。</param>
        /// <returns>执行结果。</returns>
        public static object RedactValue(string key, object value)
        {
            if (IsSensitive(key))
            {
                return "[REDACTED]";
            }

            return value is string text ? Redact(text) : value;
        }

        /// <summary>
        /// 执行 Is Sensitive。
        /// </summary>
        /// <param name="key">key 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        private static bool IsSensitive(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            foreach (var token in SensitiveTokens)
            {
                if (key.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
