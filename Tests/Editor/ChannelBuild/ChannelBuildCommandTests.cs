using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GameDeveloperKit.ChannelBuild;
using NUnit.Framework;
using UnityEditor;

namespace GameDeveloperKit.Tests
{
    [TestFixture]
    public sealed class ChannelBuildCommandTests
    {
        private string m_ProjectRoot;

        [SetUp]
        public void SetUp()
        {
            m_ProjectRoot = Path.Combine(
                Path.GetTempPath(),
                "gdk-channel-command-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_ProjectRoot);
            WriteCatalog(
                ChannelProfileSource.DefaultRelativePath,
                "{\"schemaVersion\":1,\"profiles\":[{" +
                "\"id\":\"android-dev\",\"channel\":\"base\"," +
                "\"productName\":\"Base Product\",\"defines\":[\"BASE\"]}]}");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_ProjectRoot))
            {
                Directory.Delete(m_ProjectRoot, true);
            }
        }

        [Test]
        public void ExitCodes_HaveFixedProtocolValues()
        {
            Assert.AreEqual(0, (int)ChannelBuildExitCode.Success);
            Assert.AreEqual(2, (int)ChannelBuildExitCode.InvalidInput);
            Assert.AreEqual(3, (int)ChannelBuildExitCode.PipelineFailed);
            Assert.AreEqual(4, (int)ChannelBuildExitCode.ResourceBuildFailed);
            Assert.AreEqual(5, (int)ChannelBuildExitCode.PlayerBuildFailed);
            Assert.AreEqual(6, (int)ChannelBuildExitCode.ReportFailed);
        }

        [Test]
        public void CreateContext_MinimumArguments_LoadsAndMergesDefaultProfile()
        {
            var context = ChannelBuildCommand.CreateContext(CreateMinimumArguments(), m_ProjectRoot);

            Assert.AreEqual("dev", context.Channel);
            Assert.AreEqual(ChannelBuildEnvironment.Dev, context.Environment);
            Assert.AreEqual(BuildTarget.Android, context.BuildTarget);
            Assert.AreEqual("Android", context.Platform);
            Assert.AreEqual("1.2.3", context.Version);
            Assert.AreEqual(42, context.PlayerBuildNumber);
            Assert.AreEqual("Build/Channel", context.OutputRoot);
            Assert.AreEqual("android-dev", context.Profile.Id);
            Assert.AreEqual("dev", context.Profile.Channel);
            Assert.AreEqual("Base Product", context.Profile.ProductName);
            Assert.IsNull(context.Ci);
        }

        [Test]
        public void CreateContext_ExplicitCatalogAndOptionalValues_ArePreserved()
        {
            const string catalogPath = "Config/profiles.json";
            WriteCatalog(
                catalogPath,
                "{\"schemaVersion\":1,\"profiles\":[{" +
                "\"id\":\"explicit\",\"channel\":\"base\"}]}");
            var arguments = CreateMinimumArguments();
            Set(arguments, "-gdkProfile", "explicit");
            Set(arguments, "-gdkProfileCatalog", catalogPath);
            Set(arguments, "-gdkFlavor", "official");
            Set(arguments, "-gdkRemoteRoot", "https://cdn.example.com");
            Set(arguments, "-gdkMinimumClientBuild", "100");
            Set(arguments, "-gdkMaximumClientBuild", "199");

            var context = ChannelBuildCommand.CreateContext(arguments, m_ProjectRoot);

            Assert.AreEqual("explicit", context.Profile.Id);
            Assert.AreEqual("official", context.Flavor);
            Assert.AreEqual("https://cdn.example.com", context.RemoteRoot);
            Assert.AreEqual(100, context.MinimumClientBuild);
            Assert.AreEqual(199, context.MaximumClientBuild);
        }

