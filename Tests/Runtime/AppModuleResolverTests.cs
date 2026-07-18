using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Download;
using GameDeveloperKit.Event;
using GameDeveloperKit.File;
using GameDeveloperKit.Operation;
using GameDeveloperKit.Resource;
using GameDeveloperKit.Timer;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class AppModuleResolverTests : RuntimeTestBase
    {
        [TearDown]
        public void TearDown()
        {
            TryUnregister<EventModule>();
            TryUnregister<TimerModule>();
            TryUnregister<ResourceModule>();
            TryUnregister<DownloadModule>();
            TryUnregister<FileModule>();
            TryUnregister<OperationModule>();
            TryUnregister<TargetFailingModule>();
            TryUnregister<CreatedDependencyModule>();
            TryUnregister<DependentOnFailingModule>();
            TryUnregister<FailingModule>();
            TryUnregister<ExistingModule>();
            TryUnregister<ExistingDependentOnFailingModule>();
            TryUnregister<ExistingFailingModule>();
            TryUnregister<CircularAModule>();
            TryUnregister<CircularBModule>();
            TryUnregister<AsyncShutdownAModule>();
            TryUnregister<AsyncShutdownBModule>();
            TryUnregister<CleanupFailingTargetModule>();
            TryUnregister<RollbackFailingDependencyModule>();
        }

        [Test]
        public void GetModule_WhenModuleHasDependency_StartsDependencyBeforeTarget()
        {
            var module = App.GetModule<EventModule>();

            Assert.IsNotNull(module);
            Assert.IsTrue(App.TryGetRegistered<TimerModule>(out var timer));
            Assert.IsNotNull(timer);
            Assert.AreSame(module, App.Event);
        }

        [Test]
        public void GetModule_WhenResourceHasDependencyChain_StartsDeclaredDependencies()
        {
            var module = App.GetModule<ResourceModule>();

            Assert.IsNotNull(module);
            Assert.IsTrue(App.TryGetRegistered<OperationModule>(out _));
            Assert.IsTrue(App.TryGetRegistered<DownloadModule>(out _));
            Assert.IsTrue(App.TryGetRegistered<FileModule>(out _));
            Assert.AreSame(module, App.Resource);
        }

        [Test]
        public void TryGetValue_WhenModuleIsNotRegistered_DoesNotCreateModule()
        {
            Assert.IsFalse(App.TryGetRegistered<TimerModule>(out var module));
            Assert.IsNull(module);
            Assert.IsFalse(App.TryGetRegistered<TimerModule>(out _));
        }

        [Test]
        public void Startup_WhenCalled_DoesNotPreloadDefaultModules()
        {
            App.Initialize().GetAwaiter().GetResult();

            Assert.IsFalse(App.TryGetRegistered<EventModule>(out _));
            Assert.IsFalse(App.TryGetRegistered<TimerModule>(out _));
        }

        [Test]
        public void SubsystemRegistration_WhenPreviousSessionHasModules_ReplacesEntireAppState()
        {
            var previous = App.GetModule<ExistingModule>();
            var reset = typeof(App).GetMethod("ResetStaticState", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(reset);
            reset.Invoke(null, null);

            Assert.IsFalse(App.TryGetRegistered<ExistingModule>(out _));
            App.Initialize().GetAwaiter().GetResult();
            var current = App.GetModule<ExistingModule>();
            Assert.AreNotSame(previous, current);
        }

        [Test]
        public void GetModule_WhenCalledTwice_ReturnsSameInstance()
        {
            var first = App.GetModule<TimerModule>();
            var second = App.GetModule<TimerModule>();

            Assert.AreSame(first, second);
        }

        [Test]
        public void GetModule_WhenCircularDependencyExists_ThrowsWithDependencyChain()
        {
            var exception = Assert.Throws<GameException>(() => App.GetModule<CircularAModule>());

            StringAssert.Contains(nameof(CircularAModule), exception.Message);
            StringAssert.Contains(nameof(CircularBModule), exception.Message);
        }

        [Test]
        public void GetModule_WhenDependencyStartupFails_PreservesExistingDependencies()
        {
            ExistingModule.Startups = 0;
            FailingModule.Startups = 0;
            FailingModule.Shutdowns = 0;
            App.Register<ExistingModule>();

            Assert.Throws<GameException>(() => App.GetModule<DependentOnFailingModule>());

            Assert.IsTrue(App.TryGetRegistered<ExistingModule>(out _));
            Assert.IsFalse(App.TryGetRegistered<FailingModule>(out _));
            Assert.IsFalse(App.TryGetRegistered<DependentOnFailingModule>(out _));
            Assert.AreEqual(1, ExistingModule.Startups);
            Assert.AreEqual(1, FailingModule.Startups);
            Assert.AreEqual(1, FailingModule.Shutdowns);
        }

        [Test]
        public void GetModule_WhenTargetStartupFails_RollsBackNewDependencies()
        {
            CreatedDependencyModule.Startups = 0;
            CreatedDependencyModule.Shutdowns = 0;
            TargetFailingModule.Startups = 0;
            TargetFailingModule.Shutdowns = 0;

            Assert.Throws<GameException>(() => App.GetModule<TargetFailingModule>());

            Assert.IsFalse(App.TryGetRegistered<CreatedDependencyModule>(out _));
            Assert.IsFalse(App.TryGetRegistered<TargetFailingModule>(out _));
            Assert.AreEqual(1, CreatedDependencyModule.Startups);
            Assert.AreEqual(1, CreatedDependencyModule.Shutdowns);
            Assert.AreEqual(1, TargetFailingModule.Startups);
            Assert.AreEqual(1, TargetFailingModule.Shutdowns);
        }

        [Test]
        public void GetModule_WhenFailedInstanceCleanupAndDependencyRollbackThrow_PreservesAllFailures()
        {
            CleanupFailingTargetModule.Reset();
            RollbackFailingDependencyModule.Reset();

            var exception = Assert.Throws<AggregateException>(() => App.GetModule<CleanupFailingTargetModule>());

            Assert.AreEqual(2, exception.InnerExceptions.Count);
            var startupFailure = exception.InnerExceptions[0] as GameException;
            Assert.IsNotNull(startupFailure);
            var failedInstanceFailures = startupFailure.InnerException as AggregateException;
            Assert.IsNotNull(failedInstanceFailures);
            Assert.AreEqual(2, failedInstanceFailures.InnerExceptions.Count);
            Assert.AreEqual("target startup failed", failedInstanceFailures.InnerExceptions[0].Message);
            Assert.AreEqual("target cleanup failed", failedInstanceFailures.InnerExceptions[1].Message);
            Assert.AreEqual("dependency rollback failed", exception.InnerExceptions[1].Message);

            Assert.AreEqual(1, CleanupFailingTargetModule.Startups);
            Assert.AreEqual(1, CleanupFailingTargetModule.Shutdowns);
            Assert.AreEqual(1, RollbackFailingDependencyModule.Startups);
            Assert.AreEqual(1, RollbackFailingDependencyModule.Shutdowns);
            Assert.IsFalse(App.TryGetRegistered<CleanupFailingTargetModule>(out _));
            Assert.IsFalse(App.TryGetRegistered<RollbackFailingDependencyModule>(out _));
        }

        [Test]
        public void Register_WhenModuleAlreadyResolved_Throws()
        {
            App.GetModule<TimerModule>();

            Assert.Throws<GameException>(() => App.Register<TimerModule>());
        }

        [UnityTest]
        public IEnumerator Shutdown_WhenModulesPrepareAsynchronously_CompletesAllPrepareBeforeShutdown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                AsyncShutdownModule.Reset();
                App.Register<AsyncShutdownAModule>();
                App.Register<AsyncShutdownBModule>();

                var shutdownTask = App.Shutdown();
                await UniTask.Yield();

                CollectionAssert.AreEqual(new[] { "prepare:B" }, AsyncShutdownModule.Events);
                Assert.AreEqual(UniTaskStatus.Pending, shutdownTask.Status);

                AsyncShutdownModule.CompletePendingPrepare();
                await shutdownTask;

                CollectionAssert.AreEqual(
                    new[] { "prepare:B", "prepare:A", "shutdown:B", "shutdown:A" },
                    AsyncShutdownModule.Events);
            });
        }

        private static void TryUnregister<TModule>() where TModule : class, IGameModule
        {
            try
            {
                App.Unregister<TModule>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }
        }

        public sealed class ExistingModule : GameModuleBase
        {
            public static int Startups;

            public override void Startup()
            {
                Startups++;
            }

            public override void Shutdown()
            {
            }
        }

        public sealed class CreatedDependencyModule : GameModuleBase
        {
            public static int Startups;
            public static int Shutdowns;

            public override void Startup()
            {
                Startups++;
            }

            public override void Shutdown()
            {
                Shutdowns++;
            }
        }

        [ModuleDependency(typeof(CreatedDependencyModule))]
        public sealed class TargetFailingModule : GameModuleBase
        {
            public static int Startups;

            public static int Shutdowns;

            public override void Startup()
            {
                Startups++;
                throw new InvalidOperationException("target startup failed");
            }

            public override void Shutdown()
            {
                Shutdowns++;
            }
        }

        [ModuleDependency(typeof(FailingModule))]
        public sealed class DependentOnFailingModule : GameModuleBase
        {
            public override void Startup()
            {
            }

            public override void Shutdown()
            {
            }
        }

        [ModuleDependency(typeof(ExistingModule))]
        public sealed class FailingModule : GameModuleBase
        {
            public static int Startups;

            public static int Shutdowns;

            public override void Startup()
            {
                Startups++;
                throw new InvalidOperationException("startup failed");
            }

            public override void Shutdown()
            {
                Shutdowns++;
            }
        }

        [ModuleDependency(typeof(ExistingFailingModule))]
        public sealed class ExistingDependentOnFailingModule : GameModuleBase
        {
            public override void Startup()
            {
            }

            public override void Shutdown()
            {
            }
        }

        public sealed class ExistingFailingModule : GameModuleBase
        {
            public override void Startup()
            {
                throw new InvalidOperationException("startup failed");
            }

            public override void Shutdown()
            {
            }
        }

        [ModuleDependency(typeof(CircularBModule))]
        public sealed class CircularAModule : GameModuleBase
        {
            public override void Startup()
            {
            }

            public override void Shutdown()
            {
            }
        }

        [ModuleDependency(typeof(CircularAModule))]
        public sealed class CircularBModule : GameModuleBase
        {
            public override void Startup()
            {
            }

            public override void Shutdown()
            {
            }
        }

        public abstract class AsyncShutdownModule : GameModuleBase, IAsyncShutdownParticipant
        {
            private static UniTaskCompletionSource s_PendingPrepare;

            protected AsyncShutdownModule(string name, bool waitDuringPrepare)
            {
                Name = name;
                WaitDuringPrepare = waitDuringPrepare;
            }

            public static List<string> Events { get; } = new List<string>();

            protected string Name { get; }

            private bool WaitDuringPrepare { get; }

            public static void Reset()
            {
                Events.Clear();
                s_PendingPrepare = new UniTaskCompletionSource();
            }

            public static void CompletePendingPrepare()
            {
                s_PendingPrepare.TrySetResult();
            }

            public override void Startup()
            {
            }

            public override void Shutdown()
            {
                Events.Add($"shutdown:{Name}");
            }

            async UniTask IAsyncShutdownParticipant.PrepareShutdownAsync()
            {
                Events.Add($"prepare:{Name}");
                if (WaitDuringPrepare)
                {
                    await s_PendingPrepare.Task;
                }
            }
        }

        public sealed class AsyncShutdownAModule : AsyncShutdownModule
        {
            public AsyncShutdownAModule() : base("A", false)
            {
            }
        }

        public sealed class AsyncShutdownBModule : AsyncShutdownModule
        {
            public AsyncShutdownBModule() : base("B", true)
            {
            }
        }

        [ModuleDependency(typeof(RollbackFailingDependencyModule))]
        public sealed class CleanupFailingTargetModule : GameModuleBase
        {
            public static int Startups { get; private set; }

            public static int Shutdowns { get; private set; }

            public static void Reset()
            {
                Startups = 0;
                Shutdowns = 0;
            }

            public override void Startup()
            {
                Startups++;
                throw new InvalidOperationException("target startup failed");
            }

            public override void Shutdown()
            {
                Shutdowns++;
                throw new InvalidOperationException("target cleanup failed");
            }
        }

        public sealed class RollbackFailingDependencyModule : GameModuleBase
        {
            public static int Startups { get; private set; }

            public static int Shutdowns { get; private set; }

            public static void Reset()
            {
                Startups = 0;
                Shutdowns = 0;
            }

            public override void Startup()
            {
                Startups++;
            }

            public override void Shutdown()
            {
                Shutdowns++;
                throw new InvalidOperationException("dependency rollback failed");
            }
        }
    }
}
