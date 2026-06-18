using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Combat;
using GameDeveloperKit.Timer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CombatWorld = GameDeveloperKit.Combat.World;

namespace GameDeveloperKit.Tests
{
    public sealed class CombatModuleTests : RuntimeTestBase
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                try
                {
                    await App.Unregister<CombatModule>();
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

                var root = GameObject.Find(CombatModule.RootName);
                if (root != null)
                {
                    UnityEngine.Object.Destroy(root);
                    await UniTask.Yield();
                }
            });
        }

        [UnityTest]
        public IEnumerator Register_WhenCombatModuleIsRegistered_ReturnsWorld()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Register<CombatModule>();

                Assert.IsNotNull(App.Combat);
                Assert.IsNotNull(App.Combat.World);
                Assert.IsTrue(App.TryGetRegistered<TimerModule>(out _));
                Assert.IsNull(GameObject.Find(CombatModule.RootName));
                Assert.AreSame(App.Combat, FindTimerUpdateHandle(App.Timer, "CombatModule.Update").Owner);

                await App.Unregister<CombatModule>();
                Assert.IsFalse(App.TryGetRegistered<CombatModule>(out _));
            });
        }

        [UnityTest]
        public IEnumerator Shutdown_WhenCalledRepeatedly_IsSafe()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Register<TimerModule>();
                var module = new CombatModule();
                module.Startup();

                Assert.IsNotNull(module.World);
                Assert.IsNull(GameObject.Find(CombatModule.RootName));
                Assert.AreSame(module, FindTimerUpdateHandle(App.Timer, "CombatModule.Update").Owner);

                module.Shutdown();
                await UniTask.Yield();
                module.Shutdown();

                Assert.IsNull(module.World);
                Assert.IsNull(GameObject.Find(CombatModule.RootName));
            });
        }

        [Test]
        public void Startup_WhenTimerIsMissing_ThrowsWithoutCreatingWorld()
        {
            var module = new CombatModule();

            Assert.Throws<GameException>(() => module.Startup());

            Assert.IsNull(module.World);
            Assert.IsNull(GameObject.Find(CombatModule.RootName));
        }

        [Test]
        public void Startup_RegistersTimerFixedUpdateHandle()
        {
            App.Register<CombatModule>().GetAwaiter().GetResult();

            var handle = FindTimerUpdateHandle(App.Timer, "CombatModule.Update");

            Assert.AreSame(App.Combat, handle.Owner);
            Assert.IsInstanceOf<FixedUpdateTimerHandle>(handle);
        }

        [Test]
        public void TimerFixedUpdate_WhenTriggered_UpdatesDefaultWorld()
        {
            App.Register<CombatModule>().GetAwaiter().GetResult();
            App.Combat.World.FrameRate = 10;

            App.Timer.Update(TimerTickKind.Update, 0.25f, 0.25f);
            App.Timer.Update(TimerTickKind.FixedUpdate, 0.02f, 0.02f);

            Assert.AreEqual(2, App.Combat.World.Tick);
            Assert.AreEqual(0.2f, App.Combat.World.Time, 0.0001f);
        }

        [Test]
        public void TimerUpdateAndLateUpdate_WhenTriggered_DoNotUpdateDefaultWorld()
        {
            App.Register<CombatModule>().GetAwaiter().GetResult();
            App.Combat.World.FrameRate = 10;

            App.Timer.Update(TimerTickKind.Update, 0.25f, 0.25f);
            App.Timer.Update(TimerTickKind.LateUpdate, 0.25f, 0.25f);

            Assert.AreEqual(0, App.Combat.World.Tick);
            Assert.AreEqual(0f, App.Combat.World.Time, 0.0001f);
        }

        [Test]
        public void Shutdown_UnregistersTimerUpdateHandle()
        {
            App.Register<CombatModule>().GetAwaiter().GetResult();
            var combat = App.Combat;

            App.Unregister<CombatModule>().GetAwaiter().GetResult();

            foreach (var handle in App.Timer.Snapshot().Updates)
            {
                Assert.AreNotSame(combat, handle.Owner);
                Assert.AreNotEqual("CombatModule.Update", handle.Tag);
            }
        }

        [Test]
        public void CombatModule_WhenInspected_DoesNotDeclareRuntimeDriver()
        {
            var nested = typeof(CombatModule).GetNestedType(
                "CombatRuntimeDriver",
                System.Reflection.BindingFlags.NonPublic);

            Assert.IsNull(nested);
        }

        [Test]
        public void TimerFixedUpdate_WhenWorldUpdateThrows_StoresExceptionOnHandle()
        {
            App.Register<CombatModule>().GetAwaiter().GetResult();
            App.Combat.World.Dispose();

            App.Timer.Update(TimerTickKind.Update, 0.02f, 0.02f);
            App.Timer.Update(TimerTickKind.FixedUpdate, 0.02f, 0.02f);

            var handle = FindTimerUpdateHandle(App.Timer, "CombatModule.Update");
            Assert.IsTrue(handle.HasError);
            Assert.IsInstanceOf<GameException>(handle.LastException);
        }

        [Test]
        public void EntityManager_WhenComponentsChange_StoresAndRemovesComponents()
        {
            using (var world = new CombatWorld())
            {
                var entity = world.Create();

                entity.AddComponent(new Health { Value = 100 });

                Assert.IsTrue(entity.IsAlive);
                Assert.GreaterOrEqual(entity.Id, 0);
                Assert.Greater(entity.Version, 0U);
                Assert.IsTrue(entity.HasComponent<Health>());
                Assert.AreEqual(100, entity.GetComponent<Health>().Value);
                Assert.IsTrue(entity.RemoveComponent<Health>());
                Assert.IsFalse(entity.HasComponent<Health>());
            }
        }

        [Test]
        public void SystemManager_WhenComponentsChange_TriggersLifecycleOnce()
        {
            using (var world = new CombatWorld())
            {
                var system = world.LoadSystem<HealthSystem>();
                var entity = world.Create();

                entity.AddComponent(new Health());
                entity.AddComponent<Dead>();
                entity.RemoveComponent<Dead>();
                entity.RemoveComponent<Health>();
                world.Step();

                Assert.AreEqual(2, system.Created.Count);
                Assert.AreEqual(2, system.Destroyed.Count);
                Assert.AreEqual(0, system.Updated.Count);
                Assert.AreSame(entity, system.Created[0]);
                Assert.AreSame(entity, system.Destroyed[0]);
            }
        }

        [Test]
        public void SystemManager_WhenSystemIsAddedAfterExistingEntity_CreatesMatchingEntity()
        {
            using (var world = new CombatWorld())
            {
                var entity = world.Create();
                entity.AddComponent(new Health());

                var system = world.LoadSystem<HealthSystem>();

                Assert.AreEqual(1, system.Created.Count);
                Assert.AreSame(entity, system.Created[0]);
            }
        }

        [Test]
        public void WorldUpdate_WhenDeltaIsBelowFixedStep_DoesNotUpdateSystems()
        {
            using (var world = new CombatWorld(10))
            {
                var system = world.LoadSystem<AnySystem>();
                world.Create();

                world.Update(0.05f);

                Assert.AreEqual(1, system.Created.Count);
                Assert.AreEqual(0, world.Tick);
                Assert.AreEqual(0f, world.Time);
                Assert.AreEqual(0, system.Updated.Count);
            }
        }

        [Test]
        public void WorldUpdate_WhenDeltaReachesFixedStep_UpdatesByFixedSteps()
        {
            using (var world = new CombatWorld(10))
            {
                var system = world.LoadSystem<AnySystem>();
                world.Create();

                world.Update(0.25f);

                Assert.AreEqual(2, world.Tick);
                Assert.AreEqual(0.2f, world.Time, 0.0001f);
                Assert.AreEqual(2, system.Updated.Count);
            }
        }

        [Test]
        public void Destroy_WhenEntityMatchesSystem_TriggersDestroyAndMarksDead()
        {
            using (var world = new CombatWorld())
            {
                var system = world.LoadSystem<AnySystem>();
                var entity = world.Create();

                Assert.IsTrue(world.Destroy(entity));

                Assert.IsFalse(entity.IsAlive);
                Assert.AreEqual(1, system.Destroyed.Count);
                Assert.AreSame(entity, system.Destroyed[0]);
                Assert.IsFalse(world.Destroy(entity));
                Assert.Throws<GameException>(() => entity.AddComponent<Dead>());
            }
        }

        [Test]
        public void Rollback_WhenComponentSetChanges_RebuildsSystemMatches()
        {
            using (var world = new CombatWorld())
            {
                var system = world.LoadSystem<HealthSystem>();
                var entity = world.Create();
                entity.AddComponent(new Health());
                world.SaveFrame();

                entity.AddComponent<Dead>();
                world.Rollback(0);

                Assert.IsFalse(entity.HasComponent<Dead>());
                Assert.IsTrue(entity.HasComponent<Health>());
                Assert.AreEqual(2, system.Created.Count);
                Assert.AreEqual(1, system.Destroyed.Count);
            }
        }

        [Test]
        public void SystemManager_WhenEntityStopsMatchingBeforeStep_DoesNotUpdateEntity()
        {
            using (var world = new CombatWorld())
            {
                var system = world.LoadSystem<HealthSystem>();
                var entity = world.Create();
                entity.AddComponent(new Health());

                entity.RemoveComponent<Health>();
                world.Step();

                Assert.AreEqual(1, system.Created.Count);
                Assert.AreEqual(1, system.Destroyed.Count);
                Assert.AreEqual(0, system.Updated.Count);
            }
        }

        [Test]
        public void World_WhenDisposed_RejectsFurtherUse()
        {
            var world = new CombatWorld();
            var entity = world.Create();

            world.Dispose();

            Assert.IsFalse(entity.IsAlive);
            Assert.Throws<GameException>(() => world.Create());
            Assert.Throws<GameException>(() => world.Update(0f));
            Assert.Throws<GameException>(() => entity.AddComponent<Dead>());
            Assert.DoesNotThrow(() => world.Dispose());
        }

        [Test]
        public void SystemManager_WhenSystemsChangeDuringCallbacks_DoesNotThrow()
        {
            using (var world = new CombatWorld())
            {
                world.Create();
                var loader = new LoadingSystem();
                var selfUnloading = new SelfUnloadingSystem();
                world.LoadSystem(loader);
                world.LoadSystem(selfUnloading);

                Assert.DoesNotThrow(() => world.Step());

                Assert.IsNotNull(loader.LoadedSystem);
                Assert.AreEqual(1, loader.LoadedSystem.Created.Count);
                Assert.AreEqual(1, selfUnloading.Destroyed.Count);
                Assert.DoesNotThrow(() => world.Step());
            }
        }

        [Test]
        public void GetComponent_WhenComponentIsMissing_ThrowsGameException()
        {
            using (var world = new CombatWorld())
            {
                var entity = world.Create();

                Assert.Throws<GameException>(() => entity.GetComponent<Health>());
            }
        }

        [Test]
        public void GuardClauses_WhenInputsAreInvalid_ThrowExpectedExceptions()
        {
            using (var world = new CombatWorld())
            using (var foreignWorld = new CombatWorld())
            {
                var entity = world.Create();
                var foreignEntity = foreignWorld.Create();

                Assert.Throws<ArgumentOutOfRangeException>(() => new CombatWorld(0));
                Assert.Throws<ArgumentOutOfRangeException>(() => world.FrameRate = 0);
                Assert.Throws<ArgumentOutOfRangeException>(() => world.Update(-0.01f));
                Assert.Throws<ArgumentNullException>(() => world.LoadSystem(null));
                Assert.Throws<ArgumentNullException>(() => entity.AddComponent<Health>(null));
                Assert.Throws<GameException>(() => world.LoadSystem(new ConflictingSystem()));
                Assert.Throws<GameException>(() => world.HasComponent<Health>(foreignEntity));
                Assert.Throws<ArgumentException>(() => new Queryable(typeof(string)));
            }
        }

        private sealed class Health : ComponentBase
        {
            public int Value;
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

        private sealed class Dead : ComponentBase
        {
        }

        private class RecordingSystem : SystemBase
        {
            public List<Entity> Created { get; } = new List<Entity>();

            public List<Entity> Destroyed { get; } = new List<Entity>();

            public List<Entity> Updated { get; } = new List<Entity>();

            public override void OnCreate(Entity entity)
            {
                Created.Add(entity);
            }

            public override void OnDestroy(Entity entity)
            {
                Destroyed.Add(entity);
            }

            public override void OnUpdate(Entity entity)
            {
                Updated.Add(entity);
            }
        }

        private sealed class AnySystem : RecordingSystem
        {
        }

        private sealed class HealthSystem : RecordingSystem
        {
            public override Queryable Query { get; } = new Queryable(new[] { typeof(Health) }, new[] { typeof(Dead) });
        }

        private sealed class LoadingSystem : RecordingSystem
        {
            private CombatWorld m_World;

            public RecordingSystem LoadedSystem { get; private set; }

            public override void Initialize(CombatWorld world)
            {
                m_World = world;
            }

            public override void OnUpdate(Entity entity)
            {
                base.OnUpdate(entity);
                if (LoadedSystem != null)
                {
                    return;
                }

                LoadedSystem = new RecordingSystem();
                m_World.LoadSystem(LoadedSystem);
            }
        }

        private sealed class SelfUnloadingSystem : RecordingSystem
        {
            private CombatWorld m_World;

            public override void Initialize(CombatWorld world)
            {
                m_World = world;
            }

            public override void OnUpdate(Entity entity)
            {
                base.OnUpdate(entity);
                m_World.UnloadSystem(this);
            }
        }

        private sealed class ConflictingSystem : SystemBase
        {
            public override Queryable Query { get; } = new Queryable(new[] { typeof(Health) }, new[] { typeof(Health) });
        }
    }
}
