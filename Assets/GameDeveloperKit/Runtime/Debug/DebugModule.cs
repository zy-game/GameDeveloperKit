using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Command;
using GameDeveloperKit.Timer;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Logger
{
    /// <summary>
    /// 定义 Debug Module 类型。
    /// </summary>
    public class DebugModule : GameModuleBase
    {
        /// <summary>
        /// 定义 Unity Category 常量。
        /// </summary>
        private const string UnityCategory = "Unity";
        /// <summary>
        /// 定义 Root Name 常量。
        /// </summary>
        private const string RootName = "GameDeveloperKit.Debug";
        /// <summary>
        /// 获取或设置 Unity Tags。
        /// </summary>
        private static readonly string[] UnityTags = { UnityCategory };

        /// <summary>
        /// 存储 Debug Profile。
        /// </summary>
        private readonly DebugProfileHandle m_DebugProfile;
        /// <summary>
        /// 存储 Memory Profile。
        /// </summary>
        private readonly MemoryProfileHandle m_MemoryProfile;
        /// <summary>
        /// 存储 Device Info Profile。
        /// </summary>
        private readonly DeviceInfoProfileHandle m_DeviceInfoProfile;
        /// <summary>
        /// 存储 Root。
        /// </summary>
        private GameObject m_Root;
        /// <summary>
        /// 存储 Gui Driver。
        /// </summary>
        private DebugGuiDriver m_GuiDriver;
        /// <summary>
        /// 记录 Unity Log Capture Registered 状态。
        /// </summary>
        private bool m_UnityLogCaptureRegistered;

        /// <summary>
        /// 初始化 Debug Module。
        /// </summary>
        public DebugModule()
        {
            m_DebugProfile = new DebugProfileHandle(Settings, () => Enabled, GetTimerTick);
            m_MemoryProfile = new MemoryProfileHandle(Settings);
            m_DeviceInfoProfile = new DeviceInfoProfileHandle();
        }

        public bool Enabled { get; set; } = true;

        public LogLevel MinimumLevel
        {
            get => m_DebugProfile.MinimumLevel;
            set => m_DebugProfile.MinimumLevel = value;
        }

        public DebugSettings Settings { get; } = new DebugSettings();

        /// <summary>
        /// 存储 Logs。
        /// </summary>
        public DebugLogBuffer Logs => m_DebugProfile.Logs;

        public DebugProfileRegistry Profiles { get; } = new DebugProfileRegistry();

        public DebugConsole Console { get; } = new DebugConsole();

        /// <summary>
        /// 存储 Metrics。
        /// </summary>
        public DebugMetricSnapshot Metrics => m_MemoryProfile.Metrics;

        /// <summary>
        /// 存储 Debug Profile。
        /// </summary>
        public DebugProfileHandle DebugProfile => m_DebugProfile;

        /// <summary>
        /// 存储 Memory Profile。
        /// </summary>
        public MemoryProfileHandle MemoryProfile => m_MemoryProfile;

        /// <summary>
        /// 存储 Device Info Profile。
        /// </summary>
        public DeviceInfoProfileHandle DeviceInfoProfile => m_DeviceInfoProfile;

        public bool ConsoleVisible
        {
            get => Enabled && Console.Visible;
            set => Console.Visible = value;
        }

        public bool OverlayVisible
        {
            get => ConsoleVisible;
            set => ConsoleVisible = value;
        }

        /// <summary>
        /// 启动 member。
        /// </summary>
        /// <returns>操作完成任务。</returns>
        public override UniTask Startup()
        {
            Enabled = true;
            m_DebugProfile.Reset();
            m_MemoryProfile.Reset();
            Profiles.Clear();
            RegisterBuiltInProfiles();
            ConsoleVisible = false;
            RegisterUnityLogCapture();
            CreateGuiDriver();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 关闭 member。
        /// </summary>
        /// <returns>操作完成任务。</returns>
        public override UniTask Shutdown()
        {
            UnregisterUnityLogCapture();
            m_DebugProfile.Shutdown();
            m_MemoryProfile.Reset();
            Profiles.Clear();
            Enabled = false;
            ConsoleVisible = false;
            DestroyGuiDriver();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 执行 Assert。
        /// </summary>
        /// <param name="condition">condition 参数。</param>
        /// <param name="message">message 参数。</param>
        public void Assert(bool condition, string message = null)
        {
            if (!condition)
            {
                throw new GameException(message ?? "Assertion failed");
            }
        }

        /// <summary>
        /// 执行 Trace。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Trace(string message, string category = null, object context = null)
        {
            m_DebugProfile.Trace(message, category, context);
        }

        /// <summary>
        /// 执行 Debug。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Debug(string message, string category = null, object context = null)
        {
            m_DebugProfile.Debug(message, category, context);
        }

        /// <summary>
        /// 执行 Info。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Info(string message, string category = null, object context = null)
        {
            m_DebugProfile.Info(message, category, context);
        }

        /// <summary>
        /// 执行 Warning。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Warning(string message, string category = null, object context = null)
        {
            m_DebugProfile.Warning(message, category, context);
        }

        /// <summary>
        /// 执行 Error。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Error(string message, string category = null, object context = null)
        {
            m_DebugProfile.Error(message, category, context);
        }

        /// <summary>
        /// 执行 Error。
        /// </summary>
        /// <param name="exception">exception 参数。</param>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Error(Exception exception, string message = null, string category = null, object context = null)
        {
            m_DebugProfile.Error(exception, message, category, context);
        }

        /// <summary>
        /// 执行 Fatal。
        /// </summary>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Fatal(string message, string category = null, object context = null)
        {
            m_DebugProfile.Fatal(message, category, context);
        }

        /// <summary>
        /// 执行 Fatal。
        /// </summary>
        /// <param name="exception">exception 参数。</param>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Fatal(Exception exception, string message = null, string category = null, object context = null)
        {
            m_DebugProfile.Fatal(exception, message, category, context);
        }

        /// <summary>
        /// 执行 Log。
        /// </summary>
        /// <param name="level">level 参数。</param>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Log(LogLevel level, string message, string category = null, object context = null)
        {
            m_DebugProfile.Log(level, message, category, context);
        }

        /// <summary>
        /// 执行 Log。
        /// </summary>
        /// <param name="level">level 参数。</param>
        /// <param name="exception">exception 参数。</param>
        /// <param name="message">message 参数。</param>
        /// <param name="category">category 参数。</param>
        /// <param name="context">context 参数。</param>
        public void Log(LogLevel level, Exception exception, string message = null, string category = null, object context = null)
        {
            m_DebugProfile.Log(level, exception, message, category, context);
        }

        /// <summary>
        /// 注册 Profile。
        /// </summary>
        /// <param name="handle">handle 参数。</param>
        public void RegisterProfile(ProfileHandle handle)
        {
            Profiles.Register(handle);
        }

        /// <summary>
        /// 注销 Profile。
        /// </summary>
        /// <param name="handle">handle 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        public bool UnregisterProfile(ProfileHandle handle)
        {
            return Profiles.Unregister(handle);
        }

        /// <summary>
        /// 执行 Execute Command Async。
        /// </summary>
        /// <param name="commandLine">command Line 参数。</param>
        /// <returns>操作完成任务。</returns>
        public UniTask<CommandInvokeResult> ExecuteCommandAsync(string commandLine)
        {
            if (!Enabled || !Settings.CommandEnabled)
            {
                var disabled = CommandInvokeResult.DisabledResult("Debug command input is disabled.");
                return UniTask.FromResult(disabled);
            }

            if (!TryParseCommandLine(commandLine, out var commandName, out var args, out var error))
            {
                var failed = CommandInvokeResult.Failed(commandName, error);
                return UniTask.FromResult(failed);
            }

            if (!App.TryGetRegistered<CommandModule>(out var commandModule))
            {
                var failed = CommandInvokeResult.Failed(commandName, "CommandModule is not registered.");
                return UniTask.FromResult(failed);
            }

            return commandModule.ExecuteAsync(commandName, args);
        }

        /// <summary>
        /// 设置 Category Enabled。
        /// </summary>
        /// <param name="category">category 参数。</param>
        /// <param name="enabled">enabled 参数。</param>
        public void SetCategoryEnabled(string category, bool enabled)
        {
            m_DebugProfile.SetCategoryEnabled(category, enabled);
        }

        /// <summary>
        /// 执行 Is Category Enabled。
        /// </summary>
        /// <param name="category">category 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        public bool IsCategoryEnabled(string category)
        {
            return m_DebugProfile.IsCategoryEnabled(category);
        }

        /// <summary>
        /// 执行 Update Metrics。
        /// </summary>
        /// <param name="deltaTime">delta Time 参数。</param>
        internal void UpdateMetrics(float deltaTime)
        {
            if (!Enabled)
            {
                return;
            }

            m_MemoryProfile.Sample(deltaTime);
        }

        /// <summary>
        /// 绘制 Gui。
        /// </summary>
        internal void DrawGui()
        {
            m_GuiDriver?.DrawGui();
        }

        /// <summary>
        /// 尝试执行 Try Parse Command Line。
        /// </summary>
        /// <param name="commandLine">command Line 参数。</param>
        /// <param name="commandName">command Name 参数。</param>
        /// <param name="args">args 参数。</param>
        /// <param name="error">error 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        private static bool TryParseCommandLine(string commandLine, out string commandName, out object[] args, out string error)
        {
            commandName = null;
            args = Array.Empty<object>();
            error = null;

            if (string.IsNullOrWhiteSpace(commandLine))
            {
                error = "Command line cannot be empty.";
                return false;
            }

            var parts = commandLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                error = "Command line cannot be empty.";
                return false;
            }

            commandName = parts[0];
            args = new object[parts.Length - 1];
            for (var i = 1; i < parts.Length; i++)
            {
                args[i - 1] = parts[i];
            }

            return true;
        }

        /// <summary>
        /// 创建 Gui Driver。
        /// </summary>
        private void CreateGuiDriver()
        {
            if (m_GuiDriver != null)
            {
                return;
            }

            m_Root = new GameObject(RootName);
            Object.DontDestroyOnLoad(m_Root);
            m_GuiDriver = m_Root.AddComponent<DebugGuiDriver>();
            m_GuiDriver.Initialize(this);
        }

        /// <summary>
        /// 销毁 Gui Driver。
        /// </summary>
        private void DestroyGuiDriver()
        {
            if (m_Root == null)
            {
                m_GuiDriver = null;
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(m_Root);
            }
            else
            {
                Object.DestroyImmediate(m_Root);
            }

            m_Root = null;
            m_GuiDriver = null;
        }

        /// <summary>
        /// 注册 Unity Log Capture。
        /// </summary>
        private void RegisterUnityLogCapture()
        {
            if (m_UnityLogCaptureRegistered)
            {
                return;
            }

            Application.logMessageReceived += OnUnityLogMessageReceived;
            m_UnityLogCaptureRegistered = true;
        }

        /// <summary>
        /// 注销 Unity Log Capture。
        /// </summary>
        private void UnregisterUnityLogCapture()
        {
            if (!m_UnityLogCaptureRegistered)
            {
                return;
            }

            Application.logMessageReceived -= OnUnityLogMessageReceived;
            m_UnityLogCaptureRegistered = false;
        }

        /// <summary>
        /// 处理 Unity Log Message Received 回调。
        /// </summary>
        /// <param name="condition">condition 参数。</param>
        /// <param name="stackTrace">stack Trace 参数。</param>
        /// <param name="type">type 参数。</param>
        private void OnUnityLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (!Enabled || !Settings.UnityLogCaptureEnabled)
            {
                return;
            }

            m_DebugProfile.WriteLog(
                MapUnityLogLevel(type),
                null,
                FormatUnityLogMessage(condition, stackTrace, type),
                UnityCategory,
                null,
                UnityTags);
        }

        /// <summary>
        /// 执行 Map Unity Log Level。
        /// </summary>
        /// <param name="type">type 参数。</param>
        /// <returns>执行结果。</returns>
        private static LogLevel MapUnityLogLevel(LogType type)
        {
            switch (type)
            {
                case LogType.Warning:
                    return LogLevel.Warning;
                case LogType.Log:
                    return LogLevel.Info;
                default:
                    return LogLevel.Error;
            }
        }

        /// <summary>
        /// 执行 Format Unity Log Message。
        /// </summary>
        /// <param name="condition">condition 参数。</param>
        /// <param name="stackTrace">stack Trace 参数。</param>
        /// <param name="type">type 参数。</param>
        /// <returns>执行结果。</returns>
        private static string FormatUnityLogMessage(string condition, string stackTrace, LogType type)
        {
            if ((type != LogType.Error && type != LogType.Exception) || string.IsNullOrWhiteSpace(stackTrace))
            {
                return condition ?? string.Empty;
            }

            return $"{condition}\n{stackTrace}";
        }

        /// <summary>
        /// 获取 Timer Tick。
        /// </summary>
        /// <returns>执行结果。</returns>
        private long GetTimerTick()
        {
            return App.TryGetRegistered<TimerModule>(out var timer) ? timer.Tick : 0L;
        }

        /// <summary>
        /// 注册 Built In Profiles。
        /// </summary>
        private void RegisterBuiltInProfiles()
        {
            RegisterProfile(m_MemoryProfile);
            m_DeviceInfoProfile.Refresh();
            RegisterProfile(m_DeviceInfoProfile);
        }

    }
}