        [Test]
        public void CreateContext_CompleteCiArguments_CreateMetadata()
        {
            var arguments = CreateMinimumArguments();
            Set(arguments, "-gdkCiProvider", "jenkins");
            Set(arguments, "-gdkCiJobName", "game/channel-build");
            Set(arguments, "-gdkCiBuildId", "42");
            Set(arguments, "-gdkCiBuildUrl", "http://jenkins.local/job/game/42/");
            Set(arguments, "-gdkCiRevision", "abc123");

            var context = ChannelBuildCommand.CreateContext(arguments, m_ProjectRoot);

            Assert.AreEqual("jenkins", context.Ci.Provider);
            Assert.AreEqual("game/channel-build", context.Ci.JobName);
            Assert.AreEqual("42", context.Ci.BuildId);
            Assert.AreEqual("http://jenkins.local/job/game/42/", context.Ci.BuildUrl);
            Assert.AreEqual("abc123", context.Ci.Revision);
        }

        [Test]
        public void CreateContext_IgnoresUnityAndOtherNonGdkArguments()
        {
            var arguments = CreateMinimumArguments();
            arguments.InsertRange(0, new[]
            {
                "Unity.exe",
                "-batchmode",
                "-projectPath",
                m_ProjectRoot,
                "-executeMethod",
                "GameDeveloperKit.ChannelBuildCommand.Build"
            });

            var context = ChannelBuildCommand.CreateContext(arguments, m_ProjectRoot);

            Assert.AreEqual("dev", context.Channel);
        }

        [TestCase("-gdkUnknown", "must-not-appear")]
        [TestCase("-gdkchannel", "must-not-appear")]
        public void CreateContext_UnknownOrWrongCaseGdkArgument_RejectsWithoutValue(
            string name,
            string value)
        {
            var arguments = CreateMinimumArguments();
            arguments.Add(name);
            arguments.Add(value);

            var exception = Assert.Throws<ArgumentException>(
                () => ChannelBuildCommand.CreateContext(arguments, m_ProjectRoot));

            StringAssert.Contains(name, exception.Message);
            StringAssert.DoesNotContain(value, exception.ToString());
        }

        [Test]
        public void CreateContext_DuplicateArgument_RejectsWithoutValues()
        {
            var arguments = CreateMinimumArguments();
            arguments.Add("-gdkChannel");
            arguments.Add("must-not-appear");

            var exception = Assert.Throws<ArgumentException>(
                () => ChannelBuildCommand.CreateContext(arguments, m_ProjectRoot));

            StringAssert.DoesNotContain("must-not-appear", exception.ToString());
        }

        [Test]
        public void CreateContext_MissingArgumentValue_ThrowsArgumentException()
        {
            var arguments = CreateMinimumArguments();
            arguments.Add("-gdkFlavor");

            Assert.Throws<ArgumentException>(
                () => ChannelBuildCommand.CreateContext(arguments, m_ProjectRoot));
        }

        [TestCase("-gdkChannel")]
        [TestCase("-gdkEnvironment")]
        [TestCase("-gdkBuildTarget")]
        [TestCase("-gdkVersion")]
        [TestCase("-gdkPlayerBuildNumber")]
        [TestCase("-gdkProfile")]
        [TestCase("-gdkOutputRoot")]
        public void CreateContext_MissingRequiredArgument_ThrowsArgumentException(string name)
        {
            var arguments = CreateMinimumArguments();
            Remove(arguments, name);

            Assert.Throws<ArgumentException>(
                () => ChannelBuildCommand.CreateContext(arguments, m_ProjectRoot));
        }

        [TestCase("-gdkEnvironment", "Dev")]
        [TestCase("-gdkEnvironment", "unknown")]
        [TestCase("-gdkBuildTarget", "android")]
        [TestCase("-gdkBuildTarget", "NoTarget")]
        [TestCase("-gdkPlayerBuildNumber", "0")]
        [TestCase("-gdkPlayerBuildNumber", "-1")]
        [TestCase("-gdkPlayerBuildNumber", "+1")]
        [TestCase("-gdkPlayerBuildNumber", "1.0")]
        public void CreateContext_InvalidTypedValue_ThrowsWithoutEchoingValue(string name, string value)
        {
            var arguments = CreateMinimumArguments();
            Set(arguments, name, value);

            var exception = Assert.Throws<ArgumentException>(
                () => ChannelBuildCommand.CreateContext(arguments, m_ProjectRoot));

            StringAssert.Contains(name, exception.Message);
            StringAssert.DoesNotContain(value, exception.Message);
        }

