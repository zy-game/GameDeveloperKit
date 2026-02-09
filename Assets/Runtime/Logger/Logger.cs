using System;
using System.Diagnostics;
using UnityEngine;

namespace GameDeveloperKit.Log
{
    /// <summary>
    /// 默认日志实现（使用Unity Debug）
    /// </summary>
    public class Logger : ILogger
    {
        private readonly string _prefix;
        private LogLevel _minLevel;

        public Logger(string prefix = "[GameFramework]", LogLevel minLevel = LogLevel.Debug)
        {
            _prefix = prefix;
            _minLevel = minLevel;
        }

        public void SetMinLevel(LogLevel level)
        {
            _minLevel = level;
        }
        [DebuggerHidden, HideInCallstack]
        public void Log(LogLevel level, string message)
        {
            if (level < _minLevel) return;

            string fullMessage = $"{_prefix} [{level}] {message}";

            switch (level)
            {
                case LogLevel.Debug:
                case LogLevel.Info:
                    UnityEngine.Debug.Log(fullMessage);
                    break;
                case LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(fullMessage);
                    break;
                case LogLevel.Error:
                case LogLevel.Fatal:
                    UnityEngine.Debug.LogError(fullMessage);
                    break;
            }
        }
        [DebuggerHidden, HideInCallstack]
        public void Log(LogLevel level, string message, Exception exception)
        {
            if (level < _minLevel) return;

            string fullMessage = $"{_prefix} [{level}] {message}\n{exception}";

            switch (level)
            {
                case LogLevel.Error:
                case LogLevel.Fatal:
                    UnityEngine.Debug.LogError(fullMessage);
                    break;
                default:
                    UnityEngine.Debug.LogWarning(fullMessage);
                    break;
            }
        }
        [DebuggerHidden, HideInCallstack]
        public void Debug(string message) => Log(LogLevel.Debug, message);
        [DebuggerHidden, HideInCallstack]
        public void Info(string message) => Log(LogLevel.Info, message);
        [DebuggerHidden, HideInCallstack]
        public void Warning(string message) => Log(LogLevel.Warning, message);
        [DebuggerHidden, HideInCallstack]
        public void Error(string message) => Log(LogLevel.Error, message);
        [DebuggerHidden, HideInCallstack]
        public void Error(string message, Exception exception) => Log(LogLevel.Error, message, exception);
        [DebuggerHidden, HideInCallstack]
        public void Fatal(string message) => Log(LogLevel.Fatal, message);
        [DebuggerHidden, HideInCallstack]
        public void Fatal(string message, Exception exception) => Log(LogLevel.Fatal, message, exception);
    }
}
