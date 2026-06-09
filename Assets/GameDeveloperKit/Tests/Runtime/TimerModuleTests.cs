using System;
using System.Collections;
using System.Collections.Generic;
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

                while (true)
                {
                    var timerObject = GameObject.Find("Timer");
                    if (timerObject == null)
                    {
                        break;
                    }

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
        public IEnumerator Startup_CreatesTimerObjectAndTicks()
        {
            var module = new TimerModule();

            module.Startup().GetAwaiter().GetResult();
            var timerObject = GameObject.Find("Timer");

            Assert.IsNotNull(timerObject);
            Assert.AreEqual(0, module.Tick);

            yield return WaitForTicks(module, 1);

            Assert.GreaterOrEqual(module.Tick, 1);
            Assert.Greater(module.Time, 0f);
            Assert.Greater(module.UnscaledTime, 0f);
            Assert.Greater(module.DeltaTime, 0f);
            Assert.Greater(module.UnscaledDeltaTime, 0f);
            Assert.Less(module.Time, 1d);

            module.Shutdown().GetAwaiter().GetResult();
            Assert.DoesNotThrow(() => module.Shutdown().GetAwaiter().GetResult());
            yield return null;
            Assert.IsTrue(GameObject.Find("Timer") == null);
        }

        [UnityTest]
        public IEnumerator Startup_WhenCalledTwice_DoesNotCreateDuplicateTimerObject()
        {
            var module = new TimerModule();

            module.Startup().GetAwaiter().GetResult();
            module.Startup().GetAwaiter().GetResult();

            Assert.AreEqual(1, CountTimerObjects());

            yield return WaitForTicks(module, 1);

            Assert.AreEqual(1, module.Tick);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [Test]
        public void Startup_WhenDefaultFpsIsUsed_DoesNotOverwriteUnityFixedDeltaTime()
        {
            var originalFixedDeltaTime = Time.fixedDeltaTime;
            var module = new TimerModule();

            try
            {
                Time.fixedDeltaTime = 0.033f;

                module.Startup().GetAwaiter().GetResult();

                Assert.AreEqual(0.033f, Time.fixedDeltaTime, 0.0001f);

                module.Shutdown().GetAwaiter().GetResult();

                Assert.AreEqual(0.033f, Time.fixedDeltaTime, 0.0001f);
            }
            finally
            {
                module.Shutdown().GetAwaiter().GetResult();
                Time.fixedDeltaTime = originalFixedDeltaTime;
            }
        }

        [Test]
        public void TimerUpdate_WhenScaledDeltaIsZero_OnlyUnscaledTimersAdvance()
        {
            var module = new TimerModule();
            module.Startup().GetAwaiter().GetResult();
            var scaledCount = 0;
            var unscaledCount = 0;

            module.Delay(0.02f, () => scaledCount++);
            module.Delay(0.02f, () => unscaledCount++, true);

            module.Update(0f, 0.02f);

            Assert.AreEqual(0, scaledCount);
            Assert.AreEqual(1, unscaledCount);
            Assert.AreEqual(0f, module.DeltaTime, 0.0001f);
            Assert.AreEqual(0.02f, module.UnscaledDeltaTime, 0.0001f);
            Assert.AreEqual(0d, module.Time, 0.0001d);
            Assert.AreEqual(0.02d, module.UnscaledTime, 0.0001d);

            module.Update(0.02f, 0.02f);

            Assert.AreEqual(1, scaledCount);

            module.Shutdown().GetAwaiter().GetResult();
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

        [Test]
        public void Delay_WhenElapsed_ExecutesOnceAndCompletes()
        {
            var module = new TimerModule();
            module.Startup().GetAwaiter().GetResult();
            var count = 0;

            var handle = module.Delay(0.02f, () => count++, owner: this, tag: "delay");
            var before = module.Snapshot();

            Assert.AreEqual(1, before.Delays.Count);
            Assert.AreSame(this, before.Delays[0].Owner);
            Assert.AreEqual("delay", before.Delays[0].Tag);

            module.Update(0.02f, 0.02f);

            Assert.AreEqual(1, count);
            Assert.IsTrue(handle.IsCompleted);
            Assert.AreEqual(0, module.Snapshot().Delays.Count);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [Test]
        public void Delay_WhenDelayIsZero_ExecutesOnNextTick()
        {
            var module = new TimerModule();
            module.Startup().GetAwaiter().GetResult();
            var count = 0;

            module.Delay(0f, () => count++);

            Assert.AreEqual(0, count);
            module.Update(0f, 0f);

            Assert.AreEqual(1, count);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [Test]
        public void Countdown_WhenElapsed_UpdatesRemainingAndCompletes()
        {
            var module = new TimerModule();
            module.Startup().GetAwaiter().GetResult();
            var tickCount = 0;
            var completed = false;

            var handle = module.Countdown(0.04f, _ => tickCount++, () => completed = true);

            module.Update(0.02f, 0.02f);

            Assert.GreaterOrEqual(tickCount, 1);
            Assert.IsFalse(completed);
            Assert.Greater(handle.Remaining, 0f);
            Assert.Greater(handle.Progress, 0f);

            module.Update(0.02f, 0.02f);

            Assert.IsTrue(completed);
            Assert.IsTrue(handle.IsCompleted);
            Assert.AreEqual(0f, handle.Remaining, 0.0001f);
            Assert.AreEqual(1f, handle.Progress, 0.0001f);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [Test]
        public void Interval_WhenCancelled_StopsExecuting()
        {
            var module = new TimerModule();
            module.Startup().GetAwaiter().GetResult();
            var count = 0;

            var handle = module.Interval(0.02f, _ => count++);

            module.Update(0.02f, 0.02f);
            module.Update(0.02f, 0.02f);

            Assert.GreaterOrEqual(count, 1);
            Assert.IsTrue(module.Cancel(handle));
            var countAfterCancel = count;

            module.Update(0.02f, 0.02f);
            module.Update(0.02f, 0.02f);

            Assert.AreEqual(countAfterCancel, count);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [Test]
        public void Interval_WhenDeltaSpansMultipleIntervals_CatchesUpAndKeepsRemainder()
        {
            var module = new TimerModule();
            module.Startup().GetAwaiter().GetResult();
            var count = 0;
            var accumulatedDelta = 0f;

            var handle = module.Interval(0.1f, deltaTime =>
            {
                count++;
                accumulatedDelta += deltaTime;
            });

            module.Update(0.35f, 0.35f);

            Assert.AreEqual(3, count);
            Assert.AreEqual(0.3f, accumulatedDelta, 0.0001f);
            Assert.AreEqual(0.05f, handle.Elapsed, 0.0001f);
            Assert.AreEqual(0.05f, handle.Remaining, 0.0001f);
            Assert.AreEqual(0.5f, handle.Progress, 0.0001f);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [Test]
        public void CancelOwner_WhenOwnerMatches_CancelsOnlyMatchingTimers()
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

            module.Update(0.02f, 0.02f);

            Assert.AreEqual(0, countA);
            Assert.AreEqual(1, countB);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [Test]
        public void TimerCallback_WhenCancellingSelfOrOther_RemainsStable()
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

            module.Update(0.02f, 0.02f);

            Assert.AreEqual(1, selfCount);
            Assert.AreEqual(0, otherCount);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [Test]
        public void SetTimer_WhenUsingLegacyCallback_MapsToDelayAndInterval()
        {
            var module = new TimerModule();
            module.Startup().GetAwaiter().GetResult();
            var count = 0;
            Action<float> callback = _ => count++;

            module.SetTimer(callback, 0.02f);

            module.Update(0.02f, 0.02f);

            Assert.AreEqual(1, count);

            module.SetTimer(callback, 0.02f, true);

            module.Update(0.02f, 0.02f);

            Assert.GreaterOrEqual(count, 2);
            module.ClearTimer(callback);
            var countAfterClear = count;

            module.Update(0.02f, 0.02f);

            Assert.AreEqual(countAfterClear, count);

            module.Shutdown().GetAwaiter().GetResult();
        }

        [Test]
        public void TimerHandle_WhenPaused_DoesNotAdvanceUntilResumed()
        {
            var module = new TimerModule();
            module.Startup().GetAwaiter().GetResult();
            var count = 0;

            var handle = module.Delay(0.02f, () => count++);
            handle.Pause();

            module.Update(0.02f, 0.02f);
            module.Update(0.02f, 0.02f);

            Assert.AreEqual(0, count);
            Assert.IsTrue(handle.IsPaused);

            handle.Resume();

            module.Update(0.02f, 0.02f);

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
            Assert.AreEqual(module.UnscaledTime, snapshot.UnscaledTime);
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
        public void UpdateHandles_WhenTickKindMatches_InvokeOnlyMatchingHandle()
        {
            var module = new TimerModule();
            var updateCount = 0;
            var lateCount = 0;
            var fixedCount = 0;
            var updateContext = default(TimerUpdateContext);
            var lateContext = default(TimerUpdateContext);
            var fixedContext = default(TimerUpdateContext);

            module.OnUpdate(context =>
            {
                updateCount++;
                updateContext = context;
            });
            module.OnLateUpdate(context =>
            {
                lateCount++;
                lateContext = context;
            });
            module.OnFixedUpdate(context =>
            {
                fixedCount++;
                fixedContext = context;
            });

            module.Update(TimerTickKind.LateUpdate, 0.01f, 0.02f);

            Assert.AreEqual(0, updateCount);
            Assert.AreEqual(1, lateCount);
            Assert.AreEqual(0, fixedCount);
            Assert.AreEqual(TimerTickKind.LateUpdate, lateContext.TickKind);
            Assert.AreEqual(0, module.Tick);
            Assert.AreEqual(0, lateContext.Tick);
            Assert.AreEqual(0d, lateContext.Time, 0.0001d);
            Assert.AreEqual(0d, lateContext.UnscaledTime, 0.0001d);

            module.Update(TimerTickKind.FixedUpdate, 0.02f, 0.02f);

            Assert.AreEqual(0, updateCount);
            Assert.AreEqual(1, lateCount);
            Assert.AreEqual(1, fixedCount);
            Assert.AreEqual(TimerTickKind.FixedUpdate, fixedContext.TickKind);
            Assert.AreEqual(0, fixedContext.Tick);

            module.Update(TimerTickKind.Update, 0.03f, 0.04f);

            Assert.AreEqual(1, updateCount);
            Assert.AreEqual(1, lateCount);
            Assert.AreEqual(1, fixedCount);
            Assert.AreEqual(TimerTickKind.Update, updateContext.TickKind);
            Assert.AreEqual(1, module.Tick);
            Assert.AreEqual(1, updateContext.Tick);
            Assert.AreEqual(0.03d, updateContext.Time, 0.0001d);
            Assert.AreEqual(0.04d, updateContext.UnscaledTime, 0.0001d);
            Assert.AreEqual(0.03f, module.DeltaTime, 0.0001f);
            Assert.AreEqual(0.04f, module.UnscaledDeltaTime, 0.0001f);
        }

        [Test]
        public void UpdateHandles_WhenRegistered_InvokeInRegistrationOrder()
        {
            var module = new TimerModule();
            var order = new List<string>();

            module.OnUpdate(() => order.Add("first"));
            module.OnUpdate(() => order.Add("second"));
            module.OnUpdate(() => order.Add("third"));

            module.Update(TimerTickKind.Update, 0.01f, 0.01f);

            CollectionAssert.AreEqual(new[] { "first", "second", "third" }, order);
        }

        [Test]
        public void UpdateHandle_WhenThrows_DoesNotBlockOthersAndSnapshotStoresException()
        {
            var module = new TimerModule();
            var nextCount = 0;
            var throwing = new UpdateTimerHandle(() => throw new InvalidOperationException("throwing"));
            var next = new UpdateTimerHandle(() => nextCount++);

            module.Register(throwing, tag: "throwing");
            module.Register(next, tag: "next");

            module.Update(TimerTickKind.Update, 0.01f, 0.01f);

            Assert.AreEqual(1, nextCount);
            var snapshot = FindUpdateHandle(module, "throwing");
            Assert.IsTrue(snapshot.HasError);
            Assert.IsInstanceOf<InvalidOperationException>(snapshot.LastException);
            Assert.AreEqual(1, snapshot.LastTick);
        }

        [Test]
        public void Register_WhenSameUpdateHandleRegisteredTwice_InvokesOnce()
        {
            var module = new TimerModule();
            var count = 0;
            var handle = new UpdateTimerHandle(() => count++);

            var first = module.Register(handle);
            var second = module.Register(handle);

            module.Update(TimerTickKind.Update, 0.01f, 0.01f);

            Assert.AreSame(first, second);
            Assert.AreEqual(1, count);
            Assert.AreEqual(1, module.Snapshot().Updates.Count);
        }

        [Test]
        public void Unregister_WhenUpdateHandleRegistered_RemovesHandle()
        {
            var module = new TimerModule();
            var count = 0;
            var handle = new UpdateTimerHandle(() => count++);

            module.Register(handle);

            Assert.IsTrue(module.Unregister(handle));
            Assert.IsFalse(module.Unregister(handle));
            module.Update(TimerTickKind.Update, 0.01f, 0.01f);

            Assert.AreEqual(0, count);
            Assert.AreEqual(0, module.Snapshot().Updates.Count);
        }

        [Test]
        public void FixedUpdateHandle_WhenRegistered_AdvancesOnlyOnFixedUpdate()
        {
            var module = new TimerModule();
            var count = 0;

            var handle = module.OnFixedUpdate(() => count++);

            module.Update(TimerTickKind.Update, 1f, 1f);
            module.Update(TimerTickKind.LateUpdate, 1f, 1f);

            Assert.AreEqual(TimerTickKind.FixedUpdate, handle.TickKind);
            Assert.AreEqual(0, count);

            module.Update(TimerTickKind.FixedUpdate, 0.02f, 0.02f);

            Assert.AreEqual(1, count);
            Assert.AreEqual(1, module.Snapshot().Updates.Count);
        }

        [Test]
        public void FixedUpdateHandle_WhenTriggered_UsesGlobalClockContext()
        {
            var module = new TimerModule();
            var context = default(TimerUpdateContext);
            var count = 0;

            module.Update(TimerTickKind.Update, 0.03f, 0.04f);
            module.OnFixedUpdate(value =>
            {
                count++;
                context = value;
            });

            module.Update(TimerTickKind.FixedUpdate, 0.02f, 0.02f);

            Assert.AreEqual(1, count);
            Assert.AreEqual(1, module.Tick);
            Assert.AreEqual(1, context.Tick);
            Assert.AreEqual(0.03d, context.Time, 0.0001d);
            Assert.AreEqual(0.04d, context.UnscaledTime, 0.0001d);
            Assert.AreEqual(0.03f, context.DeltaTime, 0.0001f);
            Assert.AreEqual(0.04f, context.UnscaledDeltaTime, 0.0001f);
        }

        [Test]
        public void LateAndFixedUpdateHandles_WhenFpsIsSet_InvokeOnlyAfterInterval()
        {
            var module = new TimerModule();
            var lateCount = 0;
            var fixedCount = 0;

            module.OnLateUpdate(() => lateCount++, 10f);
            module.OnFixedUpdate(() => fixedCount++, 10f);

            module.Update(TimerTickKind.LateUpdate, 0.1f, 0.04f);
            module.Update(TimerTickKind.FixedUpdate, 0.1f, 0.04f);

            Assert.AreEqual(0, lateCount);
            Assert.AreEqual(0, fixedCount);

            module.Update(TimerTickKind.LateUpdate, 0.1f, 0.06f);
            module.Update(TimerTickKind.FixedUpdate, 0.1f, 0.06f);

            Assert.AreEqual(1, lateCount);
            Assert.AreEqual(1, fixedCount);
        }

        [Test]
        public void Shutdown_WhenUpdateHandlesRegistered_ClearsHandles()
        {
            var module = new TimerModule();
            var count = 0;
            var handle = new UpdateTimerHandle(() => count++);

            module.Register(handle);

            module.Shutdown().GetAwaiter().GetResult();
            module.Update(TimerTickKind.Update, 0.01f, 0.01f);

            Assert.IsTrue(handle.IsCancelled);
            Assert.AreEqual(0, count);
            Assert.AreEqual(0, module.Snapshot().Updates.Count);
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
            Assert.Throws<ArgumentNullException>(() => module.Register((TimerHandle)null));
            Assert.Throws<ArgumentNullException>(() => new UpdateTimerHandle((Action)null));
            Assert.Throws<ArgumentNullException>(() => module.OnUpdate((Action)null));
            Assert.Throws<ArgumentException>(() => module.OnLateUpdate(() => { }, 0f));
            Assert.Throws<ArgumentException>(() => module.OnFixedUpdate(() => { }, -1f));
            Assert.Throws<ArgumentException>(() => new LateUpdateTimerHandle(() => { }, 0f));
            Assert.Throws<ArgumentException>(() => new FixedUpdateTimerHandle(() => { }, -1f));
            Assert.Throws<ArgumentException>(() => module.Register(new TestTimerHandle((TimerTickKind)999)));
        }

        private sealed class TestTimerHandle : TimerHandle
        {
            public TestTimerHandle(TimerTickKind tickKind = TimerTickKind.Update)
            {
                TickKind = tickKind;
            }

            public int Executions { get; private set; }

            internal override TimerTickKind TickKind { get; }

            internal override void Advance(in TimerUpdateContext context, float phaseUnscaledDeltaTime)
            {
                Executions++;
            }
        }

        private static TimerUpdateHandle FindUpdateHandle(TimerModule module, string tag)
        {
            foreach (var handle in module.Snapshot().Updates)
            {
                if (handle.Tag == tag)
                {
                    return handle;
                }
            }

            throw new AssertionException("Update handle was not found.");
        }

        private static int CountTimerObjects()
        {
            var count = 0;
            var gameObjects = Object.FindObjectsOfType<GameObject>();
            for (var i = 0; i < gameObjects.Length; i++)
            {
                if (gameObjects[i].name == "Timer")
                {
                    count++;
                }
            }

            return count;
        }

        private static IEnumerator WaitForTicks(TimerModule module, long ticks)
        {
            var target = module.Tick + ticks;
            var guard = 0;
            while (module.Tick < target && guard < 120)
            {
                guard++;
                yield return null;
            }

            Assert.GreaterOrEqual(module.Tick, target);
        }
    }
}
