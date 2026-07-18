using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Playable;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    public sealed class PlayableModuleTests : RuntimeTestBase
    {
        [TearDown]
        public void TearDown()
        {
            if (App.TryGetRegistered<PlayableModule>(out _))
            {
                App.Unregister<PlayableModule>().GetAwaiter().GetResult();
            }
        }

        [Test]
        public void Registry_WhenConcretePlayableRegistered_ReturnsSameInstanceAndDisposesOnShutdown()
        {
            var module = App.Playable;
            var playable = new TestPlayable();

            module.Register(playable);

            Assert.AreSame(playable, module.Get<TestPlayable>());
            Assert.IsTrue(module.TryGet<TestPlayable>(out var resolved));
            Assert.AreSame(playable, resolved);
            Assert.Throws<GameException>(() => module.Register(new TestPlayable()));

            App.Unregister<PlayableModule>().GetAwaiter().GetResult();

            Assert.AreEqual(1, playable.DisposeCount);
        }

        [Test]
        public void Registry_WhenPlayableMissing_ReportsMissingWithoutCreating()
        {
            var module = App.Playable;

            Assert.IsFalse(module.TryGet<TestPlayable>(out var playable));
            Assert.IsNull(playable);
            Assert.Throws<GameException>(() => module.Get<TestPlayable>());
        }

        [Test]
        public void Shutdown_WhenOnePlayableThrows_StillDisposesEveryPlayableAndClearsRegistry()
        {
            var module = App.Playable;
            var first = new TestPlayable { DisposeException = new InvalidOperationException("dispose failed") };
            var second = new OtherTestPlayable();
            module.Register(first);
            module.Register(second);

            Assert.Throws<InvalidOperationException>(() => App.Unregister<PlayableModule>().GetAwaiter().GetResult());

            Assert.AreEqual(1, first.DisposeCount);
            Assert.AreEqual(1, second.DisposeCount);
            Assert.IsFalse(App.TryGetRegistered<PlayableModule>(out _));
        }

        [Test]
        public void Handle_WhenPlaying_CanPauseResumeAndStopOnce()
        {
            var handle = new TestPlayableHandle();
            handle.Start();

            handle.Pause();
            Assert.AreEqual(PlayableStatus.Paused, handle.Status);
            Assert.AreEqual(1, handle.PauseCount);

            handle.Resume();
            Assert.AreEqual(PlayableStatus.Playing, handle.Status);
            Assert.AreEqual(1, handle.ResumeCount);

            handle.Stop();
            handle.Stop();
            handle.WaitForCompletionAsync().GetAwaiter().GetResult();

            Assert.AreEqual(PlayableStatus.Stopped, handle.Status);
            Assert.AreEqual(1, handle.StopCount);
        }

        [Test]
        public void Handle_WhenTerminalSubmitted_FirstTerminalWins()
        {
            var handle = new TestPlayableHandle();
            handle.Start();

            Assert.IsTrue(handle.Complete());
            Assert.IsFalse(handle.Cancel());
            Assert.IsFalse(handle.Fail(new InvalidOperationException("late")));

            handle.WaitForCompletionAsync().GetAwaiter().GetResult();
            Assert.AreEqual(PlayableStatus.Completed, handle.Status);
            Assert.IsNull(handle.Error);
        }

        [Test]
        public void Handle_WhenHookThrows_FailsAndPreservesException()
        {
            var handle = new TestPlayableHandle { PauseException = new InvalidOperationException("pause failed") };
            handle.Start();

            var exception = Assert.Throws<InvalidOperationException>(() => handle.Pause());

            Assert.AreSame(exception, handle.Error);
            Assert.AreEqual(PlayableStatus.Failed, handle.Status);
            Assert.Throws<InvalidOperationException>(() => handle.WaitForCompletionAsync().GetAwaiter().GetResult());
        }

        [Test]
        public void Handle_WhenCanceled_WaitObservesCancellation()
        {
            var handle = new TestPlayableHandle();
            handle.Start();

            handle.Cancel();

            Assert.AreEqual(PlayableStatus.Canceled, handle.Status);
            Assert.Throws<OperationCanceledException>(() => handle.WaitForCompletionAsync().GetAwaiter().GetResult());
        }

        private sealed class TestPlayable : PlayableBase<object, TestPlayableHandle>
        {
            public int DisposeCount { get; private set; }
            public Exception DisposeException { get; set; }

            public override UniTask<TestPlayableHandle> PlayAsync(
                object options,
                CancellationToken cancellationToken = default)
            {
                var handle = new TestPlayableHandle();
                handle.Start();
                return UniTask.FromResult(handle);
            }

            public override void Dispose()
            {
                DisposeCount++;
                if (DisposeException != null)
                {
                    throw DisposeException;
                }
            }
        }

        private sealed class OtherTestPlayable : PlayableBase<object, TestPlayableHandle>
        {
            public int DisposeCount { get; private set; }

            public override UniTask<TestPlayableHandle> PlayAsync(
                object options,
                CancellationToken cancellationToken = default)
            {
                var handle = new TestPlayableHandle();
                handle.Start();
                return UniTask.FromResult(handle);
            }

            public override void Dispose()
            {
                DisposeCount++;
            }
        }

        private sealed class TestPlayableHandle : PlayableHandle
        {
            public int PauseCount { get; private set; }
            public int ResumeCount { get; private set; }
            public int StopCount { get; private set; }
            public Exception PauseException { get; set; }

            public void Start()
            {
                SetPlaying();
            }

            public bool Complete()
            {
                return SetCompleted();
            }

            public bool Cancel()
            {
                return SetCanceled();
            }

            public bool Fail(Exception exception)
            {
                return SetFailed(exception);
            }

            protected override void OnPause()
            {
                PauseCount++;
                if (PauseException != null)
                {
                    throw PauseException;
                }
            }

            protected override void OnResume()
            {
                ResumeCount++;
            }

            protected override void OnStop()
            {
                StopCount++;
            }
        }
    }
}
