using System;
using System.Collections.Generic;
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
            Super.Logger.Enabled = true;
            Super.Logger.MinimumLevel = LogLevel.Trace;
        }

        [TearDown]
        public void TearDown()
        {
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
    }
}
