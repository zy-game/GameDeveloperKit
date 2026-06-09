using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Procedure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class ProcedureModuleTests : RuntimeTestBase
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                try
                {
                    await App.Shutdown();
                }
                catch (GameException)
                {
                }

                try
                {
                    await App.Unregister<ProcedureModule>();
                }
                catch (GameException)
                {
                }

                var root = GameObject.Find(ProcedureModule.RootName);
                if (root != null)
                {
                    UnityEngine.Object.Destroy(root);
                    await UniTask.Yield();
                }
            });
        }

        [UnityTest]
        public IEnumerator Startup_WhenFrameworkStarts_RegistersProcedureModule()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Startup();

                Assert.IsNotNull(App.Procedure);
                Assert.IsNotNull(GameObject.Find(ProcedureModule.RootName));

                await App.Shutdown();
                Assert.Throws<GameException>(() =>
                {
                    var _ = App.Procedure;
                });
            });
        }

        [UnityTest]
        public IEnumerator Register_WhenProcedureModuleIsRegistered_ReturnsProcedure()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Register<ProcedureModule>();

                Assert.IsNotNull(App.Procedure);
                Assert.IsNotNull(GameObject.Find(ProcedureModule.RootName));
            });
        }

        [Test]
        public void Startup_WhenCompleted_CurrentIsEmpty()
        {
            var module = new ProcedureModule();

            module.Startup().GetAwaiter().GetResult();

            Assert.IsNull(module.Current);
            Assert.IsNull(module.CurrentType);
            Assert.IsFalse(module.IsChanging);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [Test]
        public void RegisterProcedure_WhenInstanceIsRegistered_CanQuerySameInstance()
        {
            var module = new ProcedureModule();
            var procedure = new AProcedure();

            module.RegisterProcedure(procedure);

            Assert.IsTrue(module.HasProcedure<AProcedure>());
            Assert.IsTrue(module.TryGetProcedure<AProcedure>(out var result));
            Assert.AreSame(procedure, result);
            Assert.AreEqual(1, procedure.InitializeCount);
        }

        [Test]
        public void RegisterProcedure_WhenDuplicateType_ThrowsWithoutReplacing()
        {
            var module = new ProcedureModule();
            var first = new AProcedure();
            var second = new AProcedure();

            module.RegisterProcedure(first);

            Assert.Throws<GameException>(() => module.RegisterProcedure(second));
            Assert.IsTrue(module.TryGetProcedure<AProcedure>(out var result));
            Assert.AreSame(first, result);
        }

        [Test]
        public void ChangeAsync_WhenProcedureIsMissing_LazilyCreatesAndEnters()
        {
            LazyProcedure.Reset();
            var module = new ProcedureModule();

            module.ChangeAsync<LazyProcedure>("start").GetAwaiter().GetResult();

            Assert.IsInstanceOf<LazyProcedure>(module.Current);
            Assert.AreEqual(typeof(LazyProcedure), module.CurrentType);
            Assert.IsTrue(module.HasProcedure<LazyProcedure>());
            Assert.AreEqual(1, LazyProcedure.InitializeCount);
            Assert.AreEqual(1, LazyProcedure.EnterCount);
            Assert.AreEqual("start", LazyProcedure.LastUserData);
        }

        [Test]
        public void ChangeAsync_WhenSwitchingProcedures_LeavesThenEnters()
        {
            var events = new List<string>();
            var module = new ProcedureModule();
            var procedureA = new AProcedure(events);
            var procedureB = new BProcedure(events);
            module.RegisterProcedure(procedureA);
            module.RegisterProcedure(procedureB);
            module.ChangeAsync<AProcedure>("first").GetAwaiter().GetResult();
            module.ChangeAsync<BProcedure>("second").GetAwaiter().GetResult();

            CollectionAssert.AreEqual(
                new[]
                {
                    "init:A",
                    "init:B",
                    "enter:A:null:first",
                    "leave:A:BProcedure:second",
                    "enter:B:AProcedure:second",
                },
                events);
            Assert.AreSame(procedureB, module.Current);
        }

        [Test]
        public void ChangeAsync_WhenTargetIsCurrent_IsNoOp()
        {
            var events = new List<string>();
            var module = new ProcedureModule();
            var procedure = new AProcedure(events);
            module.RegisterProcedure(procedure);

            module.ChangeAsync<AProcedure>().GetAwaiter().GetResult();
            module.ChangeAsync<AProcedure>().GetAwaiter().GetResult();

            Assert.AreEqual(1, procedure.EnterCount);
            Assert.AreEqual(0, procedure.LeaveCount);
        }

        [UnityTest]
        public IEnumerator Update_WhenCurrentChanges_OnlyUpdatesCurrentProcedure()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = new ProcedureModule();
                var procedureA = new AProcedure();
                var procedureB = new BProcedure();
                await module.Startup();
                module.RegisterProcedure(procedureA);
                module.RegisterProcedure(procedureB);

                await module.ChangeAsync<AProcedure>();
                await WaitForUpdateCountAsync(procedureA, 1);

                var oldUpdateCount = procedureA.UpdateCount;
                await module.ChangeAsync<BProcedure>();
                await WaitForUpdateCountAsync(procedureB, 1);

                Assert.AreEqual(oldUpdateCount, procedureA.UpdateCount);

                await module.Shutdown();
            });
        }

        [UnityTest]
        public IEnumerator Update_WhenChangeIsInitializingProcedure_SkipsCurrentUpdate()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = new ProcedureModule();
                var procedureA = new AProcedure();
                WaitingInitializeProcedure.Reset();
                await module.Startup();
                module.RegisterProcedure(procedureA);
                await module.ChangeAsync<AProcedure>();
                await UniTask.Yield();

                var oldUpdateCount = procedureA.UpdateCount;
                var changeTask = module.ChangeAsync<WaitingInitializeProcedure>();
                await UniTask.Yield();

                var wasChanging = module.IsChanging;
                var updateCountDuringChange = procedureA.UpdateCount;
                WaitingInitializeProcedure.CompleteInitialize();
                await changeTask;
                var enteredTarget = module.Current is WaitingInitializeProcedure;
                await module.Shutdown();

                Assert.IsTrue(wasChanging);
                Assert.AreEqual(oldUpdateCount, updateCountDuringChange);
                Assert.IsTrue(enteredTarget);
            });
        }

        [Test]
        public void ChangeAsync_WhenTypeIsInvalid_Throws()
        {
            var module = new ProcedureModule();

            Assert.Throws<ArgumentNullException>(() => module.ChangeAsync(null).GetAwaiter().GetResult());
            Assert.Throws<GameException>(() => module.ChangeAsync(typeof(string)).GetAwaiter().GetResult());
            Assert.Throws<GameException>(() => module.ChangeAsync(typeof(AbstractProcedure)).GetAwaiter().GetResult());
            Assert.Throws<GameException>(() => module.ChangeAsync(typeof(ConstructorOnlyProcedure)).GetAwaiter().GetResult());
        }

        [Test]
        public void RegisterProcedure_WhenProcedureIsNull_Throws()
        {
            var module = new ProcedureModule();

            Assert.Throws<ArgumentNullException>(() => module.RegisterProcedure(null));
        }

        [Test]
        public void ChangeAsync_WhenLeaveThrows_KeepsCurrent()
        {
            var module = new ProcedureModule();
            var procedureA = new ThrowingLeaveProcedure();
            var procedureB = new BProcedure();
            module.RegisterProcedure(procedureA);
            module.RegisterProcedure(procedureB);
            module.ChangeAsync<ThrowingLeaveProcedure>().GetAwaiter().GetResult();

            Assert.Throws<InvalidOperationException>(() => module.ChangeAsync<BProcedure>().GetAwaiter().GetResult());
            Assert.AreSame(procedureA, module.Current);
            Assert.AreEqual(0, procedureB.EnterCount);
        }

        [Test]
        public void ChangeAsync_WhenEnterThrows_ClearsCurrent()
        {
            var module = new ProcedureModule();
            var procedureA = new AProcedure();
            var procedureB = new ThrowingEnterProcedure();
            module.RegisterProcedure(procedureA);
            module.RegisterProcedure(procedureB);
            module.ChangeAsync<AProcedure>().GetAwaiter().GetResult();

            Assert.Throws<InvalidOperationException>(() => module.ChangeAsync<ThrowingEnterProcedure>().GetAwaiter().GetResult());
            Assert.IsNull(module.Current);
            Assert.AreEqual(1, procedureA.LeaveCount);
        }

        [Test]
        public void ChangeAsync_WhenProcedureReenters_ThrowsAndDoesNotStartSecondChange()
        {
            var module = new ProcedureModule();
            var reentrant = new ReentrantProcedure(module);
            module.RegisterProcedure(reentrant);

            Assert.Throws<GameException>(() => module.ChangeAsync<ReentrantProcedure>().GetAwaiter().GetResult());
            Assert.IsNull(module.Current);
        }

        [Test]
        public void Shutdown_WhenCurrentExists_LeavesReleasesAndCanRepeat()
        {
            var module = new ProcedureModule();
            var procedure = new AProcedure();
            module.Startup().GetAwaiter().GetResult();
            module.RegisterProcedure(procedure);
            module.ChangeAsync<AProcedure>().GetAwaiter().GetResult();

            module.Shutdown().GetAwaiter().GetResult();
            module.Shutdown().GetAwaiter().GetResult();

            Assert.IsNull(module.Current);
            Assert.IsNull(GameObject.Find(ProcedureModule.RootName));
            Assert.AreEqual(1, procedure.LeaveCount);
            Assert.AreEqual(1, procedure.ReleaseCount);
        }

        private abstract class RecordingProcedure : ProcedureBase
        {
            private readonly List<string> m_Events;

            protected RecordingProcedure(string name, List<string> events = null)
            {
                Name = name;
                m_Events = events;
            }

            public string Name { get; }

            public int InitializeCount { get; private set; }

            public int EnterCount { get; private set; }

            public int LeaveCount { get; private set; }

            public int UpdateCount { get; private set; }

            public int ReleaseCount { get; private set; }

            public override UniTask OnInitializeAsync()
            {
                InitializeCount++;
                m_Events?.Add($"init:{Name}");
                return UniTask.CompletedTask;
            }

            public override UniTask OnEnterAsync(ProcedureBase previous, object userData)
            {
                EnterCount++;
                m_Events?.Add($"enter:{Name}:{previous?.GetType().Name ?? "null"}:{userData}");
                return UniTask.CompletedTask;
            }

            public override UniTask OnLeaveAsync(ProcedureBase next, object userData)
            {
                LeaveCount++;
                m_Events?.Add($"leave:{Name}:{next?.GetType().Name ?? "null"}:{userData}");
                return UniTask.CompletedTask;
            }

            public override void OnUpdate(float deltaTime, float unscaledDeltaTime)
            {
                UpdateCount++;
            }

            public override void Release()
            {
                ReleaseCount++;
            }
        }

        private static async UniTask WaitForUpdateCountAsync(RecordingProcedure procedure, int minimumCount)
        {
            var guard = 0;
            while (procedure.UpdateCount < minimumCount && guard < 120)
            {
                guard++;
                await UniTask.Yield();
            }

            Assert.GreaterOrEqual(procedure.UpdateCount, minimumCount);
        }

        private sealed class AProcedure : RecordingProcedure
        {
            public AProcedure(List<string> events = null) : base("A", events)
            {
            }
        }

        private sealed class BProcedure : RecordingProcedure
        {
            public BProcedure(List<string> events = null) : base("B", events)
            {
            }
        }

        private sealed class LazyProcedure : ProcedureBase
        {
            public static int InitializeCount { get; private set; }

            public static int EnterCount { get; private set; }

            public static object LastUserData { get; private set; }

            public static void Reset()
            {
                InitializeCount = 0;
                EnterCount = 0;
                LastUserData = null;
            }

            public override UniTask OnInitializeAsync()
            {
                InitializeCount++;
                return UniTask.CompletedTask;
            }

            public override UniTask OnEnterAsync(ProcedureBase previous, object userData)
            {
                EnterCount++;
                LastUserData = userData;
                return UniTask.CompletedTask;
            }
        }

        private sealed class WaitingInitializeProcedure : ProcedureBase
        {
            private static UniTaskCompletionSource s_CompletionSource;

            public static void Reset()
            {
                s_CompletionSource = new UniTaskCompletionSource();
            }

            public static void CompleteInitialize()
            {
                s_CompletionSource.TrySetResult();
            }

            public override UniTask OnInitializeAsync()
            {
                return s_CompletionSource.Task;
            }
        }

        private abstract class AbstractProcedure : ProcedureBase
        {
        }

        private sealed class ConstructorOnlyProcedure : ProcedureBase
        {
            public ConstructorOnlyProcedure(string value)
            {
            }
        }

        private sealed class ThrowingLeaveProcedure : RecordingProcedure
        {
            public ThrowingLeaveProcedure() : base("throw-leave")
            {
            }

            public override UniTask OnLeaveAsync(ProcedureBase next, object userData)
            {
                base.OnLeaveAsync(next, userData);
                throw new InvalidOperationException("leave failed");
            }
        }

        private sealed class ThrowingEnterProcedure : RecordingProcedure
        {
            public ThrowingEnterProcedure() : base("throw-enter")
            {
            }

            public override UniTask OnEnterAsync(ProcedureBase previous, object userData)
            {
                base.OnEnterAsync(previous, userData);
                throw new InvalidOperationException("enter failed");
            }
        }

        private sealed class ReentrantProcedure : ProcedureBase
        {
            private readonly ProcedureModule m_Module;

            public ReentrantProcedure(ProcedureModule module)
            {
                m_Module = module;
            }

            public override UniTask OnEnterAsync(ProcedureBase previous, object userData)
            {
                return m_Module.ChangeAsync<BProcedure>();
            }
        }
    }
}
