using GameDeveloperKit.Logger;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace GameDeveloperKit.Tests
{
    public abstract class RuntimeTestBase
    {
        [SetUp]
        public void RuntimeTestBaseSetUp()
        {
            var test = TestContext.CurrentContext.Test;
            LogTestMessage($"[TEST START] {test.ClassName}.{test.MethodName}", false);
        }

        [TearDown]
        public void RuntimeTestBaseTearDown()
        {
            var context = TestContext.CurrentContext;
            var test = context.Test;
            var result = context.Result;
            var status = result.Outcome.Status;
            var message = string.IsNullOrEmpty(result.Message) ? string.Empty : $" - {result.Message}";
            if (status == TestStatus.Passed)
            {
                LogTestMessage($"[TEST END] {test.ClassName}.{test.MethodName}: {result.Outcome}{message}", false);
                return;
            }

            LogTestMessage($"[TEST END] {test.ClassName}.{test.MethodName}: {result.Outcome}{message}", true);
        }

        private static void LogTestMessage(string message, bool warning)
        {
            if (!App.TryGetRegistered<DebugModule>(out var debug))
            {
                TestContext.Progress.WriteLine(message);
                return;
            }

            if (warning)
            {
                debug.Warning(message);
                return;
            }

            debug.Info(message);
        }
    }
}
