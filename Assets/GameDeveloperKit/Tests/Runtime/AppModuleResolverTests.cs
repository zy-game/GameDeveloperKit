using System;
using GameDeveloperKit.Download;
using GameDeveloperKit.Event;
using GameDeveloperKit.File;
using GameDeveloperKit.Operation;
using GameDeveloperKit.Resource;
using GameDeveloperKit.Timer;
using NUnit.Framework;

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
            TryUnregister<CircularModuleA>();
            TryUnregister<CircularModuleB>();
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
            Assert.IsFalse(App.TryGetValue<TimerModule>(out var module));
            Assert.IsNull(module);
            Assert.IsFalse(App.TryGetRegistered<TimerModule>(out _));
        }

        [Test]
        public void Startup_WhenCalled_DoesNotPreloadDefaultModules()
        {
            App.Startup().GetAwaiter().GetResult();

            Assert.IsFalse(App.TryGetRegistered<EventModule>(out _));
            Assert.IsFalse(App.TryGetRegistered<TimerModule>(out _));
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
            var exception = Assert.Throws<GameException>(() => App.GetModule<CircularModuleA>());

            StringAssert.Contains(nameof(CircularModuleA), exception.Message);
            StringAssert.Contains(nameof(CircularModuleB), exception.Message);
        }

        [Test]
        public void GetModule_WhenDependencyStartupFails_PreservesExistingDependencies()
        {
            ExistingModule.Startups = 0;
            FailingModule.Startups = 0;
            App.Register<ExistingModule>().GetAwaiter().GetResult();

            Assert.Throws<GameException>(() => App.GetModule<DependentOnFailingModule>());

            Assert.IsTrue(App.TryGetRegistered<ExistingModule>(out _));
            Assert.IsFalse(App.TryGetRegistered<FailingModule>(out _));
            Assert.IsFalse(App.TryGetRegistered<DependentOnFailingModule>(out _));
            Assert.AreEqual(1, ExistingModule.Startups);
            Assert.AreEqual(1, FailingModule.Startups);
        }

        [Test]
        public void GetModule_WhenTargetStartupFails_RollsBackNewDependencies()
        {
            CreatedDependencyModule.Startups = 0;
            CreatedDependencyModule.Shutdowns = 0;
            TargetFailingModule.Startups = 0;

            Assert.Throws<GameException>(() => App.GetModule<TargetFailingModule>());

            Assert.IsFalse(App.TryGetRegistered<CreatedDependencyModule>(out _));
            Assert.IsFalse(App.TryGetRegistered<TargetFailingModule>(out _));
            Assert.AreEqual(1, CreatedDependencyModule.Startups);
            Assert.AreEqual(1, CreatedDependencyModule.Shutdowns);
            Assert.AreEqual(1, TargetFailingModule.Startups);
        }

        [Test]
        public void Register_WhenModuleAlreadyResolved_Throws()
        {
            App.GetModule<TimerModule>();

            Assert.Throws<GameException>(() => App.Register<TimerModule>().GetAwaiter().GetResult());
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

            public override void Startup()
            {
                Startups++;
                throw new InvalidOperationException("target startup failed");
            }

            public override void Shutdown()
            {
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

            public override void Startup()
            {
                Startups++;
                throw new InvalidOperationException("startup failed");
            }

            public override void Shutdown()
            {
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

        [ModuleDependency(typeof(CircularModuleB))]
        public sealed class CircularModuleA : GameModuleBase
        {
            public override void Startup()
            {
            }

            public override void Shutdown()
            {
            }
        }

        [ModuleDependency(typeof(CircularModuleA))]
        public sealed class CircularModuleB : GameModuleBase
        {
            public override void Startup()
            {
            }

            public override void Shutdown()
            {
            }
        }
    }
}
