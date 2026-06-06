using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Combat;
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
                Assert.IsNotNull(GameObject.Find(CombatModule.RootName));

                await App.Unregister<CombatModule>();
                Assert.Throws<GameException>(() =>
                {
                    var _ = App.Combat;
                });
            });
        }

        [UnityTest]
        public IEnumerator Shutdown_WhenCalledRepeatedly_IsSafe()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var module = new CombatModule();
                await module.Startup();

                Assert.IsNotNull(module.World);
                Assert.IsNotNull(GameObject.Find(CombatModule.RootName));

                await module.Shutdown();
                await UniTask.Yield();
                await module.Shutdown();

                Assert.IsNull(module.World);
                Assert.IsNull(GameObject.Find(CombatModule.RootName));
            });
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
            }
        }

        private sealed class Health : ComponentBase
        {
            public int Value;
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

        private sealed class ConflictingSystem : SystemBase
        {
            public override Queryable Query { get; } = new Queryable(new[] { typeof(Health) }, new[] { typeof(Health) });
        }
    }
}
