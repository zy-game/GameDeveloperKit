using System.Collections;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Timer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class TimerModuleTests : RuntimeTestBase
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            return UniTask.ToCoroutine(async () =>
            {
                try
                {
                    await Super.Unregister<TimerModule>();
                }
                catch (GameException)
                {
                }

                var timerObject = GameObject.Find("Timer");
                if (timerObject != null)
                {
                    Object.Destroy(timerObject);
                    await UniTask.Yield();
                }
            });
        }

        [UnityTest]
        public IEnumerator Register_WhenTimerModuleIsRegistered_ReturnsTimer()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await Super.Register<TimerModule>();

                Assert.IsNotNull(Super.TryGetValue<TimerModule>(out var module));
                Assert.IsNotNull(module);
                Assert.AreSame(module, Super.Timer);
            });
        }

        [Test]
        public void Shutdown_WhenFrameworkIsNotStarted_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => Super.Shutdown().GetAwaiter().GetResult());
        }

        [UnityTest]
        public IEnumerator Shutdown_WhenTimerModuleIsRegistered_ClosesRegisteredModule()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await Super.Register<TimerModule>();

                Assert.IsNotNull(GameObject.Find("Timer"));

                await Super.Shutdown();

                Assert.IsNull(GameObject.Find("Timer"));
                Assert.Throws<GameException>(() =>
                {
                    var _ = Super.Timer;
                });
            });
        }

        [UnityTest]
        public IEnumerator Startup_CreatesTimerObjectAndTicksOnFixedUpdate()
        {
            var module = new TimerModule();

            module.Startup().GetAwaiter().GetResult();
            var timerObject = GameObject.Find("Timer");

            Assert.IsNotNull(timerObject);
            Assert.AreEqual(0, module.Tick);

            yield return new WaitForFixedUpdate();

            Assert.GreaterOrEqual(module.Tick, 1);
            Assert.Greater(module.Time, 0f);

            module.Shutdown().GetAwaiter().GetResult();
            yield return null;
            Assert.IsTrue(GameObject.Find("Timer") == null);
        }

        [Test]
        public void TimerCallbacks_WhenNotImplemented_DoNotThrow()
        {
            var module = new TimerModule();
            var handle = new TestTimerHandle();

            Assert.DoesNotThrow(() => module.SetTimer(handle, 1f));
            Assert.DoesNotThrow(() => module.ClearTimer(handle));
            Assert.DoesNotThrow(() => module.SetTimer(_ => { }, 1f));
            Assert.DoesNotThrow(() => module.ClearTimer(_ => { }));
        }

        private sealed class TestTimerHandle : TimerHandle
        {
            public override void Execute(float deltaTime)
            {
            }
        }
    }
}
