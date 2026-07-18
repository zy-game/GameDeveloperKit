using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Combat;
using GameDeveloperKit.Command;
using GameDeveloperKit.Debugger;
using GameDeveloperKit.Procedure;
using GameDeveloperKit.Timer;
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
                App.Register<DebugModule>();
            }
            catch (GameException)
            {
            }

            App.Debug.Logs.Clear();
            App.Debug.Enabled = true;
            App.Debug.MinimumLevel = LogLevel.Trace;
            App.Debug.Settings.MetricsEnabled = true;
            App.Debug.Settings.RedactionEnabled = true;
            App.Debug.Settings.UnityLogCaptureEnabled = false;
            App.Debug.Settings.UnityConsoleOutputEnabled = false;
            App.Debug.OverlayVisible = false;
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                App.Unregister<CombatModule>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }

            try
            {
                App.Unregister<ProcedureModule>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }

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

            try
            {
                App.Unregister<TimerModule>().GetAwaiter().GetResult();
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
        public void Register_WhenDebugModuleIsRegistered_StartsTimerDependency()
        {
            Assert.IsTrue(App.TryGetRegistered<TimerModule>(out var timer));
            Assert.IsNotNull(timer);
        }

        [Test]
        public void DebugModule_WhenInspected_HasRemovedSinkAnalyticsAndTransportApi()
        {
            var assembly = typeof(DebugModule).Assembly;

            Assert.IsNull(typeof(DebugModule).GetMethod("Add" + "Sink"));
            Assert.IsNull(typeof(DebugModule).GetMethod("Add" + "Analytics" + "Sink"));
            Assert.IsNull(typeof(DebugModule).GetMethod("Add" + "Log" + "Transport"));
            Assert.IsNull(typeof(DebugModule).GetMethod("Track"));
            Assert.IsNull(typeof(DebugModule).GetMethod("Track" + "Async"));
            Assert.IsNull(typeof(DebugModule).GetMethod("Upload" + "Async"));
            Assert.IsNull(typeof(DebugModule).GetMethod("Set" + "Upload" + "er"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Debugger.I" + "Log" + "Sink"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Debugger.I" + "Analytics" + "Sink"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Debugger.I" + "Debug" + "Log" + "Transport"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Debugger.Analytics" + "Event"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Debugger.Unity" + "Console" + "Log" + "Sink"));
        }

        [Test]
        public void DebugModule_WhenInspected_DoesNotHoldNetworkBridgeLifecycle()
        {
            var assembly = typeof(DebugModule).Assembly;

            Assert.IsNull(typeof(DebugModule).GetField("m_Sender", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
            Assert.IsNull(typeof(DebugModule).GetProperty("DebugLogNetworkBridge"));
            Assert.IsNotNull(assembly.GetType("GameDeveloperKit.Debugger.IDebugLogNetworkSender"));
            Assert.IsNotNull(assembly.GetType("GameDeveloperKit.Debugger.DebugLogNetworkBridge"));
            Assert.IsNotNull(assembly.GetType("GameDeveloperKit.Debugger.DebugLogPayload"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Network.IDebugLogNetworkSender"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Network.DebugLogNetworkBridge"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Network.DebugLogPayload"));
        }

        [Test]
        public void Log_WhenBelowMinimumLevel_DoesNotAppendToBuffer()
        {
            App.Debug.MinimumLevel = LogLevel.Warning;

            App.Debug.Info("hidden", "Core");
            App.Debug.Warning("visible", "Core");

            var entries = App.Debug.Logs.Snapshot();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(LogLevel.Warning, entries[0].Level);
            Assert.AreEqual("visible", entries[0].Message);
        }

        [Test]
        public void Log_WhenWritten_AppendsToDebugBuffer()
        {
            App.Debug.Error("boom", "Core");

            var entries = App.Debug.Logs.Snapshot(new DebugLogQuery(LogLevel.Error, "Core"));
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("boom", entries[0].Message);
            Assert.Greater(entries[0].Sequence, 0L);
            Assert.GreaterOrEqual(entries[0].FrameCount, 0);
            Assert.GreaterOrEqual(entries[0].TimerTick, 0L);
        }

        [Test]
        public void Log_WhenConsoleOutputEnabled_WritesUnityConsole()
        {
            App.Debug.Settings.UnityConsoleOutputEnabled = true;
            LogAssert.Expect(UnityEngine.LogType.Error, "[Error][Core] boom");

            App.Debug.Error("boom", "Core");
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
            App.Debug.SetCategoryEnabled("Resource", false);
            App.Debug.Error("hidden", "Resource");
            App.Debug.SetCategoryEnabled("Resource", true);
            App.Debug.Error("visible", "Resource");

            var entries = App.Debug.Logs.Snapshot();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("Resource", entries[0].Category);
            Assert.AreEqual("visible", entries[0].Message);
        }

        [Test]
        public void Error_WhenExceptionProvided_PreservesException()
        {
            var exception = new InvalidOperationException("load failed");

            App.Debug.Error(exception, "load failed", "Resource");

            var entries = App.Debug.Logs.Snapshot();
            Assert.AreEqual(1, entries.Count);
            Assert.AreSame(exception, entries[0].Exception);
            Assert.AreEqual("load failed", entries[0].Message);
            Assert.AreEqual("Resource", entries[0].Category);
        }

        [Test]
        public void Log_WhenRedactionEnabled_RedactsSensitiveMessage()
        {
            App.Debug.Info("token=raw-token-value", "Core");

            var entries = App.Debug.Logs.Snapshot();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("[REDACTED]", entries[0].Message);
        }

        [Test]
        public void Info_WhenMessageAndCategoryAreNull_NormalizesEntry()
        {
            App.Debug.Info(null, null);

            var entries = App.Debug.Logs.Snapshot();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(string.Empty, entries[0].Message);
            Assert.AreEqual("Default", entries[0].Category);
        }

        [Test]
        public void Log_WhenLevelIsOff_Throws()
        {
            Assert.Throws<ArgumentException>(() => App.Debug.Log(LogLevel.Off, "off"));
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
        public void AppDebugLog_WhenWritten_ForwardsToUnityConsole()
        {
            App.Debug.Settings.UnityConsoleOutputEnabled = true;
            LogAssert.Expect(UnityEngine.LogType.Log, "[Info][Resource] asset bundle loaded");

            App.Debug.Info("asset bundle loaded", "Resource");

            var entries = App.Debug.Logs.Snapshot(new DebugLogQuery(LogLevel.Info, "Resource"));
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("asset bundle loaded", entries[0].Message);
        }

        [Test]
        public void AppDebugLog_WhenUnityLogCaptureEnabled_DoesNotCaptureForwardedConsoleLogAgain()
        {
            App.Debug.Settings.UnityLogCaptureEnabled = true;
            App.Debug.Settings.UnityConsoleOutputEnabled = true;
            LogAssert.Expect(UnityEngine.LogType.Log, "[Info][Resource] asset bundle loaded");

            App.Debug.Info("asset bundle loaded", "Resource");

            Assert.AreEqual(1, App.Debug.Logs.Snapshot().Count);
            Assert.AreEqual(0, App.Debug.Logs.Snapshot(new DebugLogQuery(LogLevel.Info, "Unity")).Count);
        }

        [Test]
        public void UpdateMetrics_WhenSampleIntervalReached_StoresMemoryProfileSample()
        {
            App.Debug.Settings.MetricSampleInterval = 0.1f;
            App.Debug.UpdateMetrics(0.2f);

            Assert.AreEqual(5f, App.Debug.Metrics.Fps, 0.001f);
            Assert.AreEqual(200f, App.Debug.Metrics.FrameTimeMs, 0.001f);
            Assert.Greater(App.Debug.Metrics.ManagedMemoryBytes, 0L);
            Assert.IsFalse(App.Debug.Metrics.GraphicsMemoryBytes.HasValue);
            Assert.IsFalse(App.Debug.Metrics.GpuFrameTimeMs.HasValue);
            Assert.AreSame(App.Debug.MemoryProfile, FindProfileHandle(App.Debug.Profiles.Snapshot(), "Memory"));
            Assert.AreEqual(1, App.Debug.MemoryProfile.SamplesCount);
        }

        [Test]
        public void UpdateMetrics_WhenSampleCapacityExceeded_KeepsFixedRing()
        {
            App.Debug.Settings.MetricSampleInterval = 0.1f;

            for (var i = 0; i < App.Debug.MemoryProfile.SamplesCapacity + 10; i++)
            {
                App.Debug.UpdateMetrics(0.2f);
            }

            Assert.AreEqual(App.Debug.MemoryProfile.SamplesCapacity, App.Debug.MemoryProfile.SamplesCount);
        }

        [Test]
        public void Startup_RegistersTimerRefreshHandle()
        {
            var handle = FindTimerUpdateHandle(App.Timer, "DebugModule.Refresh");

            Assert.AreSame(App.Debug, handle.Owner);
        }

        [Test]
        public void TimerUpdate_WhenDebugRefreshHandleRegistered_StoresMemoryProfileSample()
        {
            App.Debug.Settings.MetricSampleInterval = 0.1f;

            App.Timer.Update(TimerTickKind.Update, 0.01f, 0.2f);

            Assert.AreEqual(5f, App.Debug.Metrics.Fps, 0.001f);
            Assert.AreEqual(200f, App.Debug.Metrics.FrameTimeMs, 0.001f);
            Assert.AreEqual(1, App.Debug.MemoryProfile.SamplesCount);
        }

        [Test]
        public void TimerUpdate_WhenDebugDisabled_DoesNotSampleMemoryProfile()
        {
            App.Debug.Settings.MetricSampleInterval = 0.1f;
            App.Debug.Enabled = false;

            App.Timer.Update(TimerTickKind.Update, 0.01f, 0.2f);

            Assert.AreEqual(0, App.Debug.MemoryProfile.SamplesCount);
        }

        [Test]
        public void Shutdown_UnregistersTimerRefreshHandle()
        {
            var debug = App.Debug;

            App.Unregister<DebugModule>().GetAwaiter().GetResult();

            foreach (var handle in App.Timer.Snapshot().Updates)
            {
                Assert.AreNotSame(debug, handle.Owner);
                Assert.AreNotEqual("DebugModule.Refresh", handle.Tag);
            }
        }

        [Test]
        public void DebugGuiDriver_WhenInspected_DoesNotDeclareUpdateMethod()
        {
            var type = typeof(DebugModule).Assembly.GetType("GameDeveloperKit.Debugger.DebugGuiDriver");

            Assert.IsNotNull(type);

            var method = type.GetMethod(
                "Update",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.DeclaredOnly);

            Assert.IsNull(method);
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
        public void RegisterProfile_WhenHandleRegistered_ExposesProfileHandle()
        {
            var profile = new StaticProfileHandle("Custom Debug");

            App.Debug.RegisterProfile(profile);

            var handle = FindProfileHandle(App.Debug.Profiles.Snapshot(), "Custom Debug");

            Assert.AreSame(profile, handle);
            Assert.AreEqual("Custom Debug", DebugProfileRegistry.GetDisplayName(handle));
        }

        [Test]
        public void RegisterProfile_WhenDrawThrows_DoesNotDrawDuringRegistration()
        {
            var valid = new StaticProfileHandle("Valid");
            var failed = new ThrowingProfileHandle("Broken");

            App.Debug.RegisterProfile(valid);
            App.Debug.RegisterProfile(failed);

            var handles = App.Debug.Profiles.Snapshot();

            Assert.AreSame(valid, FindProfileHandle(handles, "Valid"));
            Assert.AreSame(failed, FindProfileHandle(handles, "Broken"));
            Assert.AreEqual(0, valid.DrawCount);
        }

        [Test]
        public void ProfileName_WhenInvalid_FallsBackToTypeName()
        {
            var brokenName = new ThrowingMetadataProfileHandle(throwName: true);
            var blankName = new ThrowingMetadataProfileHandle(blankName: true);

            Assert.DoesNotThrow(() => App.Debug.RegisterProfile(brokenName));
            Assert.DoesNotThrow(() => App.Debug.RegisterProfile(blankName));

            Assert.AreEqual(nameof(ThrowingMetadataProfileHandle), DebugProfileRegistry.GetDisplayName(brokenName));
            Assert.AreEqual(nameof(ThrowingMetadataProfileHandle), DebugProfileRegistry.GetDisplayName(blankName));
        }

        [Test]
        public void UnregisterProfile_WhenHandleRegistered_RemovesProfileHandle()
        {
            var profile = new StaticProfileHandle("Custom Debug");

            App.Debug.RegisterProfile(profile);
            var removed = App.Debug.UnregisterProfile(profile);

            Assert.IsTrue(removed);
            Assert.IsFalse(ContainsProfileHandle(App.Debug.Profiles.Snapshot(), "Custom Debug"));
            Assert.Throws<ArgumentNullException>(() => App.Debug.RegisterProfile(null));
            Assert.Throws<ArgumentNullException>(() => App.Debug.UnregisterProfile(null));
        }

        [Test]
        public void ExecuteCommandAsync_WhenCommandLineValid_CallsCommandModule()
        {
            App.Register<CommandModule>();
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
            Assert.IsTrue(settings.UnityConsoleOutputEnabled);
        }

        [Test]
        public void ProfileHandle_WhenInspected_HasNameOnlyPublicContract()
        {
            var assembly = typeof(ProfileHandle).Assembly;

            Assert.IsNotNull(typeof(ProfileHandle).GetProperty("Name"));
            Assert.IsNull(typeof(ProfileHandle).GetProperty("Category"));
            Assert.IsNull(typeof(ProfileHandle).GetProperty("RefreshInterval"));
            Assert.IsNull(typeof(ProfileHandle).GetProperty("Enabled"));
            Assert.IsNull(typeof(ProfileHandle).GetProperty("Columns"));
            Assert.IsNull(typeof(ProfileHandle).GetMethod("Snapshot"));
            Assert.IsNull(typeof(ProfileHandle).GetMethod("SetColumn"));
            Assert.IsNull(typeof(ProfileHandle).GetMethod("AddRow"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Debugger.ProfileColumn"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Debugger.ProfileRow"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Debugger.ProfileTable"));
        }

        [Test]
        public void Log_WhenRedactionToStringThrows_UsesFallback()
        {
            var exception = new ThrowingToStringException();
            var context = new ThrowingToStringContext();

            App.Debug.Settings.RedactionEnabled = true;

            Assert.DoesNotThrow(() => App.Debug.Error(exception, "failed", "Core"));
            Assert.DoesNotThrow(() => App.Debug.Info("context", "Core", context));

            var entries = App.Debug.Logs.Snapshot();

            Assert.AreEqual(2, entries.Count);
            Assert.IsInstanceOf<Exception>(entries[0].Exception);
            StringAssert.Contains("ToString failed", entries[0].Exception.ToString());
            StringAssert.Contains("ToString failed", entries[1].Context.ToString());
        }

        [Test]
        public void Log_WhenRedactionDisabled_DoesNotStringifyExceptionOrContext()
        {
            var exception = new ThrowingToStringException();
            var context = new ThrowingToStringContext();

            App.Debug.Settings.RedactionEnabled = false;

            Assert.DoesNotThrow(() => App.Debug.Error(exception, "failed", "Core", context));

            var entries = App.Debug.Logs.Snapshot();

            Assert.AreSame(exception, entries[0].Exception);
            Assert.AreSame(context, entries[0].Context);
        }

        [Test]
        public void Startup_RegistersBuiltInProfileHandles()
        {
            var handles = App.Debug.Profiles.Snapshot();

            Assert.AreSame(App.Debug.MemoryProfile, FindProfileHandle(handles, "Memory"));
            Assert.AreSame(App.Debug.DeviceInfoProfile, FindProfileHandle(handles, "Device Info"));
            Assert.IsTrue(ContainsProfileHandle(handles, "Timer"));
            Assert.IsFalse(ContainsProfileHandle(handles, "Debug"));
        }

        [Test]
        public void Startup_WhenDebugStartsAfterTimer_RegistersTimerProfileHandle()
        {
            App.Unregister<DebugModule>().GetAwaiter().GetResult();

            Assert.IsTrue(App.TryGetRegistered<TimerModule>(out _));
            Assert.IsFalse(App.TryGetRegistered<DebugModule>(out _));

            App.Register<DebugModule>();

            Assert.IsTrue(ContainsProfileHandle(App.Debug.Profiles.Snapshot(), "Timer"));
            Assert.AreEqual(1, CountProfileHandles(App.Debug.Profiles.Snapshot(), "Timer"));
        }

        [Test]
        public void Startup_WhenDebugExistsAndProcedureStarts_RegistersProcedureProfileHandle()
        {
            App.Register<ProcedureModule>();

            Assert.IsTrue(ContainsProfileHandle(App.Debug.Profiles.Snapshot(), "Procedure"));

            App.Unregister<ProcedureModule>().GetAwaiter().GetResult();

            Assert.IsFalse(ContainsProfileHandle(App.Debug.Profiles.Snapshot(), "Procedure"));
        }

        [Test]
        public void Startup_WhenDebugStartsAfterCombat_RegistersCombatProfileHandle()
        {
            App.Unregister<DebugModule>().GetAwaiter().GetResult();
            App.Register<CombatModule>();

            Assert.IsFalse(App.TryGetRegistered<DebugModule>(out _));

            App.Register<DebugModule>();

            Assert.IsTrue(ContainsProfileHandle(App.Debug.Profiles.Snapshot(), "Combat"));
        }

        [Test]
        public void Shutdown_WhenTimerModuleUnregistered_RemovesTimerProfileHandle()
        {
            Assert.IsTrue(ContainsProfileHandle(App.Debug.Profiles.Snapshot(), "Timer"));

            App.Unregister<TimerModule>().GetAwaiter().GetResult();

            Assert.IsFalse(ContainsProfileHandle(App.Debug.Profiles.Snapshot(), "Timer"));
        }

        [Test]
        public void Startup_WhenCalled_StartsConsoleClosed()
        {
            App.Debug.ConsoleVisible = true;

            App.Debug.Startup();

            Assert.IsFalse(App.Debug.ConsoleVisible);
        }

        [Test]
        public void Shutdown_ClearsLogsAndCategoryStates()
        {
            App.Debug.SetCategoryEnabled("Core", false);

            App.Debug.Shutdown();
            App.Debug.Error("hidden", "Core");

            Assert.AreEqual(0, App.Debug.Logs.Snapshot().Count);
            Assert.IsTrue(App.Debug.IsCategoryEnabled("Core"));
        }

        [Test]
        public void DebugLogPayload_WhenTagsAreNull_UsesEmptyTags()
        {
            var timestamp = DateTimeOffset.Now;

            var payload = new DebugLogPayload(
                10L,
                timestamp,
                20,
                30L,
                "Info",
                "Core",
                "message",
                "exception",
                "context",
                null);

            Assert.AreEqual(10L, payload.Sequence);
            Assert.AreEqual(timestamp, payload.Timestamp);
            Assert.AreEqual(20, payload.FrameCount);
            Assert.AreEqual(30L, payload.TimerTick);
            Assert.AreEqual("Info", payload.Level);
            Assert.AreEqual("Core", payload.Category);
            Assert.AreEqual("message", payload.Message);
            Assert.AreEqual("exception", payload.Exception);
            Assert.AreEqual("context", payload.Context);
            Assert.AreEqual(0, payload.Tags.Count);
        }

        [Test]
        public void DebugLogNetworkBridge_ToPayload_MapsDebugLogRecordFields()
        {
            var timestamp = DateTimeOffset.Now;
            var exception = new InvalidOperationException("failed");
            var tags = new[] { "runtime", "qa" };
            var record = new DebugLogRecord(
                timestamp,
                12L,
                34,
                56L,
                LogLevel.Warning,
                "Core",
                "redacted message",
                exception,
                "redacted context",
                tags);

            var payload = DebugLogNetworkBridge.ToPayload(record);

            Assert.AreEqual(record.Sequence, payload.Sequence);
            Assert.AreEqual(record.Timestamp, payload.Timestamp);
            Assert.AreEqual(record.FrameCount, payload.FrameCount);
            Assert.AreEqual(record.TimerTick, payload.TimerTick);
            Assert.AreEqual("Warning", payload.Level);
            Assert.AreEqual(record.Category, payload.Category);
            Assert.AreEqual(record.Message, payload.Message);
            StringAssert.Contains("failed", payload.Exception);
            Assert.AreEqual("redacted context", payload.Context);
            CollectionAssert.AreEqual(tags, payload.Tags);
        }

        [Test]
        public void DebugLogNetworkBridge_ToPayload_WhenToStringThrows_UsesFallbackText()
        {
            var record = new DebugLogRecord(
                DateTimeOffset.Now,
                1L,
                2,
                3L,
                LogLevel.Error,
                "Core",
                "message",
                new ThrowingToStringException(),
                new ThrowingToStringContext(),
                null);

            var payload = DebugLogNetworkBridge.ToPayload(record);

            StringAssert.Contains("ToString failed", payload.Exception);
            StringAssert.Contains("ToString failed", payload.Context);
        }

        [Test]
        public void DebugLogNetworkBridge_WhenFlushed_SendsOnlyNewRecords()
        {
            var logs = new DebugLogBuffer(8);
            var sender = new TestDebugLogSender();
            var bridge = new DebugLogNetworkBridge(logs, sender);

            logs.Append(CreateLogRecord(1L, "first"));
            logs.Append(CreateLogRecord(2L, "second"));

            var firstCount = bridge.FlushAsync().GetAwaiter().GetResult();
            var secondCount = bridge.FlushAsync().GetAwaiter().GetResult();
            logs.Append(CreateLogRecord(3L, "third"));
            var thirdCount = bridge.FlushAsync().GetAwaiter().GetResult();

            Assert.AreEqual(2, firstCount);
            Assert.AreEqual(0, secondCount);
            Assert.AreEqual(1, thirdCount);
            Assert.AreEqual(3L, bridge.LastSentSequence);
            Assert.AreEqual(3, sender.Payloads.Count);
            Assert.AreEqual("first", sender.Payloads[0].Message);
            Assert.AreEqual("second", sender.Payloads[1].Message);
            Assert.AreEqual("third", sender.Payloads[2].Message);
        }

        [Test]
        public void DebugLogNetworkBridge_WhenSenderFails_StopsAndKeepsCursorAtLastSuccess()
        {
            var logs = new DebugLogBuffer(8);
            var sender = new TestDebugLogSender { FailOnSequence = 2L };
            var bridge = new DebugLogNetworkBridge(logs, sender);

            logs.Append(CreateLogRecord(1L, "first"));
            logs.Append(CreateLogRecord(2L, "second"));
            logs.Append(CreateLogRecord(3L, "third"));

            var exception = Assert.Throws<InvalidOperationException>(() => bridge.FlushAsync().GetAwaiter().GetResult());
            var lastSentAfterFailure = bridge.LastSentSequence;
            sender.FailOnSequence = null;
            var retryCount = bridge.FlushAsync().GetAwaiter().GetResult();

            Assert.AreEqual("send failed", exception.Message);
            Assert.AreEqual(1L, lastSentAfterFailure);
            Assert.AreEqual(2, retryCount);
            Assert.AreEqual(3L, bridge.LastSentSequence);
            Assert.AreEqual(4, sender.Payloads.Count);
            Assert.AreEqual(1L, sender.Payloads[0].Sequence);
            Assert.AreEqual(2L, sender.Payloads[1].Sequence);
            Assert.AreEqual(2L, sender.Payloads[2].Sequence);
            Assert.AreEqual(3L, sender.Payloads[3].Sequence);
        }

        [Test]
        public void DebugLogNetworkBridge_WhenArgumentsAreInvalid_Throws()
        {
            var logs = new DebugLogBuffer();
            var sender = new TestDebugLogSender();

            Assert.Throws<ArgumentNullException>(() => new DebugLogNetworkBridge(null, sender));
            Assert.Throws<ArgumentNullException>(() => new DebugLogNetworkBridge(logs, null));
        }

        [Test]
        public void DebugLogNetworkBridge_WhenInspected_DoesNotBindPayloadToNetworkMessage()
        {
            Assert.AreEqual("GameDeveloperKit.Debugger", typeof(DebugLogPayload).Namespace);
            Assert.IsFalse(typeof(GameDeveloperKit.Network.Message).IsAssignableFrom(typeof(DebugLogPayload)));
            Assert.IsNull(typeof(DebugLogPayload).Assembly.GetType("GameDeveloperKit.Network.DebugLogPayload"));
        }

        private static DebugLogRecord CreateEntry(LogLevel level, string category, string message)
        {
            return new DebugLogRecord(DateTimeOffset.Now, 0L, 0, 0L, level, category, message, null, null, Array.Empty<string>());
        }

        private static DebugLogRecord CreateLogRecord(long sequence, string message)
        {
            return new DebugLogRecord(
                DateTimeOffset.Now,
                sequence,
                0,
                0L,
                LogLevel.Info,
                "Core",
                message,
                null,
                null,
                Array.Empty<string>());
        }

        private static ProfileHandle FindProfileHandle(IReadOnlyList<ProfileHandle> handles, string name)
        {
            foreach (var handle in handles)
            {
                if (DebugProfileRegistry.GetDisplayName(handle) == name)
                {
                    return handle;
                }
            }

            throw new AssertionException($"Profile '{name}' was not found.");
        }

        private static bool ContainsProfileHandle(IReadOnlyList<ProfileHandle> handles, string name)
        {
            foreach (var handle in handles)
            {
                if (DebugProfileRegistry.GetDisplayName(handle) == name)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountProfileHandles(IReadOnlyList<ProfileHandle> handles, string name)
        {
            var count = 0;
            foreach (var handle in handles)
            {
                if (DebugProfileRegistry.GetDisplayName(handle) == name)
                {
                    count++;
                }
            }

            return count;
        }

        private static TimerUpdateHandle FindTimerUpdateHandle(TimerModule timer, string tag)
        {
            foreach (var handle in timer.Snapshot().Updates)
            {
                if (handle.Tag == tag)
                {
                    return handle;
                }
            }

            throw new AssertionException($"Timer update handle '{tag}' was not found.");
        }

        private sealed class StaticProfileHandle : ProfileHandle
        {
            public StaticProfileHandle(string name)
            {
                Name = name;
            }

            public override string Name { get; }

            public int DrawCount { get; private set; }

            protected internal override void Draw()
            {
                DrawCount++;
            }
        }

        private sealed class ThrowingProfileHandle : ProfileHandle
        {
            public ThrowingProfileHandle(string name)
            {
                Name = name;
            }

            public override string Name { get; }

            protected internal override void Draw()
            {
                throw new InvalidOperationException("profile failed");
            }
        }

        private sealed class ThrowingMetadataProfileHandle : ProfileHandle
        {
            private readonly bool m_ThrowName;
            private readonly bool m_BlankName;

            public ThrowingMetadataProfileHandle(bool throwName = false, bool blankName = false)
            {
                m_ThrowName = throwName;
                m_BlankName = blankName;
            }

            public override string Name
            {
                get
                {
                    if (m_ThrowName)
                    {
                        throw new InvalidOperationException("name failed");
                    }

                    if (m_BlankName)
                    {
                        return string.Empty;
                    }

                    return "Metadata";
                }
            }

            protected internal override void Draw()
            {
            }
        }

        private sealed class ThrowingToStringException : Exception
        {
            public override string ToString()
            {
                throw new InvalidOperationException("exception tostring failed");
            }
        }

        private sealed class ThrowingToStringContext
        {
            public override string ToString()
            {
                throw new InvalidOperationException("context tostring failed");
            }
        }

        private sealed class TestDebugLogSender : IDebugLogNetworkSender
        {
            public List<DebugLogPayload> Payloads { get; } = new List<DebugLogPayload>();

            public long? FailOnSequence { get; set; }

            public UniTask SendDebugLogAsync(DebugLogPayload payload)
            {
                Payloads.Add(payload);
                if (FailOnSequence.HasValue && FailOnSequence.Value == payload.Sequence)
                {
                    throw new InvalidOperationException("send failed");
                }

                return UniTask.CompletedTask;
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
