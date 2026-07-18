using System;
using System.Collections.Generic;
using GameDeveloperKit.Event;
using GameDeveloperKit.Timer;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    public sealed class EventModuleTests : RuntimeTestBase
    {
        [TearDown]
        public void TearDown()
        {
            try
            {
                App.Unregister<EventModule>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }

            try
            {
                App.Unregister<TimerModule>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }
        }

        [Test]
        public void Register_WhenEventModuleIsRegistered_ReturnsEvent()
        {
            App.Register<EventModule>();

            Assert.IsNotNull(App.Event);
        }

        [Test]
        public void FireNow_WhenDelegateRegistered_ReceivesFiredEvent()
        {
            var module = new EventModule();
            var received = 0;
            object sender = this;

            module.Subscribe<TestEvent>(evt =>
            {
                received = evt.Value;
                Assert.AreSame(sender, evt.Sender);
            });

            module.FireNow(new TestEvent { Value = 7, Sender = sender }, sender);

            Assert.AreEqual(7, received);
        }

        [Test]
        public void Fire_WhenQueued_DispatchesOnTimerUpdate()
        {
            App.Register<TimerModule>();
            App.Register<EventModule>();
            var count = 0;
            App.Event.Subscribe<TestEvent>(_ => count++);

            App.Event.Fire(new TestEvent());

            Assert.AreEqual(0, count);

            App.Timer.Update(0f, 0f);

            Assert.AreEqual(1, count);
        }

        [Test]
        public void Fire_WhenTimerModuleIsMissing_Throws()
        {
            var module = new EventModule();

            Assert.Throws<GameException>(() => module.Fire(new TestEvent()));
        }

        [Test]
        public void Fire_WhenListenerQueuesEvent_DispatchesNestedEventOnNextTimerUpdate()
        {
            App.Register<TimerModule>();
            App.Register<EventModule>();
            var count = 0;
            App.Event.Subscribe<TestEvent>(_ =>
            {
                count++;
                if (count == 1)
                {
                    App.Event.Fire(new TestEvent());
                }
            });

            App.Event.Fire(new TestEvent());
            App.Timer.Update(0f, 0f);

            Assert.AreEqual(1, count);

            App.Timer.Update(0f, 0f);

            Assert.AreEqual(2, count);
        }

        [Test]
        public void Subscription_WhenCanceled_StopsReceivingEvents()
        {
            var module = new EventModule();
            var count = 0;
            var subscription = module.Subscribe<TestEvent>(_ => count++);

            subscription.Cancel();
            module.FireNow(new TestEvent());

            Assert.IsFalse(subscription.IsActive);
            Assert.AreEqual(0, count);
        }

        [Test]
        public void FireNow_WhenEventIsUsed_StopsDispatchingRemainingListeners()
        {
            var module = new EventModule();
            var secondCalled = false;
            module.Subscribe<TestEvent>(evt => evt.Use());
            module.Subscribe<TestEvent>(_ => secondCalled = true);

            module.FireNow(new TestEvent());

            Assert.IsFalse(secondCalled);
        }

        [Test]
        public void FireNow_WhenListenerFiresSameType_OuterAndNestedSnapshotsCompleteIndependently()
        {
            var module = new EventModule();
            var calls = new List<string>();
            module.Subscribe<TestEvent>(evt =>
            {
                calls.Add($"first:{evt.Value}");
                if (evt.Value == 1)
                {
                    module.FireNow(new TestEvent { Value = 2 });
                }
            });
            module.Subscribe<TestEvent>(evt => calls.Add($"second:{evt.Value}"));

            module.FireNow(new TestEvent { Value = 1 });

            CollectionAssert.AreEqual(
                new[] { "first:1", "first:2", "second:2", "second:1" },
                calls);
        }

        [Test]
        public void FireNow_WhenListenerFiresDifferentType_OuterSnapshotStillCompletes()
        {
            var module = new EventModule();
            var calls = new List<string>();
            module.Subscribe<TestEvent>(_ =>
            {
                calls.Add("outer:first");
                module.FireNow(new SecondaryTestEvent());
            });
            module.Subscribe<TestEvent>(_ => calls.Add("outer:second"));
            module.Subscribe<SecondaryTestEvent>(_ => calls.Add("nested"));

            module.FireNow(new TestEvent());

            CollectionAssert.AreEqual(new[] { "outer:first", "nested", "outer:second" }, calls);
        }

        [Test]
        public void FireNow_WhenNestedDispatchCancelsOuterListener_SkipsCanceledListener()
        {
            var module = new EventModule();
            var canceledListenerCalls = 0;
            Subscription canceledSubscription = null;
            module.Subscribe<TestEvent>(_ => module.FireNow(new SecondaryTestEvent()));
            canceledSubscription = module.Subscribe<TestEvent>(_ => canceledListenerCalls++);
            module.Subscribe<SecondaryTestEvent>(_ => canceledSubscription.Cancel());

            module.FireNow(new TestEvent());

            Assert.AreEqual(0, canceledListenerCalls);
            Assert.IsFalse(canceledSubscription.IsActive);
        }

        [Test]
        public void FireNow_WhenListenerSubscribesDuringDispatch_ExcludesItFromCurrentSnapshot()
        {
            var module = new EventModule();
            var calls = new List<string>();
            var added = false;
            module.Subscribe<TestEvent>(_ =>
            {
                calls.Add("first");
                if (!added)
                {
                    added = true;
                    module.Subscribe<TestEvent>(_ => calls.Add("added"));
                }
            });
            module.Subscribe<TestEvent>(_ => calls.Add("second"));

            module.FireNow(new TestEvent());
            CollectionAssert.AreEqual(new[] { "first", "second" }, calls);

            calls.Clear();
            module.FireNow(new TestEvent());
            CollectionAssert.AreEqual(new[] { "first", "second", "added" }, calls);
        }

        [Test]
        public void FireNow_WhenDispatchIsHot_DoesNotAllocateListenerSnapshots()
        {
            var module = new EventModule();
            var calls = 0;
            var eventData = new TestEvent();
            module.Subscribe<TestEvent>(_ => calls++);
            module.Subscribe<TestEvent>(_ => calls++);
            module.FireNow(eventData);

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 1000; index++)
            {
                module.FireNow(eventData);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated);
            Assert.AreEqual(2002, calls);
        }

        [Test]
        public void FireNow_WhenObjectHandleRegistered_ReceivesEvent()
        {
            var module = new EventModule();
            var handle = new TestEventHandle();

            module.Subscribe(handle);
            module.FireNow(new TestEvent { Value = 11 });

            Assert.AreEqual(11, handle.LastValue);
        }

        [Test]
        public void Subscribe_WhenArgumentIsNull_Throws()
        {
            var module = new EventModule();

            Assert.Throws<ArgumentNullException>(() => module.Subscribe<TestEvent>(null));
            Assert.Throws<ArgumentNullException>(() => module.Subscribe<TestEventHandle>(null));
        }

        private sealed class TestEvent : ArgsBase
        {
            public int Value;
            public object Sender;
        }

        private sealed class SecondaryTestEvent : ArgsBase
        {
        }

        private sealed class TestEventHandle : EventHandleBase, IEventHandleBase<TestEvent>
        {
            public int LastValue { get; private set; }

            public override void Handle(object sender, object args)
            {
                Handle(sender, (TestEvent)args);
            }

            public void Handle(object sender, TestEvent eventData)
            {
                LastValue = eventData.Value;
            }
        }
    }
}
