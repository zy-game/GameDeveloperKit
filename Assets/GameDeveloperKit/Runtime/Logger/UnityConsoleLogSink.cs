using UnityEngine;

namespace GameDeveloperKit.Logger
{
    public sealed class UnityConsoleLogSink : ILogSink
    {
        public void Write(LogEntry entry)
        {
            var message = FormatMessage(entry);
            switch (entry.Level)
            {
                case LogLevel.Warning:
                    Debug.LogWarning(message, entry.Context as Object);
                    break;
                case LogLevel.Error:
                case LogLevel.Fatal:
                    Debug.LogError(message, entry.Context as Object);
                    break;
                default:
                    Debug.Log(message, entry.Context as Object);
                    break;
            }
        }

        private static string FormatMessage(LogEntry entry)
        {
            if (entry.Exception == null)
            {
                return $"[{entry.Category}] {entry.Message}";
            }

            if (string.IsNullOrEmpty(entry.Message))
            {
                return $"[{entry.Category}] {entry.Exception}";
            }

            return $"[{entry.Category}] {entry.Message}\n{entry.Exception}";
        }
    }
}
