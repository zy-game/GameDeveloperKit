using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Command;
using GameDeveloperKit.Logger;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    public sealed class LoggerModuleTests : RuntimeTestBase
    {
        [SetUp]
        public void SetUp()
        {
            try
            {
                Super.Register<LoggerModule>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }

            Super.Logger.ClearSinks();
            Super.Logger.ClearAnalyticsSinks();
            Super.Logger.SetUploader(null);
            Super.Logger.Logs.Clear();
            Super.Logger.Enabled = true;
            Super.Logger.MinimumLevel = LogLevel.Trace;
            Super.Logger.Settings.AnalyticsEnabled = true;
            Super.Logger.Settings.MetricsEnabled = true;
            Super.Logger.OverlayVisible = false;
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Super.Unregister<CommandModule>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }

            try
            {
                Super.Unregister<LoggerModule>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }
        }

        [Test]
        public void Register_WhenLoggerModuleIsRegistered_ReturnsLogger()
        {
            Assert.IsNotNull(Super.Logger);
        }

        [Test]
        public void Register_WhenLoggerModuleIsRegistered_ReturnsDebugFacade()
        {
            Assert.IsNotNull(Super.Debug);
            Assert.AreSame(Super.Logger, Super.Debug);
        }

        [Test]
        public void Register_WhenDebugModuleIsRegistered_ReturnsDebug()
        {
            Super.Unregister<LoggerModule>().GetAwaiter().GetResult();
            try
            {
                Super.Register<DebugModule>().GetAwaiter().GetResult();

                Assert.IsNotNull(Super.Debug);
            }
            finally
            {
                try
                {
                    Super.Unregister<DebugModule>().GetAwaiter().GetResult();
                }
                catch (GameException)
                {
                }

                Super.Register<LoggerModule>().GetAwaiter().GetResult();
            }
        }

        [Test]
        public void Log_WhenBelowMinimumLevel_DoesNotWrite()
        {
            var sink = new CollectingSink();
            Super.Logger.AddSink(sink);
            Super.Logger.MinimumLevel = LogLevel.Warning;

            Super.Logger.Info("hidden", "Core");
            Super.Logger.Warning("visible", "Core");

            Assert.AreEqual(1, sink.Entries.Count);
            Assert.AreEqual(LogLevel.Warning, sink.Entries[0].Level);
        }

        [Test]
        public void Log_WhenNoSinkRegistered_AppendsToDebugBuffer()
        {
            Super.Logger.ClearSinks();

            Super.Debug.Error("boom", "Core");

            var entries = Super.Debug.Logs.Snapshot(new DebugLogQuery(LogLevel.Error, "Core"));
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("boom", entries[0].Message);
        }

        [Test]
        public void DebugLogBuffer_WhenCapacityExceeded_KeepsRecentEntriesAndQueries()
        {
            var buffer = new DebugLogBuffer(2);
            buffer.Append(CreateEntry(LogLevel.Info, "Core", "old"));
            buffer.Append(CreateEntry(LogLevel.Warning, "Resource", "middle"));
            buffer.Append(CreateEntry(LogLevel.Error, "Core", "new"));

            var all = buffer.Snapshot();
            var coreErrors = buffer.Snapshot(new DebugLogQuery(LogLevel.Error, "Core"));

            Assert.AreEqual(2, all.Count);
            Assert.AreEqual("middle", all[0].Message);
            Assert.AreEqual("new", all[1].Message);
            Assert.AreEqual(1, coreErrors.Count);
            Assert.AreEqual("new", coreErrors[0].Message);
        }

        [Test]
        public void Log_WhenCategoryDisabled_DoesNotWriteUntilEnabled()
        {
            var sink = new CollectingSink();
            Super.Logger.AddSink(sink);

            Super.Logger.SetCategoryEnabled("Resource", false);
            Super.Logger.Error("hidden", "Resource");
            Super.Logger.SetCategoryEnabled("Resource", true);
            Super.Logger.Error("visible", "Resource");

            Assert.AreEqual(1, sink.Entries.Count);
            Assert.AreEqual("Resource", sink.Entries[0].Category);
            Assert.AreEqual("visible", sink.Entries[0].Message);
        }

        [Test]
        public void AddSink_WhenSameInstanceAddedTwice_WritesOnce()
        {
            var sink = new CollectingSink();
            Super.Logger.AddSink(sink);
            Super.Logger.AddSink(sink);

            Super.Logger.Error("once", "Core");

            Assert.AreEqual(1, sink.Entries.Count);
        }

        [Test]
        public void RemoveSink_WhenSinkRegistered_StopsWriting()
        {
            var sink = new CollectingSink();
            Super.Logger.AddSink(sink);
            Super.Logger.RemoveSink(sink);

            Super.Logger.Error("hidden", "Core");

            Assert.AreEqual(0, sink.Entries.Count);
        }

        [Test]
        public void Error_WhenExceptionProvided_PreservesException()
        {
            var sink = new CollectingSink();
            var exception = new InvalidOperationException("load failed");
            Super.Logger.AddSink(sink);

            Super.Logger.Error(exception, "load failed", "Resource");

            Assert.AreEqual(1, sink.Entries.Count);
            Assert.AreSame(exception, sink.Entries[0].Exception);
            Assert.AreEqual("load failed", sink.Entries[0].Message);
            Assert.AreEqual("Resource", sink.Entries[0].Category);
        }

        [Test]
        public void Info_WhenMessageAndCategoryAreNull_NormalizesEntry()
        {
            var sink = new CollectingSink();
            Super.Logger.AddSink(sink);

            Super.Logger.Info(null, null);

            Assert.AreEqual(1, sink.Entries.Count);
            Assert.AreEqual(string.Empty, sink.Entries[0].Message);
            Assert.AreEqual("Default", sink.Entries[0].Category);
        }

        [Test]
        public void AddSink_WhenSinkIsNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Super.Logger.AddSink(null));
        }

        [Test]
        public void Log_WhenLevelIsOff_Throws()
        {
            Assert.Throws<ArgumentException>(() => Super.Logger.Log(LogLevel.Off, "off"));
        }

        [Test]
        public void SinkException_DoesNotStopOtherSinks()
        {
            var exception = new InvalidOperationException("sink failed");
            var throwingSink = new ThrowingSink(exception);
            var collectingSink = new CollectingSink();
            Super.Logger.AddSink(throwingSink);
            Super.Logger.AddSink(collectingSink);

            Assert.DoesNotThrow(() => Super.Logger.Error("still visible", "Core"));

            Assert.AreSame(exception, Super.Logger.LastSinkException);
            Assert.AreEqual(1, collectingSink.Entries.Count);
        }

        [Test]
        public void UpdateMetrics_WhenSampleIntervalReached_StoresSnapshot()
        {
            Super.Debug.Settings.MetricSampleInterval = 0.1f;
            Super.Debug.UpdateMetrics(0.2f);

            Assert.AreEqual(5f, Super.Debug.Metrics.Fps, 0.001f);
            Assert.AreEqual(200f, Super.Debug.Metrics.FrameTimeMs, 0.001f);
            Assert.Greater(Super.Debug.Metrics.ManagedMemoryBytes, 0L);
            Assert.IsFalse(Super.Debug.Metrics.GraphicsMemoryBytes.HasValue);
            Assert.IsFalse(Super.Debug.Metrics.GpuFrameTimeMs.HasValue);
        }

        [Test]
        public void OverlayVisible_WhenModuleDisabled_ReturnsFalse()
        {
            Super.Debug.OverlayVisible = true;

            Super.Debug.Enabled = false;

            Assert.IsFalse(Super.Debug.OverlayVisible);
            Assert.DoesNotThrow(() => Super.Debug.DrawGui());
        }

        [Test]
        public void TrackAsync_WhenSinkRegistered_RedactsAndDeliversAnalyticsEvent()
        {
            var sink = new CollectingAnalyticsSink();
            Super.Debug.AddAnalyticsSink(sink);
            var properties = new Dictionary<string, object>
            {
                { "stage", "intro" },
                { "token", "raw-token-value" },
            };

            Super.Debug.TrackAsync("stage_start", properties).GetAwaiter().GetResult();

            Assert.AreEqual(1, sink.Events.Count);
            Assert.AreEqual("stage_start", sink.Events[0].Name);
            Assert.AreEqual("intro", sink.Events[0].Properties["stage"]);
            Assert.AreEqual("[REDACTED]", sink.Events[0].Properties["token"]);
        }

        [Test]
        public void TrackAsync_WhenSinkThrows_DoesNotThrowAndRecordsException()
        {
            var exception = new InvalidOperationException("analytics failed");
            var collectingSink = new CollectingAnalyticsSink();
            Super.Debug.AddAnalyticsSink(new ThrowingAnalyticsSink(exception));
            Super.Debug.AddAnalyticsSink(collectingSink);

            Assert.DoesNotThrow(() => Super.Debug.TrackAsync("stage_start").GetAwaiter().GetResult());

            Assert.AreSame(exception, Super.Debug.LastAnalyticsException);
            Assert.AreEqual(1, collectingSink.Events.Count);
        }

        [Test]
        public void UploadAsync_WhenUploaderSucceeds_BuildsRedactedBundle()
        {
            Super.Debug.Settings.UploadEnabled = true;
            Super.Debug.Logs.Clear();
            Super.Debug.Error("token=abc", "Core");
            Super.Debug.AddCrashLogProvider(new StaticCrashLogProvider(new[]
            {
                new CrashLogArtifact("Player.log", "secret crash text"),
            }));
            var uploader = new CapturingUploader();
            Super.Debug.SetUploader(uploader);

            var result = Super.Debug.UploadAsync().GetAwaiter().GetResult();

            Assert.IsTrue(result.Succeeded);
            Assert.IsNotNull(uploader.Bundle);
            Assert.AreEqual("[REDACTED]", uploader.Bundle.Logs[0].Message);
            Assert.AreEqual("[REDACTED]", uploader.Bundle.CrashLogs[uploader.Bundle.CrashLogs.Count - 1].Content);
            Assert.IsTrue(uploader.Bundle.Environment.ContainsKey("platform"));
        }

        [Test]
        public void UploadAsync_WhenUploaderThrows_ReturnsFailedResult()
        {
            var exception = new InvalidOperationException("upload failed");
            Super.Debug.Settings.UploadEnabled = true;
            Super.Debug.SetUploader(new ThrowingUploader(exception));

            var result = Super.Debug.UploadAsync().GetAwaiter().GetResult();

            Assert.IsFalse(result.Succeeded);
            Assert.AreSame(exception, result.Exception);
        }

        [Test]
        public void UploadAsync_WhenPreviousSessionWasNotClean_IncludesCrashCandidate()
        {
            var first = new DebugModule();
            var second = new DebugModule();
            try
            {
                first.Startup().GetAwaiter().GetResult();
                second.Settings.UploadEnabled = true;
                second.Startup().GetAwaiter().GetResult();
                var uploader = new CapturingUploader();
                second.SetUploader(uploader);

                var result = second.UploadAsync().GetAwaiter().GetResult();

                Assert.IsTrue(result.Succeeded);
                Assert.IsTrue(ContainsCrashLog(uploader.Bundle, "previous-session"));
            }
            finally
            {
                second.Shutdown().GetAwaiter().GetResult();
                first.Shutdown().GetAwaiter().GetResult();
            }
        }

        [Test]
        public void ExecuteCommandAsync_WhenCommandLineValid_CallsCommandModule()
        {
            Super.Register<CommandModule>().GetAwaiter().GetResult();
            Super.Command.Register("GM-ADD-ITEM", args => new DebugPanelCommand(args[0].ToString(), Convert.ToInt32(args[1])));
            Super.Debug.Settings.CommandEnabled = true;
            DebugPanelCommand.Reset();

            var result = Super.Debug.ExecuteCommandAsync("GM-ADD-ITEM sword 3").GetAwaiter().GetResult();

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("sword", DebugPanelCommand.LastItemId);
            Assert.AreEqual(3, DebugPanelCommand.LastCount);
        }

        [Test]
        public void DisabledModule_WhenUploadCommandAndOverlayTriggered_ReturnsDisabled()
        {
            Super.Debug.Settings.CommandEnabled = true;
            Super.Debug.Settings.UploadEnabled = true;
            Super.Debug.OverlayVisible = true;
            Super.Debug.Enabled = false;

            var upload = Super.Debug.UploadAsync().GetAwaiter().GetResult();
            var command = Super.Debug.ExecuteCommandAsync("GM-ADD-ITEM sword 3").GetAwaiter().GetResult();

            Assert.IsTrue(upload.Disabled);
            Assert.IsTrue(command.Disabled);
            Assert.IsFalse(Super.Debug.OverlayVisible);
        }

        [Test]
        public void DebugSettings_WhenValuesInvalid_Throws()
        {
            var settings = new DebugSettings();

            Assert.Throws<ArgumentException>(() => settings.LogCapacity = 0);
            Assert.Throws<ArgumentException>(() => settings.MetricSampleInterval = 0f);
        }

        [Test]
        public void DebugSettings_WhenCreated_UsesDebugBuildForDangerousDefaults()
        {
            var settings = new DebugSettings();

            Assert.AreEqual(UnityEngine.Debug.isDebugBuild, settings.CommandEnabled);
            Assert.AreEqual(UnityEngine.Debug.isDebugBuild, settings.OverlayEnabled);
            Assert.AreEqual(UnityEngine.Debug.isDebugBuild, settings.UploadEnabled);
        }

        [Test]
        public void Log_WhenSinkClearsSinksDuringWrite_UsesSnapshot()
        {
            var clearingSink = new ClearingSink(Super.Logger);
            var collectingSink = new CollectingSink();
            Super.Logger.AddSink(clearingSink);
            Super.Logger.AddSink(collectingSink);

            Super.Logger.Error("visible", "Core");
            Super.Logger.Error("hidden", "Core");

            Assert.AreEqual(1, collectingSink.Entries.Count);
            Assert.AreEqual("visible", collectingSink.Entries[0].Message);
        }

        [Test]
        public void Shutdown_ClearsSinksAndCategoryStates()
        {
            var sink = new CollectingSink();
            Super.Logger.AddSink(sink);
            Super.Logger.SetCategoryEnabled("Core", false);

            Super.Logger.Shutdown().GetAwaiter().GetResult();
            Super.Logger.Error("hidden", "Core");

            Assert.AreEqual(0, sink.Entries.Count);
            Assert.IsTrue(Super.Logger.IsCategoryEnabled("Core"));
        }

        private sealed class CollectingSink : ILogSink
        {
            public readonly List<LogEntry> Entries = new List<LogEntry>();

            public void Write(LogEntry entry)
            {
                Entries.Add(entry);
            }
        }

        private sealed class ThrowingSink : ILogSink
        {
            private readonly Exception m_Exception;

            public ThrowingSink(Exception exception)
            {
                m_Exception = exception;
            }

            public void Write(LogEntry entry)
            {
                throw m_Exception;
            }
        }

        private sealed class ClearingSink : ILogSink
        {
            private readonly LoggerModule m_Module;

            public ClearingSink(LoggerModule module)
            {
                m_Module = module;
            }

            public void Write(LogEntry entry)
            {
                m_Module.ClearSinks();
            }
        }

        private static LogEntry CreateEntry(LogLevel level, string category, string message)
        {
            return new LogEntry(DateTimeOffset.Now, level, category, message, null, null);
        }

        private static bool ContainsCrashLog(DebugBundle bundle, string name)
        {
            foreach (var crashLog in bundle.CrashLogs)
            {
                if (crashLog.Name == name)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class CollectingAnalyticsSink : IAnalyticsSink
        {
            public readonly List<AnalyticsEvent> Events = new List<AnalyticsEvent>();

            public UniTask TrackAsync(AnalyticsEvent analyticsEvent)
            {
                Events.Add(analyticsEvent);
                return UniTask.CompletedTask;
            }
        }

        private sealed class ThrowingAnalyticsSink : IAnalyticsSink
        {
            private readonly Exception m_Exception;

            public ThrowingAnalyticsSink(Exception exception)
            {
                m_Exception = exception;
            }

            public UniTask TrackAsync(AnalyticsEvent analyticsEvent)
            {
                throw m_Exception;
            }
        }

        private sealed class StaticCrashLogProvider : ICrashLogProvider
        {
            private readonly IReadOnlyList<CrashLogArtifact> m_Artifacts;

            public StaticCrashLogProvider(IReadOnlyList<CrashLogArtifact> artifacts)
            {
                m_Artifacts = artifacts;
            }

            public UniTask<IReadOnlyList<CrashLogArtifact>> CollectAsync()
            {
                return UniTask.FromResult(m_Artifacts);
            }
        }

        private sealed class CapturingUploader : IDebugUploader
        {
            public DebugBundle Bundle { get; private set; }

            public UniTask<DebugUploadResult> UploadAsync(DebugBundle bundle)
            {
                Bundle = bundle;
                return UniTask.FromResult(DebugUploadResult.Success(bundle, "ok"));
            }
        }

        private sealed class ThrowingUploader : IDebugUploader
        {
            private readonly Exception m_Exception;

            public ThrowingUploader(Exception exception)
            {
                m_Exception = exception;
            }

            public UniTask<DebugUploadResult> UploadAsync(DebugBundle bundle)
            {
                throw m_Exception;
            }
        }

        private sealed class DebugPanelCommand : CommandBase
        {
            private readonly string m_ItemId;
            private readonly int m_Count;

            public DebugPanelCommand(string itemId, int count)
            {
                m_ItemId = itemId;
                m_Count = count;
            }

            public static string LastItemId { get; private set; }

            public static int LastCount { get; private set; }

            public static void Reset()
            {
                LastItemId = null;
                LastCount = 0;
            }

            public override UniTask ExecuteAsync()
            {
                LastItemId = m_ItemId;
                LastCount = m_Count;
                return UniTask.CompletedTask;
            }

            public override UniTask UndoAsync()
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
