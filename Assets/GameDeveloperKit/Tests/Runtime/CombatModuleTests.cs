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
                    await Super.Unregister<CombatModule>();
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
                await Super.Register<CombatModule>();

                Assert.IsNotNull(Super.Combat);
                Assert.IsNotNull(Super.Combat.World);
                Assert.IsNotNull(Super.Combat.World.EntityManager);
                Assert.IsNotNull(Super.Combat.World.SystemManager);
                Assert.IsNotNull(GameObject.Find(CombatModule.RootName));

                await Super.Unregister<CombatModule>();
                Assert.Throws<GameException>(() =>
                {
                    var _ = Super.Combat;
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
                var entity = world.EntityManager.Create();

                entity.Set(new Health { Value = 100 });

                Assert.IsTrue(entity.IsAlive);
                Assert.GreaterOrEqual(entity.Id, 0);
                Assert.Greater(entity.Version, 0U);
                Assert.IsTrue(entity.Has<Health>());
                Assert.AreEqual(100, entity.Get<Health>().Value);
                Assert.IsTrue(entity.Remove<Health>());
                Assert.IsFalse(entity.Has<Health>());
            }
        }

        [Test]
        public void SystemManager_WhenComponentsChange_TriggersLifecycleOnce()
        {
            using (var world = new CombatWorld())
            {
                var system = world.SystemManager.Add<HealthSystem>();
                var entity = world.EntityManager.Create();

                entity.Set(new Health());
                entity.Add<Dead>();
                entity.Remove<Dead>();
                entity.Remove<Health>();
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
                var entity = world.EntityManager.Create();
                entity.Set(new Health());

                var system = world.SystemManager.Add<HealthSystem>();

                Assert.AreEqual(1, system.Created.Count);
                Assert.AreSame(entity, system.Created[0]);
            }
        }

        [Test]
        public void WorldUpdate_WhenDeltaIsBelowFixedStep_DoesNotUpdateSystems()
        {
            using (var world = new CombatWorld(10))
            {
                var system = world.SystemManager.Add<AnySystem>();
                world.EntityManager.Create();

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
                var system = world.SystemManager.Add<AnySystem>();
                world.EntityManager.Create();

                world.Update(0.25f);

                Assert.AreEqual(2, world.Tick);
                Assert.AreEqual(0.2f, world.Time, 0.0001f);
                Assert.AreEqual(2, system.Updated.Count);
            }
        }

        [Test]
        public void Destroy_WhenEntityIsActive_TriggersDestroyAndMarksDead()
        {
            using (var world = new CombatWorld())
            {
                var system = world.SystemManager.Add<AnySystem>();
                var entity = world.EntityManager.Create();

                Assert.IsTrue(world.EntityManager.Destroy(entity));

                Assert.IsFalse(entity.IsAlive);
                Assert.AreEqual(1, system.Destroyed.Count);
                Assert.AreSame(entity, system.Destroyed[0]);
                Assert.IsFalse(world.EntityManager.Destroy(entity));
                Assert.Throws<GameException>(() => entity.Add<Dead>());
            }
        }

        [Test]
        public void Rollback_WhenComponentSetChanges_RebuildsSystemMatches()
        {
            using (var world = new CombatWorld())
            {
                var system = world.SystemManager.Add<HealthSystem>();
                var entity = world.EntityManager.Create();
                entity.Set(new Health());
                world.SaveFrame();

                entity.Add<Dead>();
                world.Rollback(0);

                Assert.IsFalse(entity.Has<Dead>());
                Assert.IsTrue(entity.Has<Health>());
                Assert.AreEqual(2, system.Created.Count);
                Assert.AreEqual(1, system.Destroyed.Count);
            }
        }

        [Test]
        public void GuardClauses_WhenInputsAreInvalid_ThrowExpectedExceptions()
        {
            using (var world = new CombatWorld())
            using (var foreignWorld = new CombatWorld())
            {
                var entity = world.EntityManager.Create();
                var foreignEntity = foreignWorld.EntityManager.Create();

                Assert.Throws<ArgumentOutOfRangeException>(() => new CombatWorld(0));
                Assert.Throws<ArgumentOutOfRangeException>(() => world.FrameRate = 0);
                Assert.Throws<ArgumentOutOfRangeException>(() => world.Update(-0.01f));
                Assert.Throws<ArgumentNullException>(() => world.SystemManager.Add(null));
                Assert.Throws<ArgumentNullException>(() => entity.Set<Health>(null));
                Assert.Throws<GameException>(() => world.SystemManager.Add(new ConflictingSystem()));
                Assert.Throws<GameException>(() => world.EntityManager.Has<Health>(foreignEntity));
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

            protected override void OnCreate(Entity entity)
            {
                Created.Add(entity);
            }

            protected override void OnDestroy(Entity entity)
            {
                Destroyed.Add(entity);
            }

            protected override void OnUpdate(Entity entity)
            {
                Updated.Add(entity);
            }
        }

        private sealed class AnySystem : RecordingSystem
        {
        }

        private sealed class HealthSystem : RecordingSystem
        {
            public override ComponentType[] Include { get; } =
            {
                ComponentType.Of<Health>(),
            };

            public override ComponentType[] Exclude { get; } =
            {
                ComponentType.Of<Dead>(),
            };
        }

        private sealed class ConflictingSystem : SystemBase
        {
            public override ComponentType[] Include { get; } =
            {
                ComponentType.Of<Health>(),
            };

            public override ComponentType[] Exclude { get; } =
            {
                ComponentType.Of<Health>(),
            };
        }
    }
}
