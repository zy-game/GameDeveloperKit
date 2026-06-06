using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Command;
using GameDeveloperKit.Timer;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Logger
{
    public class DebugModule : GameModuleBase
    {
        private const string DefaultCategory = "Default";
        private const string UnityCategory = "Unity";
        private const string RootName = "GameDeveloperKit.Debug";
        private static readonly string[] UnityTags = { UnityCategory };

        private static readonly string[] GuiTabs =
        {
            "Logs",
            "Profiles",
            "Timers",
            "Tools",
            "Settings",
        };

        private readonly Dictionary<string, bool> m_CategoryStates = new Dictionary<string, bool>();
        private readonly List<IAnalyticsSink> m_AnalyticsSinks = new List<IAnalyticsSink>();
        private readonly List<ILogSink> m_Sinks = new List<ILogSink>();
        private readonly List<IDebugLogTransport> m_Transports = new List<IDebugLogTransport>();
        private GameObject m_Root;
        private DebugGuiDriver m_GuiDriver;
        private long m_LogSequence;
        private bool m_UnityLogCaptureRegistered;
        private float m_MetricElapsed;
        private string m_CommandLine = string.Empty;
        private string m_CommandMessage = string.Empty;
        private Vector2 m_LogScroll;
        private Vector2 m_ProfileScroll;
        private Vector2 m_TimerScroll;
        private Vector2 m_ToolScroll;
        private Vector2 m_SettingsScroll;

        public bool Enabled { get; set; } = true;

        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

        public DebugSettings Settings { get; } = new DebugSettings();

        public DebugLogBuffer Logs { get; } = new DebugLogBuffer();

        public DebugProfileRegistry Profiles { get; } = new DebugProfileRegistry();

        public DebugConsole Console { get; } = new DebugConsole();

        public DebugMetricSnapshot Metrics { get; private set; }

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

        internal Exception LastSinkException { get; private set; }

        internal Exception LastAnalyticsException { get; private set; }

        internal Exception LastTransportException { get; private set; }

        public override UniTask Startup()
        {
            Enabled = true;
            MinimumLevel = LogLevel.Info;
            m_LogSequence = 0L;
            m_CategoryStates.Clear();
            m_AnalyticsSinks.Clear();
            m_Transports.Clear();
            LastSinkException = null;
            LastAnalyticsException = null;
            LastTransportException = null;
            ClearSinks();
            AddSink(new UnityConsoleLogSink());
            Logs.SetCapacity(Settings.LogCapacity);
            Profiles.Clear();
            Profiles.RedactionEnabled = Settings.RedactionEnabled;
            ConsoleVisible = Settings.ConsoleEnabled;
            RegisterUnityLogCapture();
            CreateGuiDriver();
            return UniTask.CompletedTask;
        }

        public override UniTask Shutdown()
        {
            UnregisterUnityLogCapture();
            ClearSinks();
            m_CategoryStates.Clear();
            m_AnalyticsSinks.Clear();
            m_Transports.Clear();
            LastSinkException = null;
            LastAnalyticsException = null;
            LastTransportException = null;
            Enabled = true;
            MinimumLevel = LogLevel.Info;
            ConsoleVisible = false;
            Logs.Clear();
            Profiles.Clear();
            DestroyGuiDriver();
            return UniTask.CompletedTask;
        }

        public void Assert(bool condition, string message = null)
        {
            if (!condition)
            {
                if (message == null)
                {
                    throw new GameException("Assertion failed");
                }

                throw new GameException(message);
            }
        }

        public void Trace(string message, string category = null, object context = null)
        {
            Log(LogLevel.Trace, message, category, context);
        }

        public void Debug(string message, string category = null, object context = null)
        {
            Log(LogLevel.Debug, message, category, context);
        }

        public void Info(string message, string category = null, object context = null)
        {
            Log(LogLevel.Info, message, category, context);
        }

        public void Warning(string message, string category = null, object context = null)
        {
            Log(LogLevel.Warning, message, category, context);
        }

        public void Error(string message, string category = null, object context = null)
        {
            Log(LogLevel.Error, message, category, context);
        }

        public void Error(Exception exception, string message = null, string category = null, object context = null)
        {
            Log(LogLevel.Error, exception, message, category, context);
        }

        public void Fatal(string message, string category = null, object context = null)
        {
            Log(LogLevel.Fatal, message, category, context);
        }

        public void Fatal(Exception exception, string message = null, string category = null, object context = null)
        {
            Log(LogLevel.Fatal, exception, message, category, context);
        }

        public void Log(LogLevel level, string message, string category = null, object context = null)
        {
            Log(level, null, message, category, context);
        }

        public void Log(LogLevel level, Exception exception, string message = null, string category = null, object context = null)
        {
            WriteLog(level, exception, message, category, context, null);
        }

        private void WriteLog(
            LogLevel level,
            Exception exception,
            string message,
            string category,
            object context,
            IReadOnlyList<string> tags)
        {
            ValidateLevel(level);
            var normalizedCategory = NormalizeCategory(category);
            if (!ShouldWrite(level, normalizedCategory))
            {
                return;
            }

            var record = new DebugLogRecord(
                DateTimeOffset.Now,
                ++m_LogSequence,
                UnityEngine.Time.frameCount,
                GetTimerTick(),
                level,
                RedactLogText(normalizedCategory),
                RedactLogText(message ?? string.Empty),
                exception,
                context,
                tags ?? Array.Empty<string>());

            Logs.Append(record);
            Write(record);
            Send(record);
        }

        public void AddSink(ILogSink sink)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            if (m_Sinks.Contains(sink))
            {
                return;
            }

            m_Sinks.Add(sink);
        }

        public void RemoveSink(ILogSink sink)
        {
            if (sink == null)
            {
                return;
            }

            m_Sinks.Remove(sink);
        }

        public void ClearSinks()
        {
            m_Sinks.Clear();
        }

        public void AddLogTransport(IDebugLogTransport transport)
        {
            if (transport == null)
            {
                throw new ArgumentNullException(nameof(transport));
            }

            if (m_Transports.Contains(transport))
            {
                return;
            }

            m_Transports.Add(transport);
        }

        public void RemoveLogTransport(IDebugLogTransport transport)
        {
            if (transport == null)
            {
                return;
            }

            m_Transports.Remove(transport);
        }

        public void ClearLogTransports()
        {
            m_Transports.Clear();
        }

        public void AddAnalyticsSink(IAnalyticsSink sink)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            if (m_AnalyticsSinks.Contains(sink))
            {
                return;
            }

            m_AnalyticsSinks.Add(sink);
        }

        public void RemoveAnalyticsSink(IAnalyticsSink sink)
        {
            if (sink == null)
            {
                return;
            }

            m_AnalyticsSinks.Remove(sink);
        }

        public void ClearAnalyticsSinks()
        {
            m_AnalyticsSinks.Clear();
        }

        public void RegisterProfile(ProfileHandle handle)
        {
            Profiles.RedactionEnabled = Settings.RedactionEnabled;
            Profiles.Register(handle);
        }

        public bool UnregisterProfile(ProfileHandle handle)
        {
            return Profiles.Unregister(handle);
        }

        public void Track(string name, IReadOnlyDictionary<string, object> properties = null)
        {
            TrackAsync(name, properties).Forget();
        }

        public async UniTask TrackAsync(string name, IReadOnlyDictionary<string, object> properties = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Analytics event name cannot be empty.", nameof(name));
            }

            if (!Enabled || !Settings.AnalyticsEnabled)
            {
                return;
            }

            var analyticsEvent = new AnalyticsEvent(
                name,
                DateTimeOffset.Now,
                RedactProperties(properties));

            var sinks = m_AnalyticsSinks.ToArray();
            foreach (var sink in sinks)
            {
                try
                {
                    await sink.TrackAsync(analyticsEvent);
                }
                catch (Exception exception)
                {
                    LastAnalyticsException = exception;
                }
            }
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
            m_CategoryStates[NormalizeCategory(category)] = enabled;
        }

        public bool IsCategoryEnabled(string category)
        {
            var normalizedCategory = NormalizeCategory(category);
            return !m_CategoryStates.TryGetValue(normalizedCategory, out var enabled) || enabled;
        }

        private bool ShouldWrite(LogLevel level, string category)
        {
            return Enabled &&
                   MinimumLevel != LogLevel.Off &&
                   level >= MinimumLevel &&
                   IsCategoryEnabled(category);
        }

        private void Write(DebugLogRecord record)
        {
            var sinks = m_Sinks.ToArray();
            foreach (var sink in sinks)
            {
                try
                {
                    sink.Write(record);
                }
                catch (Exception exception)
                {
                    LastSinkException = exception;
                }
            }
        }

        private void Send(DebugLogRecord record)
        {
            var transports = m_Transports.ToArray();
            foreach (var transport in transports)
            {
                SendAsync(transport, record).Forget();
            }
        }

        private async UniTaskVoid SendAsync(IDebugLogTransport transport, DebugLogRecord record)
        {
            try
            {
                await transport.SendAsync(record);
            }
            catch (Exception exception)
            {
                LastTransportException = exception;
            }
        }

        internal void UpdateMetrics(float deltaTime)
        {
            if (!Enabled)
            {
                return;
            }

            Profiles.RedactionEnabled = Settings.RedactionEnabled;
            Profiles.Refresh(deltaTime);
            if (!Settings.MetricsEnabled)
            {
                return;
            }

            m_MetricElapsed += deltaTime;
            if (m_MetricElapsed < Settings.MetricSampleInterval)
            {
                return;
            }

            var frameTimeMs = deltaTime * 1000f;
            var fps = deltaTime > 0f ? 1f / deltaTime : 0f;
            Metrics = new DebugMetricSnapshot(
                fps,
                frameTimeMs,
                GC.GetTotalMemory(false));
            m_MetricElapsed = 0f;
        }

        internal void DrawGui()
        {
            if (!Enabled || !ConsoleVisible)
            {
                return;
            }

            var width = Mathf.Max(1f, Screen.width);
            var height = Mathf.Max(1f, Screen.height);
            GUILayout.BeginArea(new Rect(0f, 0f, width, height), GUI.skin.box);
            GUILayout.BeginHorizontal(EditorToolbarStyle());
            GUILayout.Label("Debug Console", GUILayout.Width(140f));
            Console.SelectedTab = GUILayout.Toolbar(Console.SelectedTab, GuiTabs);
            if (GUILayout.Button("Close", GUILayout.Width(72f)))
            {
                ConsoleVisible = false;
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }

            GUILayout.EndHorizontal();
            switch (Console.SelectedTab)
            {
                case 0:
                    DrawLogsTab();
                    break;
                case 1:
                    DrawProfilesTab();
                    break;
                case 2:
                    DrawTimersTab();
                    break;
                case 3:
                    DrawToolsTab();
                    break;
                case 4:
                    DrawSettingsTab();
                    break;
            }

            GUILayout.EndArea();
        }

        private void DrawLogsTab()
        {
            m_LogScroll = GUILayout.BeginScrollView(m_LogScroll);
            foreach (var entry in Logs.Snapshot())
            {
                GUILayout.Label($"#{entry.Sequence} F{entry.FrameCount} T{entry.TimerTick} [{entry.Level}] [{entry.Category}] {entry.Message}");
            }

            GUILayout.EndScrollView();
        }

        private void DrawProfilesTab()
        {
            m_ProfileScroll = GUILayout.BeginScrollView(m_ProfileScroll);
            var tables = Profiles.Snapshot();
            if (tables.Count == 0)
            {
                GUILayout.Label("No profiles registered.");
                GUILayout.EndScrollView();
                return;
            }

            foreach (var table in tables)
            {
                GUILayout.Label($"{table.Category}/{table.Name}");
                if (table.HasError)
                {
                    GUILayout.Label($"Error: {table.Exception.Message}");
                    GUILayout.Space(8f);
                    continue;
                }

                DrawProfileTable(table);
                GUILayout.Space(8f);
            }

            GUILayout.EndScrollView();
        }

        private static void DrawProfileTable(ProfileTable table)
        {
            GUILayout.BeginHorizontal();
            foreach (var column in table.Columns)
            {
                GUILayout.Label(column.Name, GUILayout.Width(140f));
            }

            GUILayout.EndHorizontal();
            foreach (var row in table.Rows)
            {
                GUILayout.BeginHorizontal();
                foreach (var column in table.Columns)
                {
                    row.Values.TryGetValue(column.Key, out var value);
                    GUILayout.Label(value?.ToString() ?? string.Empty, GUILayout.Width(140f));
                }

                GUILayout.EndHorizontal();
            }
        }

        private void DrawTimersTab()
        {
            m_TimerScroll = GUILayout.BeginScrollView(m_TimerScroll);
            if (!App.TryGetRegistered<TimerModule>(out var timer))
            {
                GUILayout.Label("TimerModule is not registered.");
                GUILayout.EndScrollView();
                return;
            }

            var snapshot = timer.Snapshot();
            GUILayout.Label($"Tick: {snapshot.Tick}");
            GUILayout.Label($"Time: {snapshot.Time:0.000}s");
            GUILayout.Label($"Delta: {snapshot.DeltaTime:0.000}s / Unscaled: {snapshot.UnscaledDeltaTime:0.000}s");
            DrawTimerHandles("Delay", snapshot.Delays);
            DrawTimerHandles("Countdown", snapshot.Countdowns);
            DrawTimerHandles("Interval", snapshot.Intervals);
            GUILayout.EndScrollView();
        }

        private static void DrawTimerHandles<T>(string title, IReadOnlyList<T> handles) where T : TimerHandle
        {
            GUILayout.Label($"{title}: {handles.Count}");
            foreach (var handle in handles)
            {
                GUILayout.Label($"{handle.Tag ?? string.Empty} remaining={handle.Remaining:0.000}s progress={handle.Progress:0.00} next={handle.NextFireTime:0.000} paused={handle.IsPaused}");
            }
        }

        private void DrawToolsTab()
        {
            m_ToolScroll = GUILayout.BeginScrollView(m_ToolScroll);
            DrawCommandTab();
            GUILayout.EndScrollView();
        }

        private void DrawSettingsTab()
        {
            m_SettingsScroll = GUILayout.BeginScrollView(m_SettingsScroll);
            GUILayout.Label($"Enabled: {Enabled}");
            GUILayout.Label($"Minimum Level: {MinimumLevel}");
            GUILayout.Label($"Console: {ConsoleVisible}");
            GUILayout.Label($"Console Enabled: {Settings.ConsoleEnabled}");
            GUILayout.Label($"Unity Log Capture: {Settings.UnityLogCaptureEnabled}");
            GUILayout.Label($"Command: {Settings.CommandEnabled}");
            GUILayout.Label($"Redaction: {Settings.RedactionEnabled}");
            GUILayout.Label($"Log Capacity: {Settings.LogCapacity}");
            GUILayout.Label($"FPS: {Metrics.Fps:0.0}");
            GUILayout.Label($"Frame: {Metrics.FrameTimeMs:0.00}ms");
            GUILayout.Label($"Managed: {Metrics.ManagedMemoryBytes / 1024f / 1024f:0.0}MB");
            GUILayout.Label(Metrics.GraphicsMemoryBytes.HasValue
                ? $"Graphics: {Metrics.GraphicsMemoryBytes.Value / 1024f / 1024f:0.0}MB"
                : "Graphics: unavailable");
            GUILayout.Label(Metrics.GpuFrameTimeMs.HasValue
                ? $"GPU Frame: {Metrics.GpuFrameTimeMs.Value:0.00}ms"
                : "GPU Frame: unavailable");
            GUILayout.Label($"Analytics Sinks: {m_AnalyticsSinks.Count}");
            GUILayout.Label(LastAnalyticsException == null
                ? "Last Analytics Error: none"
                : $"Last Analytics Error: {LastAnalyticsException.Message}");
            GUILayout.Label(LastSinkException == null
                ? "Last Sink Error: none"
                : $"Last Sink Error: {LastSinkException.Message}");
            GUILayout.Label(LastTransportException == null
                ? "Last Transport Error: none"
                : $"Last Transport Error: {LastTransportException.Message}");
            GUILayout.EndScrollView();
        }

        private void DrawCommandTab()
        {
            GUILayout.Label(Settings.CommandEnabled ? "Command input enabled." : "Command input disabled.");
            GUILayout.BeginHorizontal();
            m_CommandLine = GUILayout.TextField(m_CommandLine);
            if (GUILayout.Button("Run", GUILayout.Width(60f)))
            {
                ExecuteGuiCommandAsync(m_CommandLine).Forget();
            }

            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(m_CommandMessage))
            {
                GUILayout.Label(m_CommandMessage);
            }
        }

        private async UniTaskVoid ExecuteGuiCommandAsync(string commandLine)
        {
            var result = await ExecuteCommandAsync(commandLine);
            m_CommandMessage = result.Message;
            Info(result.Message, "Command");
        }

        private static GUIStyle EditorToolbarStyle()
        {
            return GUI.skin.box;
        }

        private static IReadOnlyDictionary<string, object> RedactProperties(IReadOnlyDictionary<string, object> properties)
        {
            var result = new Dictionary<string, object>();
            if (properties == null)
            {
                return result;
            }

            foreach (var property in properties)
            {
                result[property.Key] = DebugRedactionUtility.RedactValue(property.Key, property.Value);
            }

            return result;
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
            if (!Enabled || !Settings.UnityLogCaptureEnabled || UnityConsoleLogSink.IsWriting)
            {
                return;
            }

            WriteLog(
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

        private string RedactLogText(string value)
        {
            return Settings.RedactionEnabled ? DebugRedactionUtility.Redact(value) : value;
        }

        private static void ValidateLevel(LogLevel level)
        {
            if (level < LogLevel.Trace || level > LogLevel.Fatal)
            {
                throw new ArgumentException("Log level must be Trace, Debug, Info, Warning, Error or Fatal.", nameof(level));
            }
        }

        private static string NormalizeCategory(string category)
        {
            return string.IsNullOrWhiteSpace(category) ? DefaultCategory : category;
        }
    }
}
