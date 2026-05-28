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
                Super.Register<OperationModule>().GetAwaiter().GetResult();
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
                Super.Unregister<OperationModule>().GetAwaiter().GetResult();
            }
            catch (GameException)
            {
            }
        }

        [Test]
        public void DownloadHandler_WhenProgressChanges_ReportsOperationProgress()
        {
            var handler = Super.Operation.Execute<TestDownloadHandler>("https://example.com/a.bin", TestDownloadResult.Pending);
            var observed = -1f;

            handler.SetProgressHandle(progress => observed = progress);
            handler.SetBytesForTest(25, 100);
            handler.RaiseProgressForTest();

            Assert.AreEqual(0.25f, observed);
        }

        [Test]
        public void DownloadHandler_WhenPaused_DoesNotCompleteOperation()
        {
            var handler = Super.Operation.Execute<TestDownloadHandler>("https://example.com/a.bin", TestDownloadResult.Pending);
            handler.SetRunningForTest();

            handler.SetPause();

            Assert.AreEqual(OperationStatus.Paused, handler.Status);
        }

        [UnityTest]
        public IEnumerator DownloadHandler_WhenCanceled_CompletesOperationAsCancelled()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var handler = Super.Operation.Execute<TestDownloadHandler>("https://example.com/a.bin", TestDownloadResult.Pending);
                handler.SetRunningForTest();

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
                var first = Super.Operation.Execute<TestDownloadHandler>("https://example.com/a.bin", TestDownloadResult.Failed);
                var second = Super.Operation.Execute<TestDownloadHandler>("https://example.com/b.bin", TestDownloadResult.Completed);
                var list = Super.Operation.Execute<DownloadListHandler>(
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
            var first = Super.Operation.Execute<TestDownloadHandler>("https://example.com/a.bin", TestDownloadResult.Pending);
            var second = Super.Operation.Execute<TestDownloadHandler>("https://example.com/b.bin", TestDownloadResult.Pending);
            var list = Super.Operation.Execute<DownloadListHandler>(
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
