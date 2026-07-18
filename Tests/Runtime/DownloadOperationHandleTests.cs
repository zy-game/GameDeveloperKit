using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Download;
using GameDeveloperKit.Operation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class DownloadOperationHandleTests : RuntimeTestBase
    {
        [SetUp]
        public void SetUp()
        {
            try
            {
                App.Register<OperationModule>();
            }
            catch (GameException)
            {
            }
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                App.Unregister<OperationModule>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }
        }

        [Test]
        public void DownloadHandler_WhenProgressChanges_ReportsOperationProgress()
        {
            var handler = App.Operation.ExecuteWithKey<TestDownloadHandler>("https://example.com/a.bin", TestDownloadResult.Pending);
            var observed = -1f;

            handler.SetProgressHandle(progress => observed = progress);
            handler.SetBytesForTest(25, 100);
            handler.RaiseProgressForTest();

            Assert.AreEqual(0.25f, observed);
        }

        [Test]
        public void DownloadHandler_WhenPaused_DoesNotCompleteOperation()
        {
            var handler = App.Operation.ExecuteWithKey<TestDownloadHandler>("https://example.com/a.bin", TestDownloadResult.Pending);

            handler.SetPause();

            Assert.AreEqual(OperationStatus.Paused, handler.Status);
        }

        [Test]
        public void DownloadHandler_WhenResumed_ContinuesTheSameExecution()
        {
            var handler = App.Operation.ExecuteWithKey<TestDownloadHandler>("https://example.com/resume.bin", TestDownloadResult.Pending);

            handler.SetPause();
            handler.SetResume();
            handler.SetResult();

            Assert.AreEqual(OperationStatus.Succeeded, handler.Status);
        }

        [Test]
        public void DownloadHandler_WhenCompletedObserverThrows_CommitsTerminalBeforeNotifyingAllObservers()
        {
            var handler = new DownloadHandler();
            var statusAtNotification = OperationStatus.None;
            var secondObserverCount = 0;
            handler.Completed += value =>
            {
                statusAtNotification = value.Status;
                throw new InvalidOperationException("observer failed");
            };
            handler.Completed += _ => secondObserverCount++;

            handler.CompleteSucceededForTest();

            Assert.AreEqual(OperationStatus.Succeeded, handler.Status);
            Assert.AreEqual(OperationStatus.Succeeded, statusAtNotification);
            Assert.AreEqual(1, secondObserverCount);
            Assert.IsInstanceOf<InvalidOperationException>(handler.LastObserverException);
            Assert.DoesNotThrow(() => handler.WaitCompletionAsync().GetAwaiter().GetResult());
        }

        [Test]
        public void DownloadHandler_WhenFailedObserverThrows_PreservesOriginalFailureAndNotifiesRemainingObservers()
        {
            var handler = new DownloadHandler();
            var failure = new InvalidOperationException("download failed");
            var statusAtNotification = OperationStatus.None;
            var secondObserverCount = 0;
            handler.Failed += value =>
            {
                statusAtNotification = value.Status;
                throw new InvalidOperationException("observer failed");
            };
            handler.Failed += _ => secondObserverCount++;

            handler.CompleteFailedForTest(failure);
            ObserveCompletion(handler);

            Assert.AreEqual(OperationStatus.Failed, handler.Status);
            Assert.AreEqual(OperationStatus.Failed, statusAtNotification);
            Assert.AreSame(failure, handler.Error);
            Assert.AreEqual(1, secondObserverCount);
            Assert.IsInstanceOf<InvalidOperationException>(handler.LastObserverException);
        }

        [Test]
        public void DownloadHandler_WhenCanceledObserverThrows_CommitsCancellationAndNotifiesRemainingObservers()
        {
            var handler = new DownloadHandler();
            var statusAtNotification = OperationStatus.None;
            var secondObserverCount = 0;
            handler.Canceled += value =>
            {
                statusAtNotification = value.Status;
                throw new InvalidOperationException("observer failed");
            };
            handler.Canceled += _ => secondObserverCount++;

            handler.SetCancel();
            ObserveCompletion(handler);

            Assert.AreEqual(OperationStatus.Cancelled, handler.Status);
            Assert.AreEqual(OperationStatus.Cancelled, statusAtNotification);
            Assert.AreEqual(DownloadFailureKind.Canceled, handler.FailureKind);
            Assert.AreEqual(1, secondObserverCount);
            Assert.IsInstanceOf<InvalidOperationException>(handler.LastObserverException);
        }

        [Test]
        public void DownloadHandler_WhenProgressObserverThrows_DoesNotFailOperationOrSkipRemainingObservers()
        {
            var handler = new DownloadHandler();
            var secondObserverCount = 0;
            handler.ProgressChanged += _ => throw new InvalidOperationException("observer failed");
            handler.ProgressChanged += _ => secondObserverCount++;
            handler.SetBytesForTest(1, 2);

            Assert.DoesNotThrow(() => handler.RaiseProgressForTest());

            Assert.AreEqual(OperationStatus.None, handler.Status);
            Assert.AreEqual(1, secondObserverCount);
            Assert.IsInstanceOf<InvalidOperationException>(handler.LastObserverException);
        }

        [Test]
        public void DownloadHandler_WhenFailed_CannotResumeTerminalExecution()
        {
            var handler = App.Operation.ExecuteWithKey<TestDownloadHandler>("https://example.com/failed.bin", TestDownloadResult.Failed);
            ObserveCompletion(handler);

            handler.SetResume();

            Assert.AreEqual(OperationStatus.Failed, handler.Status);
            Assert.IsInstanceOf<InvalidOperationException>(handler.Error);
        }

        [UnityTest]
        public IEnumerator DownloadHandler_WhenCanceled_CompletesOperationAsCancelled()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var handler = App.Operation.ExecuteWithKey<TestDownloadHandler>("https://example.com/a.bin", TestDownloadResult.Pending);

                handler.SetCancel();
                await handler.WaitCompletionAsync();

                Assert.AreEqual(OperationStatus.Cancelled, handler.Status);
                Assert.AreEqual(DownloadFailureKind.Canceled, handler.FailureKind);
            });
        }

        [UnityTest]
        public IEnumerator DownloadListHandler_WhenAnyItemFails_CompletesOperationAsFailedAfterContinuing()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var first = App.Operation.ExecuteWithKey<TestDownloadHandler>("https://example.com/a.bin", TestDownloadResult.Failed);
                var second = App.Operation.ExecuteWithKey<TestDownloadHandler>("https://example.com/b.bin", TestDownloadResult.Completed);
                var list = App.Operation.ExecuteWithKey<DownloadListHandler>(
                    new List<DownloadHandler> { first, second },
                    new List<DownloadHandler> { first, second });

                await list.WaitCompletionAsync();

                Assert.AreEqual(OperationStatus.Failed, list.Status);
                Assert.AreEqual(OperationStatus.Failed, first.Status);
                Assert.AreEqual(OperationStatus.Succeeded, second.Status);
            });
        }

        [Test]
        public void DownloadListHandler_WhenCanceled_CompletesOperationAsCancelled()
        {
            var first = App.Operation.ExecuteWithKey<TestDownloadHandler>("https://example.com/a.bin", TestDownloadResult.Pending);
            var second = App.Operation.ExecuteWithKey<TestDownloadHandler>("https://example.com/b.bin", TestDownloadResult.Pending);
            var list = App.Operation.ExecuteWithKey<DownloadListHandler>(
                new List<DownloadHandler> { first, second },
                new List<DownloadHandler> { first, second });

            list.SetCancel();
            ObserveCompletion(list);
            ObserveCompletion(first);
            ObserveCompletion(second);

            Assert.AreEqual(OperationStatus.Cancelled, list.Status);
            Assert.AreEqual(OperationStatus.Cancelled, first.Status);
            Assert.AreEqual(OperationStatus.Cancelled, second.Status);
        }

        [Test]
        public void DownloadListHandler_WhenCompletedObserverThrows_CommitsTerminalAndNotifiesRemainingObservers()
        {
            var first = App.Operation.ExecuteWithKey<TestDownloadHandler>("https://example.com/list-first.bin", TestDownloadResult.Completed);
            var second = App.Operation.ExecuteWithKey<TestDownloadHandler>("https://example.com/list-second.bin", TestDownloadResult.Completed);
            var list = new DownloadListHandler();
            var statusAtNotification = OperationStatus.None;
            var secondObserverCount = 0;
            list.Completed += value =>
            {
                statusAtNotification = value.Status;
                throw new InvalidOperationException("observer failed");
            };
            list.Completed += _ => secondObserverCount++;

            App.Operation.Execute(
                "download-list-observer-order",
                list,
                new List<DownloadHandler> { first, second });

            Assert.AreEqual(OperationStatus.Succeeded, list.Status);
            Assert.AreEqual(OperationStatus.Succeeded, statusAtNotification);
            Assert.AreEqual(1, secondObserverCount);
            Assert.IsInstanceOf<InvalidOperationException>(list.LastObserverException);
        }

        private static void ObserveCompletion(OperationHandle operation)
        {
            try
            {
                operation.WaitCompletionAsync().GetAwaiter().GetResult();
            }
            catch
            {
            }
        }

        private sealed class TestDownloadHandler : DownloadHandler
        {
            private TestDownloadResult m_Result;

            internal TestDownloadHandler()
            {
            }

            public override void Execute(params object[] args)
            {
                m_Result = args.Length > 0 && args[0] is TestDownloadResult result
                    ? result
                    : TestDownloadResult.Pending;

                if (m_Result is TestDownloadResult.Completed)
                {
                    SetResult();
                    return;
                }

                if (m_Result is TestDownloadResult.Failed)
                {
                    SetFailureKindForTest(DownloadFailureKind.Network);
                    SetException(new InvalidOperationException("failed"));
                }
            }
        }

        private enum TestDownloadResult
        {
            Pending,
            Completed,
            Failed,
        }
    }
}
