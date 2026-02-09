using System;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GameDeveloperKit.Log
{
    /// <summary>
    /// 日志模块 - 统一管理日志、远程上传、命令系统、性能监控和调试GUI
    /// </summary>
    public class LoggerModule : IModule, ILogger, ILogHandler
    {
        private readonly string _prefix;
        private LogLevel _minLevel;

        private LogCache _logCache;
        private RemoteLoggerTerminal _remoteTerminal;
        private CommandManager _commandManager;
        private ProfilerCollector _profiler;
        private DebugConsole _debugConsole;
        private RemoteLoggerConfig _remoteConfig;
        private ILogHandler _defaultLogHandler;

        public LogCache Cache => _logCache;
        public CommandManager Commands => _commandManager;
        public ProfilerCollector Profiler => _profiler;
        public bool IsRemoteConnected => _remoteTerminal?.IsConnected ?? false;

        /// <summary>
        /// 注册自定义调试面板
        /// </summary>
        public void RegisterPanel(IDebugPanelIMGUI panel)
        {
            _debugConsole?.RegisterPanel(panel);
        }

        public LoggerModule(string prefix = "[GameFramework]", LogLevel minLevel = LogLevel.Debug)
        {
            _prefix = prefix;
            _minLevel = minLevel;
        }

        public void OnStartup()
        {
            _logCache = new LogCache(1000);
            _commandManager = new CommandManager();
            _profiler = new ProfilerCollector();

            // 替换Unity的日志处理器以获取堆栈信息
            _defaultLogHandler = UnityEngine.Debug.unityLogger.logHandler;
            UnityEngine.Debug.unityLogger.logHandler = this;

            // 检查是否已存在 DebugConsole（可能在场景中手动添加）
            _debugConsole = DebugConsole.Instance;
            if (_debugConsole == null)
            {
                // 尝试查找场景中已存在的实例（包括未激活的）
                var consoles = UnityEngine.Object.FindObjectsByType<DebugConsole>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (consoles.Length > 0)
                {
                    _debugConsole = consoles[0];
                }
            }
            if (_debugConsole == null)
            {
                // 创建调试控制台 (IMGUI版本)
                var consoleGo = new GameObject("[DebugConsole]");
                _debugConsole = consoleGo.AddComponent<DebugConsole>();
            }
            _debugConsole.Initialize(_logCache, _commandManager, _profiler);
        }

        public void OnUpdate(float deltaTime)
        {
            _profiler?.Update();
        }

        public void OnClearup()
        {
            // 恢复默认日志处理器
            if (_defaultLogHandler != null)
                UnityEngine.Debug.unityLogger.logHandler = _defaultLogHandler;

            _remoteTerminal?.Dispose();

            if (_debugConsole != null)
                UnityEngine.Object.Destroy(_debugConsole.gameObject);
        }

        /// <summary>
        /// 配置远程日志服务器
        /// </summary>
        public async UniTask ConfigureRemoteAsync(RemoteLoggerConfig config)
        {
            _remoteConfig = config;
            _remoteTerminal?.Dispose();

            if (config.Enabled)
            {
                _remoteTerminal = new RemoteLoggerTerminal(config);
                await _remoteTerminal.StartAsync();
            }
        }

        /// <summary>
        /// 设置最低日志级别
        /// </summary>
        public void SetMinLevel(LogLevel level) => _minLevel = level;

        #region ILogger Implementation

        [DebuggerHidden, HideInCallstack]
        public void Log(LogLevel level, string message)
        {
            if (level < _minLevel) return;
            var fullMessage = $"{_prefix} [{level}] {message}";
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

            var fullMsg = $"{_prefix} [{level}] {message}\n{exception}";
            switch (level)
            {
                case LogLevel.Debug:
                case LogLevel.Info:
                    UnityEngine.Debug.Log(fullMsg);
                    break;
                case LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(fullMsg);
                    break;
                case LogLevel.Error:
                case LogLevel.Fatal:
                    UnityEngine.Debug.LogError(fullMsg);
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

        #endregion

        #region ILogHandler Implementation

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            var message = args.Length > 0 ? string.Format(format, args) : format;
            var stackTrace = StackTraceUtility.ExtractStackTrace();

            var level = logType switch
            {
                LogType.Error => LogLevel.Error,
                LogType.Assert => LogLevel.Error,
                LogType.Warning => LogLevel.Warning,
                LogType.Log => LogLevel.Info,
                LogType.Exception => LogLevel.Fatal,
                _ => LogLevel.Debug
            };

            var entry = new LogEntry(level, message, stackTrace);
            _logCache?.Add(entry);
            _remoteTerminal?.Enqueue(entry);

            // 调用默认处理器输出到控制台
            _defaultLogHandler?.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            var message = exception.Message;
            var stackTrace = exception.StackTrace;

            var entry = new LogEntry(LogLevel.Fatal, message, stackTrace);
            _logCache?.Add(entry);
            _remoteTerminal?.Enqueue(entry);

            // 调用默认处理器输出到控制台
            _defaultLogHandler?.LogException(exception, context);
        }

        #endregion
    }
}
