using System;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Command;
using GameDeveloperKit.Timer;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Logger
{
    public class DebugModule : GameModuleBase
    {
        private const string UnityCategory = "Unity";
        private const string RootName = "GameDeveloperKit.Debug";
        private static readonly string[] UnityTags = { UnityCategory };

        private readonly DebugProfileHandle m_DebugProfile;
        private readonly MemoryProfileHandle m_MemoryProfile;
        private readonly DeviceInfoProfileHandle m_DeviceInfoProfile;
        private GameObject m_Root;
        private DebugGuiDriver m_GuiDriver;
        private bool m_UnityLogCaptureRegistered;

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

        public DebugLogBuffer Logs => m_DebugProfile.Logs;

        public DebugProfileRegistry Profiles { get; } = new DebugProfileRegistry();

        public DebugConsole Console { get; } = new DebugConsole();

        public DebugMetricSnapshot Metrics => m_MemoryProfile.Metrics;

        public DebugProfileHandle DebugProfile => m_DebugProfile;

        public MemoryProfileHandle MemoryProfile => m_MemoryProfile;

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

        public void Assert(bool condition, string message = null)
        {
            if (!condition)
            {
                throw new GameException(message ?? "Assertion failed");
            }
        }

        public void Trace(string message, string category = null, object context = null)
        {
            m_DebugProfile.Trace(message, category, context);
        }

        public void Debug(string message, string category = null, object context = null)
        {
            m_DebugProfile.Debug(message, category, context);
        }

        public void Info(string message, string category = null, object context = null)
        {
            m_DebugProfile.Info(message, category, context);
        }

        public void Warning(string message, string category = null, object context = null)
        {
            m_DebugProfile.Warning(message, category, context);
        }

        public void Error(string message, string category = null, object context = null)
        {
            m_DebugProfile.Error(message, category, context);
        }

        public void Error(Exception exception, string message = null, string category = null, object context = null)
        {
            m_DebugProfile.Error(exception, message, category, context);
        }

        public void Fatal(string message, string category = null, object context = null)
        {
            m_DebugProfile.Fatal(message, category, context);
        }

        public void Fatal(Exception exception, string message = null, string category = null, object context = null)
        {
            m_DebugProfile.Fatal(exception, message, category, context);
        }

        public void Log(LogLevel level, string message, string category = null, object context = null)
        {
            m_DebugProfile.Log(level, message, category, context);
        }

        public void Log(LogLevel level, Exception exception, string message = null, string category = null, object context = null)
        {
            m_DebugProfile.Log(level, exception, message, category, context);
        }

        public void RegisterProfile(ProfileHandle handle)
        {
            Profiles.Register(handle);
        }

        public bool UnregisterProfile(ProfileHandle handle)
        {
            return Profiles.Unregister(handle);
        }

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

        public void SetCategoryEnabled(string category, bool enabled)
        {
            m_DebugProfile.SetCategoryEnabled(category, enabled);
        }

        public bool IsCategoryEnabled(string category)
        {
            return m_DebugProfile.IsCategoryEnabled(category);
        }

        internal void UpdateMetrics(float deltaTime)
        {
            if (!Enabled)
            {
                return;
            }

            m_MemoryProfile.Sample(deltaTime);
        }

        internal void DrawGui()
        {
            m_GuiDriver?.DrawGui();
        }

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

        private void RegisterUnityLogCapture()
        {
            if (m_UnityLogCaptureRegistered)
            {
                return;
            }

            Application.logMessageReceived += OnUnityLogMessageReceived;
            m_UnityLogCaptureRegistered = true;
        }

        private void UnregisterUnityLogCapture()
        {
            if (!m_UnityLogCaptureRegistered)
            {
                return;
            }

            Application.logMessageReceived -= OnUnityLogMessageReceived;
            m_UnityLogCaptureRegistered = false;
        }

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

        private static string FormatUnityLogMessage(string condition, string stackTrace, LogType type)
        {
            if ((type != LogType.Error && type != LogType.Exception) || string.IsNullOrWhiteSpace(stackTrace))
            {
                return condition ?? string.Empty;
            }

            return $"{condition}\n{stackTrace}";
        }

        private long GetTimerTick()
        {
            return App.TryGetRegistered<TimerModule>(out var timer) ? timer.Tick : 0L;
        }

        private void RegisterBuiltInProfiles()
        {
            RegisterProfile(m_MemoryProfile);
            m_DeviceInfoProfile.Refresh();
            RegisterProfile(m_DeviceInfoProfile);
        }

    }
}
