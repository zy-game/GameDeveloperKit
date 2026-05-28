using System;
using GameDeveloperKit.Operation;
using NUnit.Framework;

namespace GameDeveloperKit.Tests
{
    public sealed class OperationModuleTests : RuntimeTestBase
    {
        [Test]
        public void Execute_WhenOperationSetsResult_CompletesWithValue()
        {
            var module = new OperationModule();

            var operation = module.Execute<ImmediateResultOperation>("sync-result", 42);

            Assert.AreEqual(OperationStatus.Succeeded, operation.Status);
            Assert.AreEqual(42, operation.Value);
        }

        [Test]
        public void WaitCompletionAsync_WhenOperationIsCompletedExternally_ReturnsOperation()
        {
            var module = new OperationModule();

            var task = module.WaitCompletionAsync<PendingIntOperation>("wait-result");
            module.SetResult("wait-result", 9);
            var operation = task.GetAwaiter().GetResult();

            Assert.AreEqual(OperationStatus.Succeeded, operation.Status);
            Assert.AreEqual(9, operation.Value);
        }

        [Test]
        public void Execute_WhenKeyIsNull_Throws()
        {
            var module = new OperationModule();

            Assert.Throws<ArgumentNullException>(() => module.Execute<PendingIntOperation>(null));
        }

        [Test]
        public void Execute_WhenOperationIsNull_Throws()
        {
            var module = new OperationModule();

            Assert.Throws<ArgumentNullException>(() => module.Execute("null-operation", null));
        }

        [Test]
        public void Execute_WhenOperationThrows_SetsFailedStatus()
        {
            var module = new OperationModule();

            var operation = module.Execute<ThrowingOperation>("throwing");

            Assert.AreEqual(OperationStatus.Failed, operation.Status);
            Assert.IsInstanceOf<InvalidOperationException>(operation.Error);
        }

        [Test]
        public void Execute_WhenSameKeyAndTypeIsAlreadyRunning_Throws()
        {
            var module = new OperationModule();

            module.Execute<PendingIntOperation>("duplicate");

            Assert.Throws<GameException>(() => module.Execute<PendingIntOperation>("duplicate"));
        }

        [Test]
        public void Execute_WhenSameKeyHasDifferentOperationTypes_AllowsBothUntilExternalSetIsAmbiguous()
        {
            var module = new OperationModule();

            module.Execute<PendingIntOperation>("shared");
            module.Execute<PendingStringOperation>("shared");

            Assert.Throws<GameException>(() => module.SetCanceled("shared"));
        }

        [Test]
        public void SetResult_WhenOperationIsRunning_CompletesWithValueAndRemovesOperation()
        {
            var module = new OperationModule();
            var operation = module.Execute<PendingIntOperation>("external-result");

            module.SetResult("external-result", 7);

            Assert.AreEqual(OperationStatus.Succeeded, operation.Status);
            Assert.AreEqual(7, operation.Value);
            Assert.Throws<GameException>(() => module.SetCanceled("external-result"));
        }

        [Test]
        public void SetResult_WhenValueTypeDoesNotMatch_ThrowsAndKeepsOperationRunning()
        {
            var module = new OperationModule();
            var operation = module.Execute<PendingIntOperation>("wrong-result");

            Assert.Throws<GameException>(() => module.SetResult("wrong-result", "text"));
            Assert.AreEqual(OperationStatus.Running, operation.Status);

            module.SetCanceled("wrong-result");
        }

        [Test]
        public void SetException_WhenOperationIsRunning_CompletesWithError()
        {
            var module = new OperationModule();
            var operation = module.Execute<PendingIntOperation>("external-error");
            var exception = new InvalidOperationException("failed");

            module.SetException("external-error", exception);
            ObserveCompletion(operation);

            Assert.AreEqual(OperationStatus.Failed, operation.Status);
            Assert.AreSame(exception, operation.Error);
        }

        [Test]
        public void SetCanceled_WhenOperationIsRunning_CompletesWithCanceled()
        {
            var module = new OperationModule();
            var operation = module.Execute<PendingIntOperation>("external-cancel");

            module.SetCanceled("external-cancel");

            Assert.AreEqual(OperationStatus.Cancelled, operation.Status);
        }

        [Test]
        public void SetMethods_WhenKeyIsMissing_Throw()
        {
            var module = new OperationModule();

            Assert.Throws<GameException>(() => module.SetResult("missing", 1));
            Assert.Throws<GameException>(() => module.SetException("missing", new Exception("missing")));
            Assert.Throws<GameException>(() => module.SetCanceled("missing"));
        }

        [Test]
        public void Shutdown_WhenOperationIsRunning_CancelsAndClearsOperation()
        {
            var module = new OperationModule();
            var operation = module.Execute<PendingIntOperation>("shutdown");

            module.Shutdown().GetAwaiter().GetResult();
            ObserveCompletion(operation);

            Assert.AreEqual(OperationStatus.Cancelled, operation.Status);
            Assert.Throws<GameException>(() => module.SetCanceled("shutdown"));
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

        private sealed class ImmediateResultOperation : OperationHandle<int>
        {
            public override void Execute(params object[] args)
            {
                SetResult((int)args[0]);
            }
        }

        private sealed class PendingIntOperation : OperationHandle<int>
        {
            public override void Execute(params object[] args)
            {
            }
        }

        private sealed class PendingStringOperation : OperationHandle<string>
        {
            public override void Execute(params object[] args)
            {
            }
        }

        private sealed class ThrowingOperation : OperationHandle
        {
            public override void Execute(params object[] args)
            {
                throw new InvalidOperationException("execute failed");
            }
        }
    }
}
