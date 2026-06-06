using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Command;
using GameDeveloperKit.Logger;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class DebugModuleTests : RuntimeTestBase
    {
        [SetUp]
        public void SetUp()
        {
            try
            {
                App.Register<DebugModule>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }

            App.Debug.ClearSinks();
            App.Debug.ClearAnalyticsSinks();
            App.Debug.Logs.Clear();
            App.Debug.Enabled = true;
            App.Debug.MinimumLevel = LogLevel.Trace;
            App.Debug.Settings.AnalyticsEnabled = true;
            App.Debug.Settings.MetricsEnabled = true;
            App.Debug.Settings.RedactionEnabled = true;
            App.Debug.Settings.UnityLogCaptureEnabled = false;
            App.Debug.ClearLogTransports();
            App.Debug.OverlayVisible = false;
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                App.Unregister<CommandModule>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }

            try
            {
                App.Unregister<DebugModule>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }
        }

        [Test]
        public void Register_WhenDebugModuleIsRegistered_ReturnsDebug()
        {
            Assert.IsNotNull(App.Debug);
        }

        [Test]
        public void DebugModule_WhenInspected_HasNoRemovedTransferApi()
        {
            Assert.IsNull(typeof(DebugModule).GetMethod("Upload" + "Async"));
            Assert.IsNull(typeof(DebugModule).GetMethod("Set" + "Upload" + "er"));
        }

        [Test]
        public void Log_WhenBelowMinimumLevel_DoesNotWrite()
        {
            var sink = new CollectingSink();
            App.Debug.AddSink(sink);
            App.Debug.MinimumLevel = LogLevel.Warning;

            App.Debug.Info("hidden", "Core");
            App.Debug.Warning("visible", "Core");

            Assert.AreEqual(1, sink.Entries.Count);
            Assert.AreEqual(LogLevel.Warning, sink.Entries[0].Level);
        }

        [Test]
        public void Log_WhenNoSinkRegistered_AppendsToDebugBuffer()
        {
            App.Debug.ClearSinks();

            App.Debug.Error("boom", "Core");

            var entries = App.Debug.Logs.Snapshot(new DebugLogQuery(LogLevel.Error, "Core"));
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("boom", entries[0].Message);
            Assert.Greater(entries[0].Sequence, 0L);
            Assert.GreaterOrEqual(entries[0].FrameCount, 0);
            Assert.GreaterOrEqual(entries[0].TimerTick, 0L);
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
            App.Debug.AddSink(sink);

            App.Debug.SetCategoryEnabled("Resource", false);
            App.Debug.Error("hidden", "Resource");
            App.Debug.SetCategoryEnabled("Resource", true);
            App.Debug.Error("visible", "Resource");

            Assert.AreEqual(1, sink.Entries.Count);
            Assert.AreEqual("Resource", sink.Entries[0].Category);
            Assert.AreEqual("visible", sink.Entries[0].Message);
        }

        [Test]
        public void AddSink_WhenSameInstanceAddedTwice_WritesOnce()
        {
            var sink = new CollectingSink();
            App.Debug.AddSink(sink);
            App.Debug.AddSink(sink);

            App.Debug.Error("once", "Core");

            Assert.AreEqual(1, sink.Entries.Count);
        }

        [Test]
        public void RemoveSink_WhenSinkRegistered_StopsWriting()
        {
            var sink = new CollectingSink();
            App.Debug.AddSink(sink);
            App.Debug.RemoveSink(sink);

            App.Debug.Error("hidden", "Core");

            Assert.AreEqual(0, sink.Entries.Count);
        }

        [Test]
        public void Error_WhenExceptionProvided_PreservesException()
        {
            var sink = new CollectingSink();
            var exception = new InvalidOperationException("load failed");
            App.Debug.AddSink(sink);

            App.Debug.Error(exception, "load failed", "Resource");

            Assert.AreEqual(1, sink.Entries.Count);
            Assert.AreSame(exception, sink.Entries[0].Exception);
            Assert.AreEqual("load failed", sink.Entries[0].Message);
            Assert.AreEqual("Resource", sink.Entries[0].Category);
        }

        [Test]
        public void Log_WhenRedactionEnabled_RedactsSensitiveMessage()
        {
            var sink = new CollectingSink();
            App.Debug.AddSink(sink);

            App.Debug.Info("token=raw-token-value", "Core");

            Assert.AreEqual(1, sink.Entries.Count);
            Assert.AreEqual("[REDACTED]", sink.Entries[0].Message);
        }

        [Test]
        public void Info_WhenMessageAndCategoryAreNull_NormalizesEntry()
        {
            var sink = new CollectingSink();
            App.Debug.AddSink(sink);

            App.Debug.Info(null, null);

            Assert.AreEqual(1, sink.Entries.Count);
            Assert.AreEqual(string.Empty, sink.Entries[0].Message);
            Assert.AreEqual("Default", sink.Entries[0].Category);
        }

        [Test]
        public void AddSink_WhenSinkIsNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => App.Debug.AddSink(null));
        }

        [Test]
        public void Log_WhenLevelIsOff_Throws()
        {
            Assert.Throws<ArgumentException>(() => App.Debug.Log(LogLevel.Off, "off"));
        }

        [Test]
        public void SinkException_DoesNotStopOtherSinks()
        {
            var exception = new InvalidOperationException("sink failed");
            var throwingSink = new ThrowingSink(exception);
            var collectingSink = new CollectingSink();
            App.Debug.AddSink(throwingSink);
            App.Debug.AddSink(collectingSink);

            Assert.DoesNotThrow(() => App.Debug.Error("still visible", "Core"));

            Assert.AreSame(exception, App.Debug.LastSinkException);
            Assert.AreEqual(1, collectingSink.Entries.Count);
        }

        [Test]
        public void LogTransport_WhenRegistered_ReceivesRecordsAndIsolatesFailures()
        {
            var exception = new InvalidOperationException("transport failed");
            var collectingTransport = new CollectingTransport();
            App.Debug.AddLogTransport(new ThrowingTransport(exception));
            App.Debug.AddLogTransport(collectingTransport);

            Assert.DoesNotThrow(() => App.Debug.Error("boom", "Core"));

            Assert.AreSame(exception, App.Debug.LastTransportException);
            Assert.AreEqual(1, collectingTransport.Entries.Count);
            Assert.AreEqual("boom", collectingTransport.Entries[0].Message);
        }

        [Test]
        public void UnityLogCapture_WhenEnabled_AppendsUnityRecord()
        {
            App.Debug.Settings.UnityLogCaptureEnabled = true;

            LogAssert.Expect(UnityEngine.LogType.Log, "raw");
            UnityEngine.Debug.Log("raw");

            var entries = App.Debug.Logs.Snapshot(new DebugLogQuery(LogLevel.Info, "Unity"));

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("raw", entries[0].Message);
            Assert.AreEqual("Unity", entries[0].Tags[0]);
        }

        [Test]
        public void UnityLogCapture_WhenConsoleSinkWrites_DoesNotReenter()
        {
            App.Debug.Settings.UnityLogCaptureEnabled = true;
            App.Debug.AddSink(new UnityConsoleLogSink());

            LogAssert.Expect(UnityEngine.LogType.Log, "[Core] framework");
            App.Debug.Info("framework", "Core");

            var entries = App.Debug.Logs.Snapshot();

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("Core", entries[0].Category);
        }

        [Test]
        public void UpdateMetrics_WhenSampleIntervalReached_StoresSnapshot()
        {
            App.Debug.Settings.MetricSampleInterval = 0.1f;
            App.Debug.UpdateMetrics(0.2f);

            Assert.AreEqual(5f, App.Debug.Metrics.Fps, 0.001f);
            Assert.AreEqual(200f, App.Debug.Metrics.FrameTimeMs, 0.001f);
            Assert.Greater(App.Debug.Metrics.ManagedMemoryBytes, 0L);
            Assert.IsFalse(App.Debug.Metrics.GraphicsMemoryBytes.HasValue);
            Assert.IsFalse(App.Debug.Metrics.GpuFrameTimeMs.HasValue);
        }

        [Test]
        public void OverlayVisible_WhenModuleDisabled_ReturnsFalse()
        {
            App.Debug.OverlayVisible = true;

            App.Debug.Enabled = false;

            Assert.IsFalse(App.Debug.OverlayVisible);
            Assert.DoesNotThrow(() => App.Debug.DrawGui());
        }

        [Test]
        public void ConsoleVisible_WhenConsoleClosed_ReturnsFalse()
        {
            App.Debug.ConsoleVisible = true;

            App.Debug.Console.Close();

            Assert.IsFalse(App.Debug.ConsoleVisible);
            Assert.IsFalse(App.Debug.OverlayVisible);
        }

        [Test]
        public void Console_WhenSelectedTabSetNegative_ClampsToFirstTab()
        {
            App.Debug.Console.SelectedTab = -1;

            Assert.AreEqual(0, App.Debug.Console.SelectedTab);
        }

        [Test]
        public void TrackAsync_WhenSinkRegistered_RedactsAndDeliversAnalyticsEvent()
        {
            var sink = new CollectingAnalyticsSink();
            App.Debug.AddAnalyticsSink(sink);
            var properties = new Dictionary<string, object>
            {
                { "stage", "intro" },
                { "token", "raw-token-value" },
            };

            App.Debug.TrackAsync("stage_start", properties).GetAwaiter().GetResult();

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
            App.Debug.AddAnalyticsSink(new ThrowingAnalyticsSink(exception));
            App.Debug.AddAnalyticsSink(collectingSink);

            Assert.DoesNotThrow(() => App.Debug.TrackAsync("stage_start").GetAwaiter().GetResult());

            Assert.AreSame(exception, App.Debug.LastAnalyticsException);
            Assert.AreEqual(1, collectingSink.Events.Count);
        }

        [Test]
        public void RegisterProfile_WhenHandleRegistered_ExposesProfileTable()
        {
            var profile = new StaticProfileHandle("Timer", "Runtime", "active", 3);

            App.Debug.RegisterProfile(profile);

            var tables = App.Debug.Profiles.Snapshot();

            Assert.AreEqual(1, tables.Count);
            Assert.AreEqual("Timer", tables[0].Name);
            Assert.AreEqual("Runtime", tables[0].Category);
            Assert.AreEqual(2, tables[0].Columns.Count);
            Assert.AreEqual("active", tables[0].Rows[0].Values["state"]);
            Assert.AreEqual(3, tables[0].Rows[0].Values["count"]);
        }

        [Test]
        public void ProfileRefresh_WhenHandleThrows_IsolatesError()
        {
            var valid = new StaticProfileHandle("Valid", "Runtime", "ok", 1);
            var failed = new ThrowingProfileHandle("Broken");

            App.Debug.RegisterProfile(valid);
            App.Debug.RegisterProfile(failed);
            App.Debug.Profiles.Refresh(1f);

            var tables = App.Debug.Profiles.Snapshot();

            Assert.AreEqual(2, tables.Count);
            Assert.IsFalse(tables[0].HasError);
            Assert.IsTrue(tables[1].HasError);
            Assert.AreEqual("Broken", tables[1].Name);
            Assert.AreEqual("ok", tables[0].Rows[0].Values["state"]);
        }

        [Test]
        public void UnregisterProfile_WhenHandleRegistered_RemovesProfileTable()
        {
            var profile = new StaticProfileHandle("Timer", "Runtime", "active", 3);

            App.Debug.RegisterProfile(profile);
            var removed = App.Debug.UnregisterProfile(profile);

            Assert.IsTrue(removed);
            Assert.AreEqual(0, App.Debug.Profiles.Snapshot().Count);
            Assert.Throws<ArgumentNullException>(() => App.Debug.RegisterProfile(null));
            Assert.Throws<ArgumentNullException>(() => App.Debug.UnregisterProfile(null));
        }

        [Test]
        public void ExecuteCommandAsync_WhenCommandLineValid_CallsCommandModule()
        {
            App.Register<CommandModule>().GetAwaiter().GetResult();
            App.Command.Register("GM-ADD-ITEM", args => new DebugPanelCommand(args[0].ToString(), Convert.ToInt32(args[1])));
            App.Debug.Settings.CommandEnabled = true;
            DebugPanelCommand.Reset();

            var result = App.Debug.ExecuteCommandAsync("GM-ADD-ITEM sword 3").GetAwaiter().GetResult();

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("sword", DebugPanelCommand.LastItemId);
            Assert.AreEqual(3, DebugPanelCommand.LastCount);
        }

        [Test]
        public void DisabledModule_WhenCommandAndOverlayTriggered_ReturnsDisabled()
        {
            App.Debug.Settings.CommandEnabled = true;
            App.Debug.OverlayVisible = true;
            App.Debug.Enabled = false;

            var command = App.Debug.ExecuteCommandAsync("GM-ADD-ITEM sword 3").GetAwaiter().GetResult();

            Assert.IsTrue(command.Disabled);
            Assert.IsFalse(App.Debug.OverlayVisible);
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
            Assert.AreEqual(UnityEngine.Debug.isDebugBuild, settings.ConsoleEnabled);
            Assert.AreEqual(UnityEngine.Debug.isDebugBuild, settings.OverlayEnabled);
            Assert.AreEqual(UnityEngine.Debug.isDebugBuild, settings.UnityLogCaptureEnabled);
        }

        [Test]
        public void ProfileSnapshot_WhenRedactionEnabled_RedactsSensitiveValues()
        {
            var profile = new SensitiveProfileHandle();

            App.Debug.Settings.RedactionEnabled = true;
            App.Debug.RegisterProfile(profile);

            var tables = App.Debug.Profiles.Snapshot();

            Assert.AreEqual("[REDACTED]", tables[0].Rows[0].Values["token"]);
            Assert.AreEqual("ok", tables[0].Rows[0].Values["state"]);
        }

        [Test]
        public void Log_WhenSinkClearsSinksDuringWrite_UsesSnapshot()
        {
            var clearingSink = new ClearingSink(App.Debug);
            var collectingSink = new CollectingSink();
            App.Debug.AddSink(clearingSink);
            App.Debug.AddSink(collectingSink);

            App.Debug.Error("visible", "Core");
            App.Debug.Error("hidden", "Core");

            Assert.AreEqual(1, collectingSink.Entries.Count);
            Assert.AreEqual("visible", collectingSink.Entries[0].Message);
        }

        [Test]
        public void Shutdown_ClearsSinksAndCategoryStates()
        {
            var sink = new CollectingSink();
            App.Debug.AddSink(sink);
            App.Debug.SetCategoryEnabled("Core", false);

            App.Debug.Shutdown().GetAwaiter().GetResult();
            App.Debug.Error("hidden", "Core");

            Assert.AreEqual(0, sink.Entries.Count);
            Assert.IsTrue(App.Debug.IsCategoryEnabled("Core"));
        }

        private sealed class CollectingSink : ILogSink
        {
            public readonly List<DebugLogRecord> Entries = new List<DebugLogRecord>();

            public void Write(DebugLogRecord entry)
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

            public void Write(DebugLogRecord entry)
            {
                throw m_Exception;
            }
        }

        private sealed class ClearingSink : ILogSink
        {
            private readonly DebugModule m_Module;

            public ClearingSink(DebugModule module)
            {
                m_Module = module;
            }

            public void Write(DebugLogRecord entry)
            {
                m_Module.ClearSinks();
            }
        }

        private static DebugLogRecord CreateEntry(LogLevel level, string category, string message)
        {
            return new DebugLogRecord(DateTimeOffset.Now, 0L, 0, 0L, level, category, message, null, null, Array.Empty<string>());
        }

        private sealed class CollectingTransport : IDebugLogTransport
        {
            public readonly List<DebugLogRecord> Entries = new List<DebugLogRecord>();

            public UniTask SendAsync(DebugLogRecord record)
            {
                Entries.Add(record);
                return UniTask.CompletedTask;
            }
        }

        private sealed class ThrowingTransport : IDebugLogTransport
        {
            private readonly Exception m_Exception;

            public ThrowingTransport(Exception exception)
            {
                m_Exception = exception;
            }

            public UniTask SendAsync(DebugLogRecord record)
            {
                throw m_Exception;
            }
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

        private sealed class StaticProfileHandle : ProfileHandle
        {
            private readonly IReadOnlyList<ProfileColumn> m_Columns;
            private readonly IReadOnlyList<ProfileRow> m_Rows;

            public StaticProfileHandle(string name, string category, string state, int count)
            {
                Name = name;
                Category = category;
                m_Columns = new[]
                {
                    new ProfileColumn("state", "State"),
                    new ProfileColumn("count", "Count"),
                };
                m_Rows = new[]
                {
                    new ProfileRow(new Dictionary<string, object>
                    {
                        { "state", state },
                        { "count", count },
                    }),
                };
            }

            public override string Name { get; }

            public override string Category { get; }

            public override IReadOnlyList<ProfileColumn> Columns => m_Columns;

            public override IReadOnlyList<ProfileRow> Snapshot()
            {
                return m_Rows;
            }
        }

        private sealed class ThrowingProfileHandle : ProfileHandle
        {
            private readonly IReadOnlyList<ProfileColumn> m_Columns = new[]
            {
                new ProfileColumn("error", "Error"),
            };

            public ThrowingProfileHandle(string name)
            {
                Name = name;
            }

            public override string Name { get; }

            public override IReadOnlyList<ProfileColumn> Columns => m_Columns;

            public override IReadOnlyList<ProfileRow> Snapshot()
            {
                throw new InvalidOperationException("profile failed");
            }
        }

        private sealed class SensitiveProfileHandle : ProfileHandle
        {
            private readonly IReadOnlyList<ProfileColumn> m_Columns = new[]
            {
                new ProfileColumn("state", "State"),
                new ProfileColumn("token", "Token"),
            };

            private readonly IReadOnlyList<ProfileRow> m_Rows = new[]
            {
                new ProfileRow(new Dictionary<string, object>
                {
                    { "state", "ok" },
                    { "token", "raw-token-value" },
                }),
            };

            public override string Name => "Sensitive";

            public override IReadOnlyList<ProfileColumn> Columns => m_Columns;

            public override IReadOnlyList<ProfileRow> Snapshot()
            {
                return m_Rows;
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
