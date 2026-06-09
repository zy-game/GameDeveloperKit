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

            App.Debug.Logs.Clear();
            App.Debug.Enabled = true;
            App.Debug.MinimumLevel = LogLevel.Trace;
            App.Debug.Settings.MetricsEnabled = true;
            App.Debug.Settings.RedactionEnabled = true;
            App.Debug.Settings.UnityLogCaptureEnabled = false;
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
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Logger.I" + "Log" + "Sink"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Logger.I" + "Analytics" + "Sink"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Logger.I" + "Debug" + "Log" + "Transport"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Logger.Analytics" + "Event"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Logger.Unity" + "Console" + "Log" + "Sink"));
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
            var profile = new StaticProfileHandle("Timer");

            App.Debug.RegisterProfile(profile);

            var handle = FindProfileHandle(App.Debug.Profiles.Snapshot(), "Timer");

            Assert.AreSame(profile, handle);
            Assert.AreEqual("Timer", DebugProfileRegistry.GetDisplayName(handle));
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
            var profile = new StaticProfileHandle("Timer");

            App.Debug.RegisterProfile(profile);
            var removed = App.Debug.UnregisterProfile(profile);

            Assert.IsTrue(removed);
            Assert.IsFalse(ContainsProfileHandle(App.Debug.Profiles.Snapshot(), "Timer"));
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
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Logger.ProfileColumn"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Logger.ProfileRow"));
            Assert.IsNull(assembly.GetType("GameDeveloperKit.Logger.ProfileTable"));
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
            Assert.IsFalse(ContainsProfileHandle(handles, "Debug"));
        }

        [Test]
        public void Startup_WhenCalled_StartsConsoleClosed()
        {
            App.Debug.ConsoleVisible = true;

            App.Debug.Startup().GetAwaiter().GetResult();

            Assert.IsFalse(App.Debug.ConsoleVisible);
        }

        [Test]
        public void Shutdown_ClearsLogsAndCategoryStates()
        {
            App.Debug.SetCategoryEnabled("Core", false);

            App.Debug.Shutdown().GetAwaiter().GetResult();
            App.Debug.Error("hidden", "Core");

            Assert.AreEqual(0, App.Debug.Logs.Snapshot().Count);
            Assert.IsTrue(App.Debug.IsCategoryEnabled("Core"));
        }

        private static DebugLogRecord CreateEntry(LogLevel level, string category, string message)
        {
            return new DebugLogRecord(DateTimeOffset.Now, 0L, 0, 0L, level, category, message, null, null, Array.Empty<string>());
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
