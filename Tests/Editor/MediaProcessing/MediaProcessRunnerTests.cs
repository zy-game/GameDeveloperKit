using System;
using System.Collections;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.MediaEditor;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace GameDeveloperKit.Tests
{
    public sealed class MediaProcessRunnerTests
    {
        [Test]
        public void BuildArguments_WhenValuesContainSpacesAndQuotes_ProducesWindowsSafeCommandLine()
        {
            var commandLine = MediaProcessRunner.BuildArguments(new[]
            {
                "-i",
                "E:\\Media Files\\rain \"night\".mp4",
                string.Empty
            });

            Assert.AreEqual(
                "-i \"E:\\Media Files\\rain \\\"night\\\".mp4\" \"\"",
                commandLine);
        }

        [UnityTest]
        public IEnumerator RunAsync_WhenExecutableSucceeds_CapturesBoundedOutput()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var runner = new MediaProcessRunner();
                var result = await runner.RunAsync(
                    new MediaProcessRequest(
                        "where.exe",
                        new[] { "where.exe" },
                        Directory.GetCurrentDirectory(),
                        TimeSpan.FromSeconds(10)),
                    CancellationToken.None);

                Assert.IsTrue(result.Succeeded);
                StringAssert.Contains("where.exe", result.StandardOutput.ToLowerInvariant());
                Assert.GreaterOrEqual(result.Elapsed, TimeSpan.Zero);
            });
        }

        [UnityTest]
        public IEnumerator RunAsync_WhenExecutableReturnsNonZero_PreservesExitCode()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var runner = new MediaProcessRunner();
                var result = await runner.RunAsync(
                    new MediaProcessRequest(
                        "where.exe",
                        new[] { "gdk-file-that-does-not-exist.exe" },
                        Directory.GetCurrentDirectory(),
                        TimeSpan.FromSeconds(10)),
                    CancellationToken.None);

                Assert.IsFalse(result.Succeeded);
                Assert.AreNotEqual(0, result.ExitCode);
            });
        }
    }
}
