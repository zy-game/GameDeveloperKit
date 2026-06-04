using System;

namespace GameDeveloperKit.Logger
{
    internal static class DebugRedactionUtility
    {
        private static readonly string[] SensitiveTokens =
        {
            "secret",
            "token",
            "password",
            "key",
        };

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

        public static object RedactValue(string key, object value)
        {
            if (IsSensitive(key))
            {
                return "[REDACTED]";
            }

            return value is string text ? Redact(text) : value;
        }

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