        [Test]
        public void CreateContext_NumericBuildTarget_ThrowsArgumentException()
        {
            var arguments = CreateMinimumArguments();
            Set(arguments, "-gdkBuildTarget", ((int)BuildTarget.Android).ToString());

            Assert.Throws<ArgumentException>(
                () => ChannelBuildCommand.CreateContext(arguments, m_ProjectRoot));
        }

        [Test]
        public void CreateContext_InvalidClientRange_ThrowsArgumentException()
        {
            var arguments = CreateMinimumArguments();
            Set(arguments, "-gdkMinimumClientBuild", "200");
            Set(arguments, "-gdkMaximumClientBuild", "199");

            Assert.Throws<ArgumentOutOfRangeException>(
                () => ChannelBuildCommand.CreateContext(arguments, m_ProjectRoot));
        }

        [TestCase("-gdkCiProvider")]
        [TestCase("-gdkCiJobName")]
        [TestCase("-gdkCiBuildId")]
        [TestCase("-gdkCiBuildUrl")]
        [TestCase("-gdkCiRevision")]
        public void CreateContext_PartialCiArguments_RequireCompleteIdentity(string name)
        {
            var arguments = CreateMinimumArguments();
            Set(arguments, name, name == "-gdkCiBuildUrl" ? "https://jenkins.example.com" : "value");

            Assert.Throws<ArgumentException>(
                () => ChannelBuildCommand.CreateContext(arguments, m_ProjectRoot));
        }

        [Test]
        public void CreateContext_MissingProfileFile_PreservesFileNotFoundException()
        {
            var arguments = CreateMinimumArguments();
            Set(arguments, "-gdkProfileCatalog", "Config/missing.json");

            Assert.Throws<FileNotFoundException>(
                () => ChannelBuildCommand.CreateContext(arguments, m_ProjectRoot));
        }

        [Test]
        public void CreateContext_InvalidProfileJson_ThrowsGameException()
        {
            WriteCatalog(ChannelProfileSource.DefaultRelativePath, "{not-json}");

            Assert.Throws<GameException>(
                () => ChannelBuildCommand.CreateContext(CreateMinimumArguments(), m_ProjectRoot));
        }

        [Test]
        public void CreateContext_MissingProfileId_ThrowsKeyNotFoundException()
        {
            var arguments = CreateMinimumArguments();
            Set(arguments, "-gdkProfile", "missing");

            Assert.Throws<KeyNotFoundException>(
                () => ChannelBuildCommand.CreateContext(arguments, m_ProjectRoot));
        }

        [Test]
        public void BatchModeGuard_RejectsInteractiveInvocation()
        {
            var method = typeof(ChannelBuildCommand).GetMethod(
                "EnsureBatchMode",
                BindingFlags.Static | BindingFlags.NonPublic);

            var exception = Assert.Throws<TargetInvocationException>(
                () => method.Invoke(null, new object[] { false }));

            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
        }

        private static List<string> CreateMinimumArguments()
        {
            return new List<string>
            {
                "-gdkChannel", "dev",
                "-gdkEnvironment", "dev",
                "-gdkBuildTarget", "Android",
                "-gdkVersion", "1.2.3",
                "-gdkPlayerBuildNumber", "42",
                "-gdkProfile", "android-dev",
                "-gdkOutputRoot", "Build/Channel"
            };
        }

        private static void Set(List<string> arguments, string name, string value)
        {
            for (var i = 0; i < arguments.Count; i += 2)
            {
                if (arguments[i] == name)
                {
                    arguments[i + 1] = value;
                    return;
                }
            }

            arguments.Add(name);
            arguments.Add(value);
        }

        private static void Remove(List<string> arguments, string name)
        {
            var index = arguments.IndexOf(name);
            arguments.RemoveRange(index, 2);
        }

        private void WriteCatalog(string relativePath, string json)
        {
            var path = Path.Combine(m_ProjectRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, json);
        }
    }
}
