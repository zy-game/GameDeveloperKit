using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    public abstract class RuntimeTestBase
    {
        [SetUp]
        public void RuntimeTestBaseSetUp()
        {
            var test = TestContext.CurrentContext.Test;
            App.Debug.Info($"[TEST START] {test.ClassName}.{test.MethodName}");
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
                App.Debug.Info($"[TEST END] {test.ClassName}.{test.MethodName}: {result.Outcome}{message}");
                return;
            }

            App.Debug.Warning($"[TEST END] {test.ClassName}.{test.MethodName}: {result.Outcome}{message}");
        }
    }
}