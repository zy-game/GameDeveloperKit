using System;
using System.Collections;
using System.Reflection;
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

            var operation = module.ExecuteWithKey<ImmediateResultOperation>("sync-result", 42);

            Assert.AreEqual(OperationStatus.Succeeded, operation.Status);
            Assert.AreEqual(42, operation.Value);
        }

        [Test]
        public void Execute_WhenUsingTypeKey_PassesArgumentsAndCompletesWithValue()
        {
            var module = new OperationModule();

            var operation = module.Execute<ImmediateResultOperation>(42);

            Assert.AreEqual(OperationStatus.Succeeded, operation.Status);
            Assert.AreEqual(42, operation.Value);
            Assert.Throws<GameException>(() => module.SetCanceled<ImmediateResultOperation>());
        }

        [Test]
        public void WaitCompletionAsync_WhenOperationIsCompletedExternally_ReturnsOperation()
        {
            var module = new OperationModule();

            var task = module.WaitCompletionWithKeyAsync<PendingIntOperation>("wait-result");
            module.SetResult("wait-result", 9);
            var operation = task.GetAwaiter().GetResult();

            Assert.AreEqual(OperationStatus.Succeeded, operation.Status);
            Assert.AreEqual(9, operation.Value);
        }

        [Test]
        public void WaitCompletionAsync_WhenUsingTypeKeyAndCompletedExternally_ReturnsOperation()
        {
            var module = new OperationModule();

            var task = module.WaitCompletionAsync<PendingIntOperation>();
            module.SetResult<PendingIntOperation>(9);
            var operation = task.GetAwaiter().GetResult();

            Assert.AreEqual(OperationStatus.Succeeded, operation.Status);
            Assert.AreEqual(9, operation.Value);
        }

        [Test]
        public void Execute_WhenKeyIsNull_Throws()
        {
            var module = new OperationModule();

            Assert.Throws<ArgumentNullException>(() => module.ExecuteWithKey<PendingIntOperation>(null));
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

            var operation = module.ExecuteWithKey<ThrowingOperation>("throwing");

            Assert.AreEqual(OperationStatus.Failed, operation.Status);
            Assert.IsInstanceOf<InvalidOperationException>(operation.Error);
        }

        [Test]
        public void Execute_WhenSameKeyAndTypeIsAlreadyRunning_Throws()
        {
            var module = new OperationModule();

            module.ExecuteWithKey<PendingIntOperation>("duplicate");

            Assert.Throws<GameException>(() => module.ExecuteWithKey<PendingIntOperation>("duplicate"));
        }

        [Test]
        public void Execute_WhenSameTypeKeyIsAlreadyRunning_Throws()
        {
            var module = new OperationModule();

            module.Execute<PendingIntOperation>();

            Assert.Throws<GameException>(() => module.Execute<PendingIntOperation>());
        }

        [Test]
        public void Execute_WhenSameHandleIsAlreadyRunning_ThrowsForAnyKey()
        {
            var module = new OperationModule();
            var operation = new PendingIntOperation();

            module.Execute("first-key", operation);

            Assert.Throws<GameException>(() => module.Execute("second-key", operation));
        }

        [Test]
        public void Execute_WhenHandleAlreadyCompleted_ThrowsInsteadOfReusingIt()
        {
            var module = new OperationModule();
            var operation = new PendingIntOperation();

            module.Execute("first-run", operation);
            module.SetResult("first-run", 3);

            Assert.Throws<GameException>(() => module.Execute("second-run", operation));
        }

        [Test]
        public void OperationHandle_PublicSurface_DoesNotExposeReusableLifecycleMethods()
        {
            var publicInstance = BindingFlags.Instance | BindingFlags.Public;

            Assert.IsNull(typeof(OperationHandle).GetMethod("SetReset", publicInstance));
            Assert.IsNull(typeof(OperationHandle).GetMethod("Release", publicInstance));
            Assert.IsNull(typeof(OperationHandle).GetMethod("SetPause", publicInstance));
            Assert.IsNull(typeof(OperationHandle).GetMethod("SetResume", publicInstance));
            Assert.IsFalse(typeof(IReference).IsAssignableFrom(typeof(OperationHandle)));
        }

        [Test]
        public void WaitCompletionWithKeyAsync_WhenSameTypeUsesDifferentKeys_AllowsBoth()
        {
            var module = new OperationModule();

            var firstTask = module.WaitCompletionWithKeyAsync<PendingIntOperation>("first");
            var secondTask = module.WaitCompletionWithKeyAsync<PendingIntOperation>("second");
            module.SetResult("first", 1);
            module.SetResult("second", 2);
            var first = firstTask.GetAwaiter().GetResult();
            var second = secondTask.GetAwaiter().GetResult();

            Assert.AreEqual(OperationStatus.Succeeded, first.Status);
            Assert.AreEqual(1, first.Value);
            Assert.AreEqual(OperationStatus.Succeeded, second.Status);
            Assert.AreEqual(2, second.Value);
        }

        [Test]
        public void Execute_WhenSameKeyHasDifferentOperationTypes_AllowsBothUntilExternalSetIsAmbiguous()
        {
            var module = new OperationModule();

            module.ExecuteWithKey<PendingIntOperation>("shared");
            module.ExecuteWithKey<PendingStringOperation>("shared");

            Assert.Throws<GameException>(() => module.SetCanceled("shared"));
        }

        [Test]
        public void SetResult_WhenOperationIsRunning_CompletesWithValueAndRemovesOperation()
        {
            var module = new OperationModule();
            var operation = module.ExecuteWithKey<PendingIntOperation>("external-result");

            module.SetResult("external-result", 7);

            Assert.AreEqual(OperationStatus.Succeeded, operation.Status);
            Assert.AreEqual(7, operation.Value);
            Assert.Throws<GameException>(() => module.SetCanceled("external-result"));
        }

        [Test]
        public void HandleCompletionMethods_WhenCalledExternally_CompleteTheCurrentExecution()
        {
            var resultModule = new OperationModule();
            var result = resultModule.ExecuteWithKey<PendingIntOperation>("direct-result");
            result.SetProgress(0.5f);
            result.SetResult(11);

            var errorModule = new OperationModule();
            var error = errorModule.ExecuteWithKey<PendingOperation>("direct-error");
            var exception = new InvalidOperationException("direct failure");
            error.SetException(exception);
            ObserveCompletion(error);

            var cancelModule = new OperationModule();
            var canceled = cancelModule.ExecuteWithKey<PendingOperation>("direct-cancel");
            canceled.SetCancel();
            ObserveCompletion(canceled);

            Assert.AreEqual(OperationStatus.Succeeded, result.Status);
            Assert.AreEqual(11, result.Value);
            Assert.AreEqual(OperationStatus.Failed, error.Status);
            Assert.AreSame(exception, error.Error);
            Assert.AreEqual(OperationStatus.Cancelled, canceled.Status);
        }

        [Test]
        public void RemoveOperation_WhenCalledForStaleEntry_DoesNotRemoveCurrentEntry()
        {
            var module = new OperationModule();
            module.ExecuteWithKey<PendingIntOperation>("entry-identity");
            var staleEntry = GetSingleEntry(module);
            module.SetResult("entry-identity", 1);

            var current = module.ExecuteWithKey<PendingIntOperation>("entry-identity");
            InvokeRemoveOperation(module, staleEntry);
            module.SetResult("entry-identity", 2);

            Assert.AreEqual(OperationStatus.Succeeded, current.Status);
            Assert.AreEqual(2, current.Value);
        }

        [Test]
        public void SetResult_WhenTypeKeyValueTypeDoesNotMatch_ThrowsAndKeepsOperationRunning()
        {
            var module = new OperationModule();
            var operation = module.Execute<PendingIntOperation>();

            Assert.Throws<GameException>(() => module.SetResult<PendingIntOperation>("text"));
            Assert.AreEqual(OperationStatus.Running, operation.Status);

            module.SetCanceled<PendingIntOperation>();
        }

        [Test]
        public void SetResult_WhenUsingTypeKeyWithoutValue_CompletesOperation()
        {
            var module = new OperationModule();
            var operation = module.Execute<PendingOperation>();

            module.SetResult<PendingOperation>();

            Assert.AreEqual(OperationStatus.Succeeded, operation.Status);
        }

        [Test]
        public void SetResult_WhenValueTypeDoesNotMatch_ThrowsAndKeepsOperationRunning()
        {
            var module = new OperationModule();
            var operation = module.ExecuteWithKey<PendingIntOperation>("wrong-result");

            Assert.Throws<GameException>(() => module.SetResult("wrong-result", "text"));
            Assert.AreEqual(OperationStatus.Running, operation.Status);

            module.SetCanceled("wrong-result");
        }

        [Test]
        public void SetException_WhenOperationIsRunning_CompletesWithError()
        {
            var module = new OperationModule();
            var operation = module.ExecuteWithKey<PendingIntOperation>("external-error");
            var exception = new InvalidOperationException("failed");

            module.SetException("external-error", exception);
            ObserveCompletion(operation);

            Assert.AreEqual(OperationStatus.Failed, operation.Status);
            Assert.AreSame(exception, operation.Error);
        }

        [Test]
        public void SetException_WhenUsingTypeKey_CompletesWithError()
        {
            var module = new OperationModule();
            var operation = module.Execute<PendingIntOperation>();
            var exception = new InvalidOperationException("failed");

            module.SetException<PendingIntOperation>(exception);
            ObserveCompletion(operation);

            Assert.AreEqual(OperationStatus.Failed, operation.Status);
            Assert.AreSame(exception, operation.Error);
        }

        [Test]
        public void SetCanceled_WhenOperationIsRunning_CompletesWithCanceled()
        {
            var module = new OperationModule();
            var operation = module.ExecuteWithKey<PendingIntOperation>("external-cancel");

            module.SetCanceled("external-cancel");

            Assert.AreEqual(OperationStatus.Cancelled, operation.Status);
        }

        [Test]
        public void SetCanceled_WhenUsingTypeKey_CompletesWithCanceled()
        {
            var module = new OperationModule();
            var operation = module.Execute<PendingIntOperation>();

            module.SetCanceled<PendingIntOperation>();

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
        public void SetMethods_WhenTypeKeyIsMissing_Throw()
        {
            var module = new OperationModule();

            Assert.Throws<GameException>(() => module.SetResult<PendingIntOperation>(1));
            Assert.Throws<GameException>(() => module.SetException<PendingIntOperation>(new Exception("missing")));
            Assert.Throws<GameException>(() => module.SetCanceled<PendingIntOperation>());
        }

        [Test]
        public void Shutdown_WhenOperationIsRunning_CancelsAndClearsOperation()
        {
            var module = new OperationModule();
            var operation = module.ExecuteWithKey<PendingIntOperation>("shutdown");

            module.Shutdown();
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

        private static object GetSingleEntry(OperationModule module)
        {
            var field = typeof(OperationModule).GetField("m_Operations", BindingFlags.Instance | BindingFlags.NonPublic);
            var operations = (IDictionary)field.GetValue(module);
            var enumerator = operations.Values.GetEnumerator();
            Assert.IsTrue(enumerator.MoveNext());
            return enumerator.Current;
        }

        private static void InvokeRemoveOperation(OperationModule module, object entry)
        {
            var method = typeof(OperationModule).GetMethod("RemoveOperation", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(module, new[] { entry });
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

        private sealed class PendingOperation : OperationHandle
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
