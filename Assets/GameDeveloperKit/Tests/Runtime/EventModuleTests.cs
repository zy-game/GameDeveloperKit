using System;
using GameDeveloperKit.Event;
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
        }

        [Test]
        public void Register_WhenEventModuleIsRegistered_ReturnsEvent()
        {
            App.Register<EventModule>().GetAwaiter().GetResult();

            Assert.IsNotNull(App.Event);
        }

        [Test]
        public void Subscribe_WhenDelegateRegistered_ReceivesFiredEvent()
        {
            var module = new EventModule();
            var received = 0;
            object sender = this;

            module.Subscribe<TestEvent>(evt =>
            {
                received = evt.Value;
                Assert.AreSame(sender, evt.Sender);
            });

            module.Fire(new TestEvent { Value = 7, Sender = sender }, sender);

            Assert.AreEqual(7, received);
        }

        [Test]
        public void Subscription_WhenCanceled_StopsReceivingEvents()
        {
            var module = new EventModule();
            var count = 0;
            var subscription = module.Subscribe<TestEvent>(_ => count++);

            subscription.Cancel();
            module.Fire(new TestEvent());

            Assert.IsFalse(subscription.IsActive);
            Assert.AreEqual(0, count);
        }

        [Test]
        public void Fire_WhenEventIsUsed_StopsDispatchingRemainingListeners()
        {
            var module = new EventModule();
            var secondCalled = false;
            module.Subscribe<TestEvent>(evt => evt.Use());
            module.Subscribe<TestEvent>(_ => secondCalled = true);

            module.Fire(new TestEvent());

            Assert.IsFalse(secondCalled);
        }

        [Test]
        public void Subscribe_WhenObjectHandleRegistered_ReceivesEvent()
        {
            var module = new EventModule();
            var handle = new TestEventHandle();

            module.Subscribe(handle);
            module.Fire(new TestEvent { Value = 11 });

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
