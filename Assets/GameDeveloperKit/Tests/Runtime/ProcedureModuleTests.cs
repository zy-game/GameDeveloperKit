using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Resource;
using Newtonsoft.Json;
using GameDeveloperKit.Procedure;
using GameDeveloperKit.Timer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class ProcedureModuleTests : RuntimeTestBase
    {
        private readonly List<string> m_TempFiles = new List<string>();

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

                try
                {
                    await App.Unregister<TimerModule>();
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

                foreach (var path in m_TempFiles)
                {
                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Delete(path);
                    }
                }

                m_TempFiles.Clear();
            });
        }

        [UnityTest]
        public IEnumerator Startup_WhenFrameworkStarts_RegistersProcedureModule()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Startup();

                Assert.IsNotNull(App.Procedure);
                Assert.IsTrue(App.TryGetRegistered<TimerModule>(out _));
                Assert.IsNull(GameObject.Find(ProcedureModule.RootName));
                Assert.AreSame(App.Procedure, FindTimerUpdateHandle(App.Timer, "ProcedureModule.Update").Owner);

                await App.Shutdown();
                Assert.IsFalse(App.TryGetRegistered<ProcedureModule>(out _));
            });
        }

        [UnityTest]
        public IEnumerator Register_WhenProcedureModuleIsRegistered_ReturnsProcedure()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Register<ProcedureModule>();

                Assert.IsNotNull(App.Procedure);
                Assert.IsTrue(App.TryGetRegistered<TimerModule>(out _));
                Assert.IsNull(GameObject.Find(ProcedureModule.RootName));
                Assert.AreSame(App.Procedure, FindTimerUpdateHandle(App.Timer, "ProcedureModule.Update").Owner);
            });
        }

        [Test]
        public void Startup_WhenCompleted_CurrentIsEmpty()
        {
            var module = new ProcedureModule();

            module.Startup();

            Assert.IsNull(module.Current);
            Assert.IsNull(module.CurrentType);
            Assert.IsFalse(module.IsChanging);
            Assert.IsNull(GameObject.Find(ProcedureModule.RootName));

            module.Shutdown();
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
                await App.Register<ProcedureModule>();
                var module = App.Procedure;
                var procedureA = new AProcedure();
                var procedureB = new BProcedure();
                module.RegisterProcedure(procedureA);
                module.RegisterProcedure(procedureB);

                await module.ChangeAsync<AProcedure>();
                App.Timer.Update(TimerTickKind.Update, 0.03f, 0.04f);

                var oldUpdateCount = procedureA.UpdateCount;
                await module.ChangeAsync<BProcedure>();
                App.Timer.Update(TimerTickKind.Update, 0.05f, 0.06f);

                Assert.AreEqual(1, oldUpdateCount);
                Assert.AreEqual(oldUpdateCount, procedureA.UpdateCount);
                Assert.AreEqual(1, procedureB.UpdateCount);
                Assert.AreEqual(0.05f, procedureB.LastDeltaTime, 0.0001f);
                Assert.AreEqual(0.06f, procedureB.LastUnscaledDeltaTime, 0.0001f);

                await App.Unregister<ProcedureModule>();
            });
        }

        [UnityTest]
        public IEnumerator Update_WhenChangeIsInitializingProcedure_SkipsCurrentUpdate()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Register<ProcedureModule>();
                var module = App.Procedure;
                var procedureA = new AProcedure();
                WaitingInitializeProcedure.Reset();
                module.RegisterProcedure(procedureA);
                await module.ChangeAsync<AProcedure>();
                App.Timer.Update(TimerTickKind.Update, 0.03f, 0.04f);

                var oldUpdateCount = procedureA.UpdateCount;
                var changeTask = module.ChangeAsync<WaitingInitializeProcedure>();
                await UniTask.Yield();

                var wasChanging = module.IsChanging;
                App.Timer.Update(TimerTickKind.Update, 0.03f, 0.04f);
                var updateCountDuringChange = procedureA.UpdateCount;
                WaitingInitializeProcedure.CompleteInitialize();
                await changeTask;
                var enteredTarget = module.Current is WaitingInitializeProcedure;
                await App.Unregister<ProcedureModule>();

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
        public void Startup_RegistersTimerUpdateHandle()
        {
            App.Register<ProcedureModule>().GetAwaiter().GetResult();

            var handle = FindTimerUpdateHandle(App.Timer, "ProcedureModule.Update");

            Assert.AreSame(App.Procedure, handle.Owner);
        }

        [Test]
        public void Shutdown_UnregistersTimerUpdateHandle()
        {
            App.Register<ProcedureModule>().GetAwaiter().GetResult();
            var procedure = App.Procedure;

            App.Unregister<ProcedureModule>().GetAwaiter().GetResult();

            foreach (var handle in App.Timer.Snapshot().Updates)
            {
                Assert.AreNotSame(procedure, handle.Owner);
                Assert.AreNotEqual("ProcedureModule.Update", handle.Tag);
            }
        }

        [Test]
        public void ProcedureModule_WhenInspected_DoesNotDeclareRuntimeDriver()
        {
            var nested = typeof(ProcedureModule).GetNestedType(
                "ProcedureRuntimeDriver",
                System.Reflection.BindingFlags.NonPublic);

            Assert.IsNull(nested);
        }

        [Test]
        public void TimerUpdate_WhenProcedureUpdateThrows_StoresExceptionOnHandle()
        {
            App.Register<ProcedureModule>().GetAwaiter().GetResult();
            App.Procedure.RegisterProcedure(new ThrowingUpdateProcedure());
            App.Procedure.ChangeAsync<ThrowingUpdateProcedure>().GetAwaiter().GetResult();

            App.Timer.Update(TimerTickKind.Update, 0.03f, 0.04f);

            var handle = FindTimerUpdateHandle(App.Timer, "ProcedureModule.Update");
            Assert.IsTrue(handle.HasError);
            Assert.IsInstanceOf<InvalidOperationException>(handle.LastException);
        }

        [Test]
        public void RequestChange_WhenCalledOutsideChange_ThrowsAndDoesNotLeavePending()
        {
            var module = new ProcedureModule();

            Assert.Throws<GameException>(() => module.RequestChange<AProcedure>());

            Assert.IsFalse(module.HasPendingChange);
            Assert.IsNull(module.PendingChangeType);
        }

        [Test]
        public void ChangeAsync_WhenProcedureRequestsChange_DrainsAfterEnter()
        {
            var module = new ProcedureModule();
            var bootstrap = new RequestingProcedure(module, "next", typeof(AProcedure));
            module.RegisterProcedure(bootstrap);
            module.RegisterProcedure(new AProcedure());

            module.ChangeAsync<RequestingProcedure>().GetAwaiter().GetResult();

            Assert.IsInstanceOf<AProcedure>(module.Current);
            Assert.IsTrue(bootstrap.ObservedPendingDuringEnter);
            Assert.AreEqual(typeof(AProcedure), bootstrap.ObservedPendingType);
            Assert.IsFalse(module.HasPendingChange);
        }

        [Test]
        public void ChangeAsync_WhenProcedureRequestsMultipleChanges_LastRequestWins()
        {
            var module = new ProcedureModule();
            var bootstrap = new RequestingProcedure(module, typeof(AProcedure), typeof(BProcedure));
            module.RegisterProcedure(bootstrap);
            module.RegisterProcedure(new AProcedure());
            module.RegisterProcedure(new BProcedure());

            module.ChangeAsync<RequestingProcedure>().GetAwaiter().GetResult();

            Assert.IsInstanceOf<BProcedure>(module.Current);
            Assert.IsFalse(module.HasPendingChange);
        }

        [UnityTest]
        public IEnumerator BootstrapProcedure_WhenResourceInitializes_EntersNextWithReadyModules()
        {
            return UniTask.ToCoroutine(async () =>
            {
                ResourceReadyProcedure.Reset();
                var module = App.Procedure;
                var settings = CreateResourceSettings(CreateManifestPath("bootstrap-success"));
                module.RegisterProcedure(new ResourceBootstrapProcedure(settings));
                module.RegisterProcedure(new ResourceReadyProcedure());

                await module.ChangeAsync<ResourceBootstrapProcedure>("enter-login");

                Assert.IsInstanceOf<ResourceReadyProcedure>(module.Current);
                Assert.IsTrue(App.Resource.IsInitialized);
                Assert.IsTrue(ResourceReadyProcedure.ResourceWasInitializedOnEnter);
                Assert.IsTrue(ResourceReadyProcedure.ConfigTagsWasAvailableOnEnter);
                Assert.AreEqual("enter-login", ResourceReadyProcedure.LastUserData);
            });
        }

        [UnityTest]
        public IEnumerator BootstrapProcedure_WhenResourceInitializeFails_DoesNotEnterNext()
        {
            return UniTask.ToCoroutine(async () =>
            {
                ResourceReadyProcedure.Reset();
                var module = App.Procedure;
                var missingManifest = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json");
                var settings = CreateResourceSettings(missingManifest);
                module.RegisterProcedure(new ResourceBootstrapProcedure(settings));
                module.RegisterProcedure(new ResourceReadyProcedure());

                var exception = await ThrowsAsync<GameException>(async () =>
                {
                    await module.ChangeAsync<ResourceBootstrapProcedure>();
                });

                StringAssert.Contains("Resource manifest initialize failed", exception.Message);
                Assert.IsFalse(module.HasPendingChange);
                Assert.IsFalse(ResourceReadyProcedure.Entered);
                Assert.IsNull(module.Current);
            });
        }

        [Test]
        public void Shutdown_WhenCurrentExists_LeavesReleasesAndCanRepeat()
        {
            var module = new ProcedureModule();
            var procedure = new AProcedure();
            module.Startup();
            module.RegisterProcedure(procedure);
            module.ChangeAsync<AProcedure>().GetAwaiter().GetResult();

            module.Shutdown();
            module.Shutdown();

            Assert.IsNull(module.Current);
            Assert.IsNull(GameObject.Find(ProcedureModule.RootName));
            Assert.AreEqual(1, procedure.LeaveCount);
            Assert.AreEqual(1, procedure.ReleaseCount);
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

            public float LastDeltaTime { get; private set; }

            public float LastUnscaledDeltaTime { get; private set; }

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
                LastDeltaTime = deltaTime;
                LastUnscaledDeltaTime = unscaledDeltaTime;
            }

            public override void Release()
            {
                ReleaseCount++;
            }
        }

        private string WriteTemp(string content)
        {
            var path = Path.GetTempFileName();
            System.IO.File.WriteAllText(path, content);
            m_TempFiles.Add(path);
            return path;
        }

        private string CreateManifestPath(string version)
        {
            var manifest = new ManifestInfo
            {
                Version = version,
                BuildTime = 1,
                Packages = new List<PackageInfo>(),
            };

            return WriteTemp(JsonConvert.SerializeObject(manifest));
        }

        private static ResourceSettings CreateResourceSettings(string manifestPath)
        {
            var settings = ScriptableObject.CreateInstance<ResourceSettings>();
            settings.Mode = ResourceMode.Offline;
            settings.ManifestName = manifestPath;
            settings.DefaultPackages = Array.Empty<string>();
            return settings;
        }

        private static async UniTask<TException> ThrowsAsync<TException>(Func<UniTask> action)
            where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException exception)
            {
                return exception;
            }

            Assert.Fail($"Expected exception of type {typeof(TException).FullName}.");
            return null;
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

        private sealed class ThrowingUpdateProcedure : ProcedureBase
        {
            public override void OnUpdate(float deltaTime, float unscaledDeltaTime)
            {
                throw new InvalidOperationException("update failed");
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

        private sealed class RequestingProcedure : ProcedureBase
        {
            private readonly ProcedureModule m_Module;
            private readonly Type[] m_TargetTypes;
            private readonly object m_UserData;

            public RequestingProcedure(ProcedureModule module, params Type[] targetTypes) : this(module, null, targetTypes)
            {
            }

            public RequestingProcedure(ProcedureModule module, object userData, params Type[] targetTypes)
            {
                m_Module = module;
                m_UserData = userData;
                m_TargetTypes = targetTypes;
            }

            public bool ObservedPendingDuringEnter { get; private set; }

            public Type ObservedPendingType { get; private set; }

            public override UniTask OnEnterAsync(ProcedureBase previous, object userData)
            {
                foreach (var targetType in m_TargetTypes)
                {
                    m_Module.RequestChange(targetType, m_UserData);
                    ObservedPendingDuringEnter = m_Module.HasPendingChange;
                    ObservedPendingType = m_Module.PendingChangeType;
                }

                return UniTask.CompletedTask;
            }
        }

        private sealed class ResourceBootstrapProcedure : ProcedureBase
        {
            private readonly ResourceSettings m_Settings;

            public ResourceBootstrapProcedure(ResourceSettings settings)
            {
                m_Settings = settings;
            }

            public override async UniTask OnEnterAsync(ProcedureBase previous, object userData)
            {
                await App.Resource.InitializeAsync(new ResourceInitializeOptions { Settings = m_Settings });
                _ = App.Config.Tags;
                App.Procedure.RequestChange<ResourceReadyProcedure>(userData);
            }
        }

        private sealed class ResourceReadyProcedure : ProcedureBase
        {
            public static bool Entered { get; private set; }

            public static bool ResourceWasInitializedOnEnter { get; private set; }

            public static bool ConfigTagsWasAvailableOnEnter { get; private set; }

            public static object LastUserData { get; private set; }

            public static void Reset()
            {
                Entered = false;
                ResourceWasInitializedOnEnter = false;
                ConfigTagsWasAvailableOnEnter = false;
                LastUserData = null;
            }

            public override UniTask OnEnterAsync(ProcedureBase previous, object userData)
            {
                Entered = true;
                ResourceWasInitializedOnEnter = App.Resource.IsInitialized;
                ConfigTagsWasAvailableOnEnter = App.Config.Tags != null;
                LastUserData = userData;
                return UniTask.CompletedTask;
            }
        }
    }
}
