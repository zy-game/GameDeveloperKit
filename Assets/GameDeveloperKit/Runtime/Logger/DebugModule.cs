using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Command;
using UnityEngine;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;
using Object = UnityEngine.Object;

namespace GameDeveloperKit.Logger
{
    public class DebugModule : GameModuleBase
    {
        private const string DefaultCategory = "Default";
        private const string RootName = "GameDeveloperKit.Debug";
        private const string SessionMarkerFileName = "game-developer-kit-debug-session.marker";

        private static readonly string[] GuiTabs =
        {
            "Logs",
            "Metrics",
            "Upload",
            "Analytics",
            "Command",
        };

        private readonly Dictionary<string, bool> m_CategoryStates = new Dictionary<string, bool>();
        private readonly List<IAnalyticsSink> m_AnalyticsSinks = new List<IAnalyticsSink>();
        private readonly List<ICrashLogProvider> m_CrashLogProviders = new List<ICrashLogProvider>();
        private readonly List<CrashLogArtifact> m_SessionCrashLogs = new List<CrashLogArtifact>();
        private readonly List<ILogSink> m_Sinks = new List<ILogSink>();
        private GameObject m_Root;
        private DebugGuiDriver m_GuiDriver;
        private IDebugUploader m_Uploader;
        private bool m_IsUploading;
        private float m_MetricElapsed;
        private string m_CommandLine = string.Empty;
        private string m_CommandMessage = string.Empty;
        private bool m_OverlayVisible;
        private int m_GuiTab;

        public bool Enabled { get; set; } = true;

        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

        public DebugSettings Settings { get; } = new DebugSettings();

        public DebugLogBuffer Logs { get; } = new DebugLogBuffer();

        public DebugMetricSnapshot Metrics { get; private set; }

        public bool OverlayVisible
        {
            get => Enabled && m_OverlayVisible;
            set => m_OverlayVisible = value;
        }

        internal Exception LastSinkException { get; private set; }

        internal Exception LastAnalyticsException { get; private set; }

        internal DebugUploadResult LastUploadResult { get; private set; }

        public override UniTask Startup()
        {
            Enabled = true;
            MinimumLevel = LogLevel.Info;
            m_CategoryStates.Clear();
            m_AnalyticsSinks.Clear();
            m_CrashLogProviders.Clear();
            m_SessionCrashLogs.Clear();
            LastSinkException = null;
            LastAnalyticsException = null;
            LastUploadResult = default;
            ClearSinks();
            AddSink(new UnityConsoleLogSink());
            Logs.SetCapacity(Settings.LogCapacity);
            OverlayVisible = Settings.OverlayEnabled;
            InitializeSessionMarker();
            CreateGuiDriver();
            return UniTask.CompletedTask;
        }

