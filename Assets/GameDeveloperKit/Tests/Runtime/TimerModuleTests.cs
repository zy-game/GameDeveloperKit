using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Timer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

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
                    await App.Unregister<TimerModule>();
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
                await App.Register<TimerModule>();

                Assert.IsNotNull(App.TryGetValue<TimerModule>(out var module));
                Assert.IsNotNull(module);
                Assert.AreSame(module, App.Timer);
            });
        }

        [Test]
        public void Shutdown_WhenFrameworkIsNotStarted_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => App.Shutdown().GetAwaiter().GetResult());
        }

        [UnityTest]
        public IEnumerator Shutdown_WhenTimerModuleIsRegistered_ClosesRegisteredModule()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await App.Register<TimerModule>();

                Assert.IsNotNull(GameObject.Find("Timer"));

                await App.Shutdown();

                Assert.IsNull(GameObject.Find("Timer"));
                Assert.Throws<GameException>(() =>
                {
                    var _ = App.Timer;
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
            Assert.Greater(module.DeltaTime, 0f);
            Assert.Greater(module.UnscaledDeltaTime, 0f);
            Assert.Less(module.Time, 1d);

            module.Shutdown().GetAwaiter().GetResult();
            Assert.DoesNotThrow(() => module.Shutdown().GetAwaiter().GetResult());
            yield return null;
            Assert.IsTrue(GameObject.Find("Timer") == null);
        }

        [Test]
        public void TimerCallbacks_WhenRegisteredAndCleared_DoNotThrow()
        {
            var module = new TimerModule();
            var handle = new TestTimerHandle();

            Assert.DoesNotThrow(() => module.SetTimer(handle, 1f));
            Assert.DoesNotThrow(() => module.ClearTimer(handle));
            Assert.DoesNotThrow(() => module.SetTimer(_ => { }, 1f));
            Assert.DoesNotThrow(() => module.ClearTimer(_ => { }));
        }

        [UnityTest]
        public IEnumerator Delay_WhenElapsed_ExecutesOnceAndCompletes()
        {
            var module = new TimerModule();
            module.Startup().GetAwaiter().GetResult();
            var count = 0;

            var handle = module.Delay(0.02f, () => count++, owner: this, tag: "delay");
            var before = module.Snapshot();

            Assert.AreEqual(1, before.Delays.Count);
            Assert.AreSame(this, before.Delays[0].Owner);
            Assert.AreEqual("delay", before.Delays[0].Tag);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, count);
            Assert.IsTrue(handle.IsCompleted);
            Assert.AreEqual(0, module.Snapshot().Delays.Count);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [UnityTest]
        public IEnumerator Delay_WhenDelayIsZero_ExecutesOnNextTick()
        {
            var module = new TimerModule();
            module.Startup().GetAwaiter().GetResult();
            var count = 0;

            module.Delay(0f, () => count++);

            Assert.AreEqual(0, count);
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, count);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [UnityTest]
        public IEnumerator Countdown_WhenElapsed_UpdatesRemainingAndCompletes()
        {
            var module = new TimerModule();
            module.Startup().GetAwaiter().GetResult();
            var tickCount = 0;
            var completed = false;

            var handle = module.Countdown(0.04f, _ => tickCount++, () => completed = true);

            yield return new WaitForFixedUpdate();

            Assert.GreaterOrEqual(tickCount, 1);
            Assert.IsFalse(completed);
            Assert.Greater(handle.Remaining, 0f);
            Assert.Greater(handle.Progress, 0f);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.IsTrue(completed);
            Assert.IsTrue(handle.IsCompleted);
            Assert.AreEqual(0f, handle.Remaining, 0.0001f);
            Assert.AreEqual(1f, handle.Progress, 0.0001f);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [UnityTest]
        public IEnumerator Interval_WhenCancelled_StopsExecuting()
        {
            var module = new TimerModule();
            module.Startup().GetAwaiter().GetResult();
            var count = 0;

            var handle = module.Interval(0.02f, _ => count++);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.GreaterOrEqual(count, 1);
            Assert.IsTrue(module.Cancel(handle));
            var countAfterCancel = count;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(countAfterCancel, count);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [UnityTest]
        public IEnumerator CancelOwner_WhenOwnerMatches_CancelsOnlyMatchingTimers()
        {
            var module = new TimerModule();
            module.Startup().GetAwaiter().GetResult();
            var ownerA = new object();
            var ownerB = new object();
            var countA = 0;
            var countB = 0;

            module.Delay(0.02f, () => countA++, owner: ownerA);
            module.Delay(0.02f, () => countB++, owner: ownerB);

            Assert.AreEqual(1, module.CancelOwner(ownerA));

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(0, countA);
            Assert.AreEqual(1, countB);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [UnityTest]
        public IEnumerator TimerCallback_WhenCancellingSelfOrOther_RemainsStable()
        {
            var module = new TimerModule();
            module.Startup().GetAwaiter().GetResult();
            TimerDelayHandle selfHandle = null;
            TimerDelayHandle otherHandle = null;
            var selfCount = 0;
            var otherCount = 0;

            selfHandle = module.Delay(0.02f, () =>
            {
                selfCount++;
                module.Cancel(selfHandle);
                module.Cancel(otherHandle);
            });
            otherHandle = module.Delay(0.02f, () => otherCount++);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, selfCount);
            Assert.AreEqual(0, otherCount);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [UnityTest]
        public IEnumerator SetTimer_WhenUsingLegacyCallback_MapsToDelayAndInterval()
        {
            var module = new TimerModule();
            module.Startup().GetAwaiter().GetResult();
            var count = 0;
            Action<float> callback = _ => count++;

            module.SetTimer(callback, 0.02f);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, count);

            module.SetTimer(callback, 0.02f, true);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.GreaterOrEqual(count, 2);
            module.ClearTimer(callback);
            var countAfterClear = count;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(countAfterClear, count);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [UnityTest]
        public IEnumerator TimerHandle_WhenPaused_DoesNotAdvanceUntilResumed()
        {
            var module = new TimerModule();
            module.Startup().GetAwaiter().GetResult();
            var count = 0;

            var handle = module.Delay(0.02f, () => count++);
            handle.Pause();

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(0, count);
            Assert.IsTrue(handle.IsPaused);

            handle.Resume();

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, count);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [Test]
        public void Snapshot_WhenTimersRegistered_ContainsTimerState()
        {
            var module = new TimerModule();
            module.Startup().GetAwaiter().GetResult();
            var owner = new object();

            module.Delay(0.5f, () => { }, owner: owner, tag: "delay");
            module.Countdown(1f, owner: owner, tag: "countdown");
            module.Interval(0.25f, _ => { }, owner: owner, tag: "interval");

            var snapshot = module.Snapshot();

            Assert.AreEqual(module.Tick, snapshot.Tick);
            Assert.AreEqual(module.Time, snapshot.Time);
            Assert.AreEqual(1, snapshot.Delays.Count);
            Assert.AreEqual(1, snapshot.Countdowns.Count);
            Assert.AreEqual(1, snapshot.Intervals.Count);
            Assert.AreSame(owner, snapshot.Delays[0].Owner);
            Assert.AreEqual("delay", snapshot.Delays[0].Tag);
            Assert.AreEqual("countdown", snapshot.Countdowns[0].Tag);
            Assert.AreEqual("interval", snapshot.Intervals[0].Tag);
            Assert.Greater(snapshot.Delays[0].NextFireTime, snapshot.Time);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [Test]
        public void TimerArguments_WhenInvalid_Throw()
        {
            var module = new TimerModule();
            var handle = new TestTimerHandle();

            Assert.Throws<ArgumentNullException>(() => module.Delay(0f, null));
            Assert.Throws<ArgumentNullException>(() => module.Interval(0f, null));
            Assert.Throws<ArgumentNullException>(() => module.Cancel(null));
            Assert.Throws<ArgumentNullException>(() => module.SetTimer((TimerHandle)null, 0f));
            Assert.Throws<ArgumentNullException>(() => module.ClearTimer((TimerHandle)null));
            Assert.Throws<ArgumentNullException>(() => module.SetTimer((Action<float>)null, 0f));
            Assert.Throws<ArgumentNullException>(() => module.ClearTimer((Action<float>)null));
            Assert.Throws<ArgumentException>(() => module.Delay(-0.01f, () => { }));
            Assert.Throws<ArgumentException>(() => module.Countdown(-0.01f));
            Assert.Throws<ArgumentException>(() => module.Interval(-0.01f, _ => { }));
            Assert.Throws<ArgumentException>(() => module.SetTimer(handle, -0.01f));
        }

        private sealed class TestTimerHandle : TimerHandle
        {
            public int Executions { get; private set; }

            public override void Execute(float deltaTime)
            {
                Executions++;
            }
        }
    }
}
