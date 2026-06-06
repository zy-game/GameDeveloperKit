using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Command;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    public sealed class CommandModuleTests : RuntimeTestBase
    {
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
        }

        [Test]
        public void Register_WhenCommandModuleIsRegistered_ReturnsCommand()
        {
            App.Register<CommandModule>().GetAwaiter().GetResult();

            Assert.IsNotNull(App.Command);
        }

        [Test]
        public void Startup_WhenCompleted_HistoryIsEmpty()
        {
            var module = new CommandModule();
            module.Startup().GetAwaiter().GetResult();

            Assert.IsFalse(module.CanUndo);
            Assert.IsFalse(module.CanRedo);
            Assert.AreEqual(0, module.UndoCount);
            Assert.AreEqual(0, module.RedoCount);
        }

        [Test]
        public void ExecuteUndoRedo_WhenCommandsAreUndoable_MovesBetweenStacksInOrder()
        {
            var module = new CommandModule();
            var events = new List<string>();
            var commandA = new RecordingCommand("A", events);
            var commandB = new RecordingCommand("B", events);
            var commandC = new RecordingCommand("C", events);

            module.ExecuteAsync(commandA).GetAwaiter().GetResult();
            module.ExecuteAsync(commandB).GetAwaiter().GetResult();
            module.ExecuteAsync(commandC).GetAwaiter().GetResult();

            Assert.IsTrue(module.CanUndo);
            Assert.IsFalse(module.CanRedo);
            Assert.AreEqual(3, module.UndoCount);
            Assert.AreEqual(0, module.RedoCount);

            module.UndoAsync().GetAwaiter().GetResult();
            module.UndoAsync().GetAwaiter().GetResult();
            module.UndoAsync().GetAwaiter().GetResult();

            module.RedoAsync().GetAwaiter().GetResult();
            module.RedoAsync().GetAwaiter().GetResult();
            module.RedoAsync().GetAwaiter().GetResult();

            CollectionAssert.AreEqual(
                new[]
                {
                    "execute:A",
                    "execute:B",
                    "execute:C",
                    "undo:C",
                    "undo:B",
                    "undo:A",
                    "redo:A",
                    "redo:B",
                    "redo:C",
                },
                events);
        }

        [Test]
        public void Execute_WhenRedoStackExists_ReleasesAndClearsRedoStack()
        {
            var module = new CommandModule();
            var first = new RecordingCommand("first");
            var second = new RecordingCommand("second");

            module.ExecuteAsync(first).GetAwaiter().GetResult();
            module.UndoAsync().GetAwaiter().GetResult();
            module.ExecuteAsync(second).GetAwaiter().GetResult();

            Assert.AreEqual(1, first.ReleaseCount);
            Assert.AreEqual(1, module.UndoCount);
            Assert.AreEqual(0, module.RedoCount);
            Assert.AreEqual("second", module.GetSnapshot().UndoName);
        }

        [Test]
        public void Execute_WhenCommandIsTransient_DoesNotChangeHistory()
        {
            var module = new CommandModule();
            var command = new RecordingCommand("preview", null, CommandHistoryMode.Transient);

            module.ExecuteAsync(command).GetAwaiter().GetResult();

            Assert.AreEqual(1, command.ExecuteCount);
            Assert.AreEqual(0, module.UndoCount);
            Assert.AreEqual(0, module.RedoCount);
        }

        [Test]
        public void RegisterFactory_WhenDuplicateCommandName_ReturnsFalseWithoutReplacing()
        {
            var module = new CommandModule();

            Assert.IsTrue(module.Register("GM-ADD-ITEM", _ => new RecordingCommand("first")));
            Assert.IsFalse(module.Register("GM-ADD-ITEM", _ => new RecordingCommand("second")));

            var result = module.ExecuteAsync("GM-ADD-ITEM").GetAwaiter().GetResult();

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("first", module.GetSnapshot().UndoName);
        }

        [Test]
        public void ExecuteByName_WhenFactoryRegistered_ExecutesCommandAndUsesHistoryMode()
        {
            var module = new CommandModule();
            var events = new List<string>();
            module.Register("GM-ADD-ITEM", args => new RecordingCommand($"{args[0]}:{args[1]}", events));

            var result = module.ExecuteAsync("GM-ADD-ITEM", "sword", 3).GetAwaiter().GetResult();

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("GM-ADD-ITEM", result.CommandName);
            Assert.AreEqual(1, module.UndoCount);
            Assert.AreEqual("sword:3", module.GetSnapshot().UndoName);
            CollectionAssert.AreEqual(new[] { "execute:sword:3" }, events);
        }

        [Test]
        public void RegisterAttribute_WhenConstructorArgsMatch_BindsArgsAndExecutes()
        {
            var module = new CommandModule();
            GmAddItemCommand.Reset();

            Assert.IsTrue(module.Register<GmAddItemCommand>());
            var result = module.ExecuteAsync("GM-ADD-ITEM", "sword", "3").GetAwaiter().GetResult();

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("sword", GmAddItemCommand.LastItemId);
            Assert.AreEqual(3, GmAddItemCommand.LastCount);
            Assert.AreEqual(1, module.UndoCount);
        }

        [Test]
        public void ExecuteByName_WhenNameInvalidOrMissing_ReturnsFailureResult()
        {
            var module = new CommandModule();

            var empty = module.ExecuteAsync(string.Empty).GetAwaiter().GetResult();
            var missing = module.ExecuteAsync("UNKNOWN").GetAwaiter().GetResult();

            Assert.IsFalse(empty.Succeeded);
            Assert.IsFalse(missing.Succeeded);
            Assert.IsNull(empty.Exception);
            Assert.IsNull(missing.Exception);
        }

        [Test]
        public void ExecuteByName_WhenArgumentsDoNotMatch_ReturnsFailureResult()
        {
            var module = new CommandModule();
            module.Register<GmAddItemCommand>();

            var result = module.ExecuteAsync("GM-ADD-ITEM", "sword").GetAwaiter().GetResult();

            Assert.IsFalse(result.Succeeded);
            Assert.IsInstanceOf<GameException>(result.Exception);
            Assert.AreEqual(0, module.UndoCount);
        }

        [Test]
        public void Execute_WhenCommandIsBarrier_ClearsHistoryAndReleasesCommands()
        {
            var module = new CommandModule();
            var undoable = new RecordingCommand("undoable");
            var barrier = new RecordingCommand("barrier", null, CommandHistoryMode.Barrier);

            module.ExecuteAsync(undoable).GetAwaiter().GetResult();
            module.ExecuteAsync(barrier).GetAwaiter().GetResult();

            Assert.AreEqual(1, undoable.ReleaseCount);
            Assert.AreEqual(1, barrier.ReleaseCount);
            Assert.AreEqual(0, module.UndoCount);
            Assert.AreEqual(0, module.RedoCount);
        }

        [Test]
        public void Execute_WhenHistoryCapacityIsExceeded_ReleasesOldestCommand()
        {
            var module = new CommandModule { HistoryCapacity = 2 };
            var commandA = new RecordingCommand("A");
            var commandB = new RecordingCommand("B");
            var commandC = new RecordingCommand("C");

            module.ExecuteAsync(commandA).GetAwaiter().GetResult();
            module.ExecuteAsync(commandB).GetAwaiter().GetResult();
            module.ExecuteAsync(commandC).GetAwaiter().GetResult();

            Assert.AreEqual(1, commandA.ReleaseCount);
            Assert.AreEqual(2, module.UndoCount);
            Assert.AreEqual("C", module.GetSnapshot().UndoName);
        }

        [Test]
        public void Execute_WhenHistoryCapacityIsUnlimited_DoesNotReleaseOldCommands()
        {
            var module = new CommandModule { HistoryCapacity = 0 };
            var commandA = new RecordingCommand("A");
            var commandB = new RecordingCommand("B");
            var commandC = new RecordingCommand("C");

            module.ExecuteAsync(commandA).GetAwaiter().GetResult();
            module.ExecuteAsync(commandB).GetAwaiter().GetResult();
            module.ExecuteAsync(commandC).GetAwaiter().GetResult();

            Assert.AreEqual(0, commandA.ReleaseCount);
            Assert.AreEqual(3, module.UndoCount);
        }

        [Test]
        public void HistoryChanged_WhenHistoryChanges_ReportsSnapshots()
        {
            var module = new CommandModule();
            var snapshots = new List<CommandHistorySnapshot>();
            module.HistoryChanged += snapshot => snapshots.Add(snapshot);

            module.ExecuteAsync(new RecordingCommand("A")).GetAwaiter().GetResult();
            module.UndoAsync().GetAwaiter().GetResult();
            module.RedoAsync().GetAwaiter().GetResult();
            module.Clear();

            Assert.AreEqual(4, snapshots.Count);
            Assert.IsTrue(snapshots[0].CanUndo);
            Assert.IsTrue(snapshots[1].CanRedo);
            Assert.IsFalse(snapshots[3].CanUndo);
            Assert.IsFalse(snapshots[3].CanRedo);
        }

        [Test]
        public void CommandGroup_WhenExecutedAndUndone_RecordsAsSingleHistoryEntry()
        {
            var module = new CommandModule();
            var events = new List<string>();
            var group = new CommandGroup(
                "group",
                new RecordingCommand("A", events),
                new RecordingCommand("B", events),
                new RecordingCommand("C", events));

            module.ExecuteAsync(group).GetAwaiter().GetResult();
            module.UndoAsync().GetAwaiter().GetResult();

            Assert.AreEqual(0, module.UndoCount);
            Assert.AreEqual(1, module.RedoCount);
            CollectionAssert.AreEqual(
                new[]
                {
                    "execute:A",
                    "execute:B",
                    "execute:C",
                    "undo:C",
                    "undo:B",
                    "undo:A",
                },
                events);
        }

        [Test]
        public void Execute_WhenCommandIsNull_Throws()
        {
            var module = new CommandModule();

            Assert.Throws<ArgumentNullException>(() => module.ExecuteAsync((ICommand)null).GetAwaiter().GetResult());
        }

        [Test]
        public void Execute_WhenHistoryModeIsInvalid_ThrowsBeforeExecuting()
        {
            var module = new CommandModule();
            var command = new RecordingCommand("invalid", null, (CommandHistoryMode)99);

            Assert.Throws<GameException>(() => module.ExecuteAsync(command).GetAwaiter().GetResult());
            Assert.AreEqual(0, command.ExecuteCount);
            Assert.AreEqual(0, module.UndoCount);
            Assert.AreEqual(0, module.RedoCount);
        }

        [Test]
        public void UndoRedo_WhenStacksAreEmpty_AreNoOps()
        {
            var module = new CommandModule();

            Assert.DoesNotThrow(() => module.UndoAsync().GetAwaiter().GetResult());
            Assert.DoesNotThrow(() => module.RedoAsync().GetAwaiter().GetResult());
            Assert.AreEqual(0, module.UndoCount);
            Assert.AreEqual(0, module.RedoCount);
        }

        [Test]
        public void Clear_WhenCalledRepeatedly_IsNoOpAfterFirstClear()
        {
            var module = new CommandModule();

            module.ExecuteAsync(new RecordingCommand("A")).GetAwaiter().GetResult();
            module.Clear();
            module.Clear();

            Assert.AreEqual(0, module.UndoCount);
            Assert.AreEqual(0, module.RedoCount);
        }

        [Test]
        public void Execute_WhenCommandThrows_DoesNotChangeHistory()
        {
            var module = new CommandModule();
            var oldRedo = new RecordingCommand("redo");
            var throwing = new ThrowingCommand("throw", CommandPhase.Execute);

            module.ExecuteAsync(oldRedo).GetAwaiter().GetResult();
            module.UndoAsync().GetAwaiter().GetResult();

            Assert.Throws<InvalidOperationException>(() => module.ExecuteAsync(throwing).GetAwaiter().GetResult());
            Assert.AreEqual(0, module.UndoCount);
            Assert.AreEqual(1, module.RedoCount);
            Assert.AreEqual(0, oldRedo.ReleaseCount);
        }

        [Test]
        public void Undo_WhenCommandThrows_DoesNotMoveCommand()
        {
            var module = new CommandModule();
            var throwing = new ThrowingCommand("throw", CommandPhase.Undo);

            module.ExecuteAsync(throwing).GetAwaiter().GetResult();

            Assert.Throws<InvalidOperationException>(() => module.UndoAsync().GetAwaiter().GetResult());
            Assert.AreEqual(1, module.UndoCount);
            Assert.AreEqual(0, module.RedoCount);
        }

        [Test]
        public void Redo_WhenCommandThrows_DoesNotMoveCommand()
        {
            var module = new CommandModule();
            var throwing = new ThrowingCommand("throw", CommandPhase.Redo);

            module.ExecuteAsync(throwing).GetAwaiter().GetResult();
            module.UndoAsync().GetAwaiter().GetResult();

            Assert.Throws<InvalidOperationException>(() => module.RedoAsync().GetAwaiter().GetResult());
            Assert.AreEqual(0, module.UndoCount);
            Assert.AreEqual(1, module.RedoCount);
        }

        [Test]
        public void Execute_WhenCommandReentersModule_ThrowsAndDoesNotChangeHistory()
        {
            var module = new CommandModule();
            var command = new ReentrantCommand(module);

            Assert.Throws<GameException>(() => module.ExecuteAsync(command).GetAwaiter().GetResult());
            Assert.AreEqual(0, module.UndoCount);
            Assert.AreEqual(0, module.RedoCount);
        }

        [Test]
        public void Shutdown_WhenHistoryExists_ReleasesHistoryAndClearsSubscriptions()
        {
            var module = new CommandModule();
            var command = new RecordingCommand("A");
            var eventCount = 0;
            module.HistoryChanged += _ => eventCount++;

            module.ExecuteAsync(command).GetAwaiter().GetResult();
            module.Shutdown().GetAwaiter().GetResult();
            module.ExecuteAsync(new RecordingCommand("B")).GetAwaiter().GetResult();

            Assert.AreEqual(1, command.ReleaseCount);
            Assert.AreEqual(0, module.RedoCount);
            Assert.AreEqual(1, module.UndoCount);
            Assert.AreEqual(2, eventCount);
        }

        private enum CommandPhase
        {
            Execute,
            Undo,
            Redo,
        }

        private class RecordingCommand : CommandBase
        {
            private readonly List<string> m_Events;
            private readonly CommandHistoryMode m_HistoryMode;

            public RecordingCommand(string name, List<string> events = null, CommandHistoryMode historyMode = CommandHistoryMode.Undoable)
            {
                Name = name;
                m_Events = events;
                m_HistoryMode = historyMode;
            }

            public override string Name { get; }

            public override CommandHistoryMode HistoryMode => m_HistoryMode;

            public int ExecuteCount { get; private set; }

            public int ReleaseCount { get; private set; }

            public override UniTask ExecuteAsync()
            {
                ExecuteCount++;
                m_Events?.Add($"execute:{Name}");
                return UniTask.CompletedTask;
            }

            public override UniTask UndoAsync()
            {
                m_Events?.Add($"undo:{Name}");
                return UniTask.CompletedTask;
            }

            public override UniTask RedoAsync()
            {
                m_Events?.Add($"redo:{Name}");
                return UniTask.CompletedTask;
            }

            public override void Release()
            {
                ReleaseCount++;
            }
        }

        private sealed class ThrowingCommand : RecordingCommand
        {
            private readonly CommandPhase m_Phase;

            public ThrowingCommand(string name, CommandPhase phase) : base(name)
            {
                m_Phase = phase;
            }

            public override UniTask ExecuteAsync()
            {
                if (m_Phase == CommandPhase.Execute)
                {
                    throw new InvalidOperationException("execute failed");
                }

                return base.ExecuteAsync();
            }

            public override UniTask UndoAsync()
            {
                if (m_Phase == CommandPhase.Undo)
                {
                    throw new InvalidOperationException("undo failed");
                }

                return base.UndoAsync();
            }

            public override UniTask RedoAsync()
            {
                if (m_Phase == CommandPhase.Redo)
                {
                    throw new InvalidOperationException("redo failed");
                }

                return base.RedoAsync();
            }
        }

        private sealed class ReentrantCommand : CommandBase
        {
            private readonly CommandModule m_Module;

            public ReentrantCommand(CommandModule module)
            {
                m_Module = module;
            }

            public override UniTask ExecuteAsync()
            {
                return m_Module.ExecuteAsync(new RecordingCommand("inner"));
            }

            public override UniTask UndoAsync()
            {
                return UniTask.CompletedTask;
            }
        }

        [Command("GM-ADD-ITEM")]
        private sealed class GmAddItemCommand : CommandBase
        {
            private readonly string m_ItemId;
            private readonly int m_Count;

            public GmAddItemCommand(string itemId, int count)
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

            public override string Name => "GM-ADD-ITEM";

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
