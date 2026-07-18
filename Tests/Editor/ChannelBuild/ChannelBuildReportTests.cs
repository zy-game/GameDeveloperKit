using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using GameDeveloperKit.ChannelBuild;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace GameDeveloperKit.Tests
{
    [TestFixture]
    public sealed class ChannelBuildReportTests
    {
        private static readonly DateTime StartUtc =
            new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc);

        private string m_Root;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(
                Path.GetTempPath(),
                "gdk-channel-report-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_Root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_Root))
            {
                Directory.Delete(m_Root, true);
            }
        }

        [Test]
        public void Report_SuccessSnapshot_PreservesImmutableFields()
        {
            var context = new ChannelBuildReportContext("dev", "Android", "1.2.3");
            var ci = new CiBuildMetadata("jenkins", "game/build", "42", null, "abc123");
            var warnings = new List<string> { "warning" };
            var report = CreateSuccess(context, ci, warnings: warnings);

            warnings[0] = "mutated";

            Assert.AreEqual(1, report.SchemaVersion);
            Assert.AreEqual("succeeded", report.Status);
            Assert.AreEqual("none", report.FailureKind);
            Assert.AreEqual(0, report.ExitCode);
            Assert.AreSame(context, report.Context);
            Assert.AreSame(ci, report.Ci);
            Assert.AreEqual("warning", report.Warnings[0]);
            Assert.IsEmpty(report.Artifacts);
            Assert.IsEmpty(report.Steps);
            Assert.AreEqual(StartUtc, report.StartedAtUtc);
            Assert.AreEqual(StartUtc.AddSeconds(1), report.FinishedAtUtc);
        }

        [TestCase(ChannelBuildExitCode.InvalidInput, "invalid-input")]
        [TestCase(ChannelBuildExitCode.PipelineFailed, "pipeline")]
        [TestCase(ChannelBuildExitCode.ResourceBuildFailed, "resource-build")]
        [TestCase(ChannelBuildExitCode.PlayerBuildFailed, "player-build")]
        [TestCase(ChannelBuildExitCode.ReportFailed, "report")]
        public void Report_FailureSnapshot_AcceptsMatchingClassification(
            ChannelBuildExitCode exitCode,
            string failureKind)
        {
            var report = CreateFailure(exitCode, failureKind);

            Assert.AreEqual("failed", report.Status);
            Assert.AreEqual(failureKind, report.FailureKind);
            Assert.AreEqual((int)exitCode, report.ExitCode);
            Assert.IsNull(report.Context);
        }

        [Test]
        public void Report_RejectsInconsistentSuccessAndFailureOutcomes()
        {
            Assert.Throws<ArgumentException>(() => new ChannelBuildReport(
                "succeeded", "none", ChannelBuildExitCode.Success,
                null, null, null, null, null, StartUtc, StartUtc));
            Assert.Throws<ArgumentException>(() => new ChannelBuildReport(
                "succeeded", "pipeline", ChannelBuildExitCode.PipelineFailed,
                new ChannelBuildReportContext("dev", "Android", "1.2.3"),
                null, null, null, null, StartUtc, StartUtc));
            Assert.Throws<ArgumentException>(() => new ChannelBuildReport(
                "failed", "pipeline", ChannelBuildExitCode.InvalidInput,
                null, null, null, null, null, StartUtc, StartUtc));
            Assert.Throws<ArgumentException>(() => new ChannelBuildReport(
                "unknown", "none", ChannelBuildExitCode.Success,
                new ChannelBuildReportContext("dev", "Android", "1.2.3"),
                null, null, null, null, StartUtc, StartUtc));
            Assert.Throws<ArgumentException>(() => new ChannelBuildReport(
                "failed", null, (ChannelBuildExitCode)7,
                null, null, null, null, null, StartUtc, StartUtc));
        }

        [Test]
        public void Report_RejectsNonUtcOrReversedTiming()
        {
            var context = new ChannelBuildReportContext("dev", "Android", "1.2.3");
            Assert.Throws<ArgumentException>(() => new ChannelBuildReport(
                "succeeded", "none", ChannelBuildExitCode.Success, context,
                null, null, null, null, StartUtc.ToLocalTime(), StartUtc));
            Assert.Throws<ArgumentException>(() => new ChannelBuildReport(
                "succeeded", "none", ChannelBuildExitCode.Success, context,
                null, null, null, null, StartUtc.AddSeconds(1), StartUtc));
        }

        [Test]
        public void CaptureArtifact_UsesRelativeSlashPathSha256AndSize()
        {
            var outputRoot = Path.Combine(m_Root, "Build", "Channel");
            var artifactPath = Path.Combine(outputRoot, "Android", "manifest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath));
            System.IO.File.WriteAllText(artifactPath, "abc", new UTF8Encoding(false));

            var artifact = ChannelBuildReportWriter.CaptureArtifact(
                "resource-manifest",
                outputRoot,
                artifactPath);

            Assert.AreEqual("resource-manifest", artifact.Kind);
            Assert.AreEqual("Android/manifest.json", artifact.Path);
            Assert.AreEqual(
                "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
                artifact.Sha256);
            Assert.AreEqual(3, artifact.SizeBytes);
        }

        [Test]
        public void CaptureArtifact_RejectsEscapeMissingAndDirectory()
        {
            var outputRoot = Path.Combine(m_Root, "output");
            Directory.CreateDirectory(outputRoot);
            var outside = Path.Combine(m_Root, "outside.txt");
            System.IO.File.WriteAllText(outside, "outside");

            Assert.Throws<ArgumentException>(
                () => ChannelBuildReportWriter.CaptureArtifact("file", outputRoot, outside));
            Assert.Throws<FileNotFoundException>(
                () => ChannelBuildReportWriter.CaptureArtifact(
                    "file", outputRoot, Path.Combine(outputRoot, "missing.txt")));
            Assert.Throws<ArgumentException>(
                () => ChannelBuildReportWriter.CaptureArtifact("file", outputRoot, outputRoot));
        }

        [Test]
        public void Report_RejectsDuplicateOrNullCollectionEntries()
        {
            var outputRoot = Path.Combine(m_Root, "output");
            var path = Path.Combine(outputRoot, "file.bin");
            Directory.CreateDirectory(outputRoot);
            System.IO.File.WriteAllBytes(path, new byte[] { 1 });
            var artifact = ChannelBuildReportWriter.CaptureArtifact("file", outputRoot, path);
            var context = new ChannelBuildReportContext("dev", "Android", "1.2.3");

            Assert.Throws<ArgumentException>(() => new ChannelBuildReport(
                "succeeded", "none", ChannelBuildExitCode.Success, context, null,
                new[] { artifact, artifact }, null, null, StartUtc, StartUtc));
            Assert.Throws<ArgumentException>(() => new ChannelBuildReport(
                "succeeded", "none", ChannelBuildExitCode.Success, context, null,
                new ChannelBuildArtifact[] { null }, null, null, StartUtc, StartUtc));
            Assert.Throws<ArgumentException>(() => new ChannelBuildReport(
                "succeeded", "none", ChannelBuildExitCode.Success, context, null,
                null,
                new[]
                {
                    new ChannelBuildStepReport("prepare", "succeeded"),
                    new ChannelBuildStepReport("prepare", "failed")
                },
                null, StartUtc, StartUtc));
        }

        [Test]
        public void Write_EmitsCamelCaseSchemaAndAtomicOverwrite()
        {
            var path = Path.Combine(m_Root, "nested", "report.json");
            var ci = new CiBuildMetadata(
                "jenkins", "game/build", "42", "http://jenkins.local/42", "abc123");
            ChannelBuildReportWriter.Write(path, CreateSuccess(
                new ChannelBuildReportContext("dev", "Android", "1.2.3"), ci));
            ChannelBuildReportWriter.Write(
                path,
                CreateFailure(ChannelBuildExitCode.InvalidInput, "invalid-input"));

            var json = System.IO.File.ReadAllText(path);
            var root = JObject.Parse(json);
            Assert.AreEqual(1, (int)root["schemaVersion"]);
            Assert.AreEqual("failed", (string)root["status"]);
            Assert.AreEqual("invalid-input", (string)root["failureKind"]);
            Assert.AreEqual(2, (int)root["exitCode"]);
            Assert.AreEqual(JTokenType.Null, root["context"].Type);
            Assert.IsNotNull(root["artifacts"] as JArray);
            Assert.IsNotNull(root["steps"] as JArray);
            Assert.IsNotNull(root["warnings"] as JArray);
            Assert.AreEqual(0, Directory.GetFiles(Path.GetDirectoryName(path), "*.tmp").Length);
            var bytes = System.IO.File.ReadAllBytes(path);
            Assert.IsFalse(bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf);
        }

        [Test]
        public void Write_SuccessIncludesMinimalContextAndCamelCaseCi()
        {
            var path = Path.Combine(m_Root, "report.json");
            var ci = new CiBuildMetadata("jenkins", "game/build", "42", null, "abc123");
            ChannelBuildReportWriter.Write(path, CreateSuccess(
                new ChannelBuildReportContext("dev", "Android", "1.2.3"), ci));

            var json = System.IO.File.ReadAllText(path);
            var root = JObject.Parse(json);
            Assert.AreEqual("dev", (string)root["context"]["channel"]);
            Assert.AreEqual("Android", (string)root["context"]["platform"]);
            Assert.AreEqual("1.2.3", (string)root["context"]["version"]);
            Assert.AreEqual("jenkins", (string)root["ci"]["provider"]);
            Assert.AreEqual("42", (string)root["ci"]["buildId"]);
            Assert.AreEqual("abc123", (string)root["ci"]["revision"]);
            StringAssert.Contains("\"startedAtUtc\": \"2026-07-18T00:00:00.0000000Z\"", json);
        }

        [Test]
        public void Write_RejectsNullInputsAndCleansTempAfterFailure()
        {
            Assert.Throws<ArgumentException>(() => ChannelBuildReportWriter.Write(null, CreateFailure(
                ChannelBuildExitCode.InvalidInput, "invalid-input")));
            Assert.Throws<ArgumentNullException>(() => ChannelBuildReportWriter.Write(
                Path.Combine(m_Root, "report.json"), null));

            Assert.Throws<IOException>(() => ChannelBuildReportWriter.Write(
                m_Root,
                CreateFailure(ChannelBuildExitCode.InvalidInput, "invalid-input")));
            Assert.AreEqual(0, Directory.GetFiles(m_Root, "*.tmp").Length);
        }

        [Test]
        public void Command_RunWritesSuccessAndInvalidInputReports()
        {
            WriteDefaultCatalog();
            var reportPath = Path.Combine(m_Root, "Build", "report.json");
            var arguments = CreateCommandArguments(reportPath);

            Assert.AreEqual(ChannelBuildExitCode.Success, InvokeRun(arguments));
            Assert.AreEqual("succeeded", (string)JObject.Parse(System.IO.File.ReadAllText(reportPath))["status"]);

            Set(arguments, "-gdkEnvironment", "invalid");
            Assert.AreEqual(ChannelBuildExitCode.InvalidInput, InvokeRun(arguments));
            var failed = JObject.Parse(System.IO.File.ReadAllText(reportPath));
            Assert.AreEqual("failed", (string)failed["status"]);
            Assert.AreEqual("invalid-input", (string)failed["failureKind"]);
            Assert.AreEqual(JTokenType.Null, failed["context"].Type);
        }

        [Test]
        public void Command_MissingReportPathReturnsInvalidInputWithoutReport()
        {
            WriteDefaultCatalog();
            var reportPath = Path.Combine(m_Root, "report.json");
            var arguments = CreateCommandArguments(reportPath);
            Remove(arguments, "-gdkReportPath");

            Assert.AreEqual(ChannelBuildExitCode.InvalidInput, InvokeRun(arguments));
            Assert.IsFalse(System.IO.File.Exists(reportPath));
        }

        [Test]
        public void Command_ReportWriteFailureReturnsReportFailed()
        {
            WriteDefaultCatalog();
            var arguments = CreateCommandArguments(m_Root);

            Assert.AreEqual(ChannelBuildExitCode.ReportFailed, InvokeRun(arguments));
            Assert.AreEqual(0, Directory.GetFiles(m_Root, "*.tmp").Length);
        }

        [Test]
        public void Command_PlayerModeWritesBuildEvidenceAndExitCode()
        {
            WriteDefaultCatalog();
            var outputRoot = Path.Combine(m_Root, "Build");
            var artifactPath = Path.Combine(outputRoot, "player", "player.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath));
            System.IO.File.WriteAllText(artifactPath, "player");
            var reportPath = Path.Combine(outputRoot, "report.json");
            var arguments = CreateCommandArguments(reportPath);
            Set(arguments, "-gdkOutputRoot", outputRoot);
            arguments.Add("-gdkMode");
            arguments.Add("player");
            var artifact = ChannelBuildReportWriter.CaptureArtifact(
                "player-artifact", outputRoot, artifactPath);
            var result = new ChannelPlayerBuildResult(
                ChannelBuildExitCode.PlayerBuildFailed,
                Path.GetDirectoryName(artifactPath),
                artifacts: new[] { artifact },
                steps: new[] { new ChannelBuildStepReport("player-operation", "failed", "expected") },
                warnings: new[] { "warning" });

            var exitCode = InvokeRunWithPlayerBuild(arguments, context => result);

            Assert.AreEqual(ChannelBuildExitCode.PlayerBuildFailed, exitCode);
            var report = JObject.Parse(System.IO.File.ReadAllText(reportPath));
            Assert.AreEqual("player-build", (string)report["failureKind"]);
            Assert.AreEqual("player/player.apk", (string)report["artifacts"][0]["path"]);
            Assert.AreEqual("player-operation", (string)report["steps"][0]["id"]);
            Assert.AreEqual("warning", (string)report["warnings"][0]);
        }

        [Test]
        public void Command_ValidateModeDoesNotCallPlayerBuildAndRejectsUnknownMode()
        {
            WriteDefaultCatalog();
            var reportPath = Path.Combine(m_Root, "Build", "report.json");
            var arguments = CreateCommandArguments(reportPath);
            var calls = 0;

            Assert.AreEqual(
                ChannelBuildExitCode.Success,
                InvokeRunWithPlayerBuild(arguments, context => { calls++; return null; }));
            Assert.AreEqual(0, calls);

            arguments.Add("-gdkMode");
            arguments.Add("unknown");
            Assert.AreEqual(
                ChannelBuildExitCode.InvalidInput,
                InvokeRunWithPlayerBuild(arguments, context => { calls++; return null; }));
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void ReportJson_DoesNotContainProfileArgumentsExceptionOrSecret()
        {
            var path = Path.Combine(m_Root, "report.json");
            ChannelBuildReportWriter.Write(path, CreateSuccess(
                new ChannelBuildReportContext("dev", "Android", "1.2.3"), null));

            var json = System.IO.File.ReadAllText(path);
            StringAssert.DoesNotContain("profile", json.ToLowerInvariant());
            StringAssert.DoesNotContain("arguments", json.ToLowerInvariant());
            StringAssert.DoesNotContain("exception", json.ToLowerInvariant());
            StringAssert.DoesNotContain("secret", json.ToLowerInvariant());
        }

        private ChannelBuildExitCode InvokeRun(IReadOnlyList<string> arguments)
        {
            var method = typeof(ChannelBuildCommand).GetMethod(
                "Run",
                BindingFlags.Static | BindingFlags.NonPublic);
            var previous = Debug.unityLogger.logHandler;
            try
            {
                Debug.unityLogger.logHandler = new NullLogHandler();
                return (ChannelBuildExitCode)method.Invoke(null, new object[] { arguments, m_Root });
            }
            finally
            {
                Debug.unityLogger.logHandler = previous;
            }
        }

        private ChannelBuildExitCode InvokeRunWithPlayerBuild(
            IReadOnlyList<string> arguments,
            Func<ChannelBuildContext, ChannelPlayerBuildResult> playerBuild)
        {
            var previous = Debug.unityLogger.logHandler;
            try
            {
                Debug.unityLogger.logHandler = new NullLogHandler();
                return ChannelBuildCommand.RunWithPlayerBuild(arguments, m_Root, playerBuild);
            }
            finally
            {
                Debug.unityLogger.logHandler = previous;
            }
        }

        private static ChannelBuildReport CreateSuccess(
            ChannelBuildReportContext context,
            CiBuildMetadata ci,
            IReadOnlyList<ChannelBuildArtifact> artifacts = null,
            IReadOnlyList<ChannelBuildStepReport> steps = null,
            IReadOnlyList<string> warnings = null)
        {
            return new ChannelBuildReport(
                "succeeded", "none", ChannelBuildExitCode.Success, context, ci,
                artifacts, steps, warnings, StartUtc, StartUtc.AddSeconds(1));
        }

        private static ChannelBuildReport CreateFailure(
            ChannelBuildExitCode exitCode,
            string failureKind)
        {
            return new ChannelBuildReport(
                "failed", failureKind, exitCode, null, null,
                null, null, null, StartUtc, StartUtc.AddSeconds(1));
        }

        private void WriteDefaultCatalog()
        {
            var path = Path.Combine(
                m_Root,
                "ProjectSettings",
                "GameDeveloperKit",
                "channel-build-profiles.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path,
                "{\"schemaVersion\":1,\"profiles\":[{" +
                "\"id\":\"android-dev\",\"channel\":\"base\"}]}");
        }

        private static List<string> CreateCommandArguments(string reportPath)
        {
            return new List<string>
            {
                "-gdkChannel", "dev",
                "-gdkEnvironment", "dev",
                "-gdkBuildTarget", "Android",
                "-gdkVersion", "1.2.3",
                "-gdkPlayerBuildNumber", "42",
                "-gdkProfile", "android-dev",
                "-gdkOutputRoot", "Build/Channel",
                "-gdkReportPath", reportPath
            };
        }

        private static void Set(List<string> arguments, string name, string value)
        {
            var index = arguments.IndexOf(name);
            arguments[index + 1] = value;
        }

        private static void Remove(List<string> arguments, string name)
        {
            var index = arguments.IndexOf(name);
            arguments.RemoveRange(index, 2);
        }

        private sealed class NullLogHandler : ILogHandler
        {
            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
            }

            public void LogException(Exception exception, UnityEngine.Object context)
            {
            }
        }
    }
}