        public override UniTask Shutdown()
        {
            MarkCleanExit();
            ClearSinks();
            m_CategoryStates.Clear();
            m_AnalyticsSinks.Clear();
            m_CrashLogProviders.Clear();
            m_SessionCrashLogs.Clear();
            m_Uploader = null;
            m_IsUploading = false;
            LastSinkException = null;
            LastAnalyticsException = null;
            LastUploadResult = default;
            Enabled = true;
            MinimumLevel = LogLevel.Info;
            OverlayVisible = false;
            Logs.Clear();
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
            ValidateLevel(level);
            var normalizedCategory = NormalizeCategory(category);
            if (!ShouldWrite(level, normalizedCategory))
            {
                return;
            }

            var entry = new LogEntry(
                DateTimeOffset.Now,
                level,
                normalizedCategory,
                message ?? string.Empty,
                exception,
                context);

            Logs.Append(entry);
            Write(entry);
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

        public void AddCrashLogProvider(ICrashLogProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            if (m_CrashLogProviders.Contains(provider))
            {
                return;
            }

            m_CrashLogProviders.Add(provider);
        }

        public void RemoveCrashLogProvider(ICrashLogProvider provider)
        {
            if (provider == null)
            {
                return;
            }

            m_CrashLogProviders.Remove(provider);
        }

        public void SetUploader(IDebugUploader uploader)
        {
            m_Uploader = uploader;
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

        public async UniTask<DebugUploadResult> UploadAsync(DebugUploadOptions options = null)
        {
            if (!Enabled || !Settings.UploadEnabled)
            {
                LastUploadResult = DebugUploadResult.DisabledResult("Debug upload is disabled.");
                return LastUploadResult;
            }

            if (m_Uploader == null)
            {
                LastUploadResult = DebugUploadResult.Failed("Debug uploader is not registered.");
                return LastUploadResult;
            }

            if (m_IsUploading)
            {
                LastUploadResult = DebugUploadResult.Failed("Debug upload is already running.");
                return LastUploadResult;
            }

            m_IsUploading = true;
            DebugBundle bundle = null;
            try
            {
                bundle = await BuildBundleAsync();
                LastUploadResult = await m_Uploader.UploadAsync(bundle);
                return LastUploadResult;
            }
            catch (Exception exception)
            {
                LastUploadResult = DebugUploadResult.Failed("Debug upload failed.", exception, bundle);
                return LastUploadResult;
            }
            finally
            {
                m_IsUploading = false;
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

            if (!Super.TryGetRegistered<CommandModule>(out var commandModule))
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

        private void Write(LogEntry entry)
        {
            var sinks = m_Sinks.ToArray();
            foreach (var sink in sinks)
            {
                try
                {
                    sink.Write(entry);
                }
                catch (Exception exception)
                {
                    LastSinkException = exception;
                }
            }
        }

        internal void UpdateMetrics(float deltaTime)
        {
            if (!Enabled || !Settings.MetricsEnabled)
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
            if (!Enabled || !OverlayVisible)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(12f, 12f, 520f, 560f), GUI.skin.box);
            GUILayout.Label("Debug");
            m_GuiTab = GUILayout.Toolbar(m_GuiTab, GuiTabs);
            switch (m_GuiTab)
            {
                case 0:
                    DrawLogsTab();
                    break;
                case 1:
                    DrawMetricsTab();
                    break;
                case 2:
                    DrawUploadTab();
                    break;
                case 3:
                    DrawAnalyticsTab();
                    break;
                case 4:
                    DrawCommandTab();
                    break;
            }

            GUILayout.EndArea();
        }

        private void DrawLogsTab()
        {
            foreach (var entry in Logs.Snapshot())
            {
                GUILayout.Label($"[{entry.Level}] [{entry.Category}] {entry.Message}");
            }
        }

        private void DrawMetricsTab()
        {
            GUILayout.Label($"FPS: {Metrics.Fps:0.0}");
            GUILayout.Label($"Frame: {Metrics.FrameTimeMs:0.00}ms");
            GUILayout.Label($"Managed: {Metrics.ManagedMemoryBytes / 1024f / 1024f:0.0}MB");
            GUILayout.Label(Metrics.GraphicsMemoryBytes.HasValue
                ? $"Graphics: {Metrics.GraphicsMemoryBytes.Value / 1024f / 1024f:0.0}MB"
                : "Graphics: unavailable");
            GUILayout.Label(Metrics.GpuFrameTimeMs.HasValue
                ? $"GPU Frame: {Metrics.GpuFrameTimeMs.Value:0.00}ms"
                : "GPU Frame: unavailable");
        }

        private void DrawUploadTab()
        {
            if (GUILayout.Button("Upload"))
            {
                UploadAsync().Forget();
            }

            GUILayout.Label(LastUploadResult.Message ?? "Upload idle.");
        }

        private void DrawAnalyticsTab()
        {
            GUILayout.Label($"Sinks: {m_AnalyticsSinks.Count}");
            GUILayout.Label(LastAnalyticsException == null
                ? "Last error: none"
                : $"Last error: {LastAnalyticsException.Message}");
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

        private async UniTask<DebugBundle> BuildBundleAsync()
        {
            var crashLogs = new List<CrashLogArtifact>();
            foreach (var crashLog in m_SessionCrashLogs)
            {
                crashLogs.Add(RedactCrashLog(crashLog));
            }

            var providers = m_CrashLogProviders.ToArray();
            foreach (var provider in providers)
            {
                try
                {
                    var artifacts = await provider.CollectAsync();
                    if (artifacts == null)
                    {
                        continue;
                    }

                    foreach (var artifact in artifacts)
                    {
                        crashLogs.Add(RedactCrashLog(artifact));
                    }
                }
                catch (Exception exception)
                {
                    crashLogs.Add(RedactCrashLog(new CrashLogArtifact(provider.GetType().Name, exception.Message, false)));
                }
            }

            return new DebugBundle(
                Guid.NewGuid().ToString("N"),
                DateTimeOffset.Now,
                RedactLogs(Logs.Snapshot()),
                Metrics,
                crashLogs,
                BuildEnvironment());
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

        private static CrashLogArtifact RedactCrashLog(CrashLogArtifact artifact)
        {
            return new CrashLogArtifact(
                DebugRedactionUtility.Redact(artifact.Name),
                DebugRedactionUtility.Redact(artifact.Content),
                artifact.IsAvailable);
        }

        private static IReadOnlyList<LogEntry> RedactLogs(IReadOnlyList<LogEntry> entries)
        {
            var result = new List<LogEntry>();
            foreach (var entry in entries)
            {
                result.Add(new LogEntry(
                    entry.Timestamp,
                    entry.Level,
                    DebugRedactionUtility.Redact(entry.Category),
                    DebugRedactionUtility.Redact(entry.Message),
                    entry.Exception,
                    entry.Context));
            }

            return result;
        }

        private static IReadOnlyDictionary<string, string> BuildEnvironment()
        {
            return new Dictionary<string, string>
            {
                { "platform", Application.platform.ToString() },
                { "unityVersion", Application.unityVersion },
                { "productName", Application.productName },
                { "version", Application.version },
            };
        }

        private void InitializeSessionMarker()
        {
            try
            {
                var markerPath = GetSessionMarkerPath();
                if (IOFile.Exists(markerPath))
                {
                    m_SessionCrashLogs.Add(new CrashLogArtifact(
                        "previous-session",
                        "Previous debug session did not cleanly shut down.",
                        true));
                }

                var directory = IOPath.GetDirectoryName(markerPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    IODirectory.CreateDirectory(directory);
                }

                IOFile.WriteAllText(markerPath, DateTimeOffset.Now.ToString("O"));
            }
            catch (Exception exception)
            {
                m_SessionCrashLogs.Add(new CrashLogArtifact("session-marker", exception.Message, false));
            }
        }

        private void MarkCleanExit()
        {
            try
            {
                var markerPath = GetSessionMarkerPath();
                if (IOFile.Exists(markerPath))
                {
                    IOFile.Delete(markerPath);
                }
            }
            catch
            {
            }
        }

        private static string GetSessionMarkerPath()
        {
            return IOPath.Combine(Application.persistentDataPath, SessionMarkerFileName);
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