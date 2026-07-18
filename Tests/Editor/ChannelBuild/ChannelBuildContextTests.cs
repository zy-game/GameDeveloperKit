using System;
using System.Collections.Generic;
using GameDeveloperKit.ChannelBuild;
using NUnit.Framework;
using UnityEditor;

namespace GameDeveloperKit.Tests
{
    public sealed class ChannelBuildContextTests
    {
        [Test]
        public void Constructor_PreservesValidFullContextAndDerivesPlatform()
        {
            var profile = new ChannelProfile("dev-profile", "dev", productName: "Black Rain Dev");
            var ci = new CiBuildMetadata(
                "jenkins",
                "game/channel-build",
                "42",
                "http://jenkins.local/job/game/42/",
                "abc123");
            var arguments = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["region"] = "ap-shanghai"
            };

            var context = new ChannelBuildContext(
                "dev",
                ChannelBuildEnvironment.Dev,
                BuildTarget.Android,
                "1.2.3",
                42,
                "Build/Channel",
                "official",
                "https://cdn.example.com",
                100,
                199,
                profile,
                arguments,
                ci);

            Assert.AreEqual("dev", context.Channel);
            Assert.AreEqual(ChannelBuildEnvironment.Dev, context.Environment);
            Assert.AreEqual("official", context.Flavor);
            Assert.AreEqual(BuildTarget.Android, context.BuildTarget);
            Assert.AreEqual("Android", context.Platform);
            Assert.AreEqual("1.2.3", context.Version);
            Assert.AreEqual(42, context.PlayerBuildNumber);
            Assert.AreEqual("Build/Channel", context.OutputRoot);
            Assert.AreEqual("https://cdn.example.com", context.RemoteRoot);
            Assert.AreEqual(100, context.MinimumClientBuild);
            Assert.AreEqual(199, context.MaximumClientBuild);
            Assert.AreSame(profile, context.Profile);
            Assert.AreSame(ci, context.Ci);
            Assert.AreEqual("ap-shanghai", context.Arguments["region"]);
        }

        [Test]
        public void Constructor_AllowsLocalContextWithoutOptionalValues()
        {
            var context = CreateContext();

            Assert.IsNull(context.Flavor);
            Assert.IsNull(context.RemoteRoot);
            Assert.IsNull(context.MinimumClientBuild);
            Assert.IsNull(context.MaximumClientBuild);
            Assert.IsNull(context.Profile);
            Assert.IsNull(context.Ci);
            Assert.IsEmpty(context.Arguments);
        }

        [Test]
        public void Constructor_DefensivelyCopiesProfileAndContextCollections()
        {
            var defines = new List<string> { "CHANNEL_DEV" };
            var config = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["apiUrl"] = "https://api.example.com"
            };
            var arguments = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["region"] = "ap-shanghai"
            };
            var profile = new ChannelProfile(
                "dev-profile",
                "dev",
                defines: defines,
                configOverrides: config);
            var context = CreateContext(profile: profile, arguments: arguments);

            defines[0] = "MUTATED";
            config["apiUrl"] = "https://mutated.invalid";
            arguments["region"] = "mutated";

            Assert.AreEqual("CHANNEL_DEV", profile.Defines[0]);
            Assert.AreEqual("https://api.example.com", profile.ConfigOverrides["apiUrl"]);
            Assert.AreEqual("ap-shanghai", context.Arguments["region"]);
            Assert.Throws<NotSupportedException>(
                () => ((IList<string>)profile.Defines)[0] = "MUTATED_AGAIN");
            Assert.Throws<NotSupportedException>(
                () => ((IDictionary<string, string>)context.Arguments)["region"] = "mutated-again");
        }

        [TestCase(ChannelBuildEnvironment.Dev)]
        [TestCase(ChannelBuildEnvironment.Test)]
        [TestCase(ChannelBuildEnvironment.Staging)]
        [TestCase(ChannelBuildEnvironment.Prod)]
        public void Constructor_AcceptsDefinedEnvironments(ChannelBuildEnvironment environment)
        {
            Assert.AreEqual(environment, CreateContext(environment: environment).Environment);
        }

        [TestCase("http://jenkins.local/job/game/42/")]
        [TestCase("https://jenkins.example.com/job/game/42/")]
        public void CiMetadata_AcceptsAbsoluteHttpAndHttpsBuildUrls(string buildUrl)
        {
            var metadata = new CiBuildMetadata("jenkins", "game/build", "42", buildUrl, "abc123");

            Assert.AreEqual(buildUrl, metadata.BuildUrl);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("dev channel")]
        [TestCase("dev/test")]
        [TestCase("bad\nchannel")]
        [TestCase(".")]
        [TestCase("..")]
        public void Constructor_RejectsInvalidChannel(string channel)
        {
            var exception = Assert.Throws<ArgumentException>(() => CreateContext(channel: channel));

            Assert.AreEqual("channel", exception.ParamName);
        }

        [TestCase("")]
        [TestCase("1.2.3 beta")]
        [TestCase("1/2/3")]
        [TestCase("bad\rversion")]
        public void Constructor_RejectsInvalidVersion(string version)
        {
            var exception = Assert.Throws<ArgumentException>(() => CreateContext(version: version));

            Assert.AreEqual("version", exception.ParamName);
        }

        [Test]
        public void Constructor_RejectsNoTargetAndUndefinedTarget()
        {
            Assert.AreEqual(
                "buildTarget",
                Assert.Throws<ArgumentException>(() => CreateContext(buildTarget: BuildTarget.NoTarget)).ParamName);
            Assert.AreEqual(
                "buildTarget",
                Assert.Throws<ArgumentException>(() => CreateContext(buildTarget: (BuildTarget)int.MaxValue)).ParamName);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_RejectsNonPositivePlayerBuildNumber(int playerBuildNumber)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateContext(playerBuildNumber: playerBuildNumber));

            Assert.AreEqual("playerBuildNumber", exception.ParamName);
        }

        [Test]
        public void Constructor_AllowsEmptyRemoteRootAndRejectsNonHttpsRemoteRoot()
        {
            Assert.IsNull(CreateContext(remoteRoot: null).RemoteRoot);
            Assert.IsNull(CreateContext(remoteRoot: string.Empty).RemoteRoot);
            Assert.AreEqual(
                "remoteRoot",
                Assert.Throws<ArgumentException>(() => CreateContext(remoteRoot: "relative/path")).ParamName);
            Assert.AreEqual(
                "remoteRoot",
                Assert.Throws<ArgumentException>(() => CreateContext(remoteRoot: "http://cdn.example.com")).ParamName);
            Assert.AreEqual(
                "remoteRoot",
                Assert.Throws<ArgumentException>(
                    () => CreateContext(remoteRoot: "https://user:password@cdn.example.com")).ParamName);
            Assert.AreEqual(
                "remoteRoot",
                Assert.Throws<ArgumentException>(
                    () => CreateContext(remoteRoot: "https://cdn.example.com?token=value")).ParamName);
        }

        [Test]
        public void Constructor_RejectsInvalidClientBuildRanges()
        {
            Assert.AreEqual(
                "maximumClientBuild",
                Assert.Throws<ArgumentException>(
                    () => CreateContext(minimumClientBuild: 100, maximumClientBuild: null)).ParamName);
            Assert.AreEqual(
                "minimumClientBuild",
                Assert.Throws<ArgumentException>(
                    () => CreateContext(minimumClientBuild: null, maximumClientBuild: 199)).ParamName);
            Assert.AreEqual(
                "minimumClientBuild",
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => CreateContext(minimumClientBuild: 0, maximumClientBuild: 199)).ParamName);
            Assert.AreEqual(
                "maximumClientBuild",
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => CreateContext(minimumClientBuild: 200, maximumClientBuild: 199)).ParamName);
        }

        [TestCase("secretKey")]
        [TestCase("dbPassword")]
        [TestCase("accessToken")]
        [TestCase("privateKeyPath")]
        [TestCase("private_key_path")]
        [TestCase("credentialId")]
        [TestCase("storageAccessKey")]
        [TestCase("signing-key")]
        public void Constructor_RejectsSensitiveArgumentKeysWithoutLeakingValues(string key)
        {
            const string sensitiveValue = "must-not-appear";
            var arguments = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [key] = sensitiveValue
            };

            var exception = Assert.Throws<ArgumentException>(() => CreateContext(arguments: arguments));

            Assert.AreEqual("arguments", exception.ParamName);
            StringAssert.Contains(key, exception.Message);
            StringAssert.DoesNotContain(sensitiveValue, exception.Message);
        }

        [Test]
        public void Constructor_RejectsNullDictionaryValuesButAllowsEmptyValues()
        {
            var nullArguments = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["region"] = null
            };
            var emptyArguments = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["region"] = string.Empty
            };

            var exception = Assert.Throws<ArgumentException>(
                () => CreateContext(arguments: nullArguments));

            Assert.AreEqual("arguments", exception.ParamName);
            Assert.AreEqual(string.Empty, CreateContext(arguments: emptyArguments).Arguments["region"]);
        }

        [Test]
        public void Profile_RejectsSensitiveOverrideKeysWithoutLeakingValues()
        {
            const string sensitiveValue = "must-not-appear";
            var overrides = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["apiToken"] = sensitiveValue
            };

            var exception = Assert.Throws<ArgumentException>(
                () => new ChannelProfile("dev-profile", "dev", configOverrides: overrides));

            Assert.AreEqual("configOverrides", exception.ParamName);
            StringAssert.DoesNotContain(sensitiveValue, exception.Message);
        }

        [Test]
        public void CiMetadata_RejectsLineBreaksAndInvalidBuildUrl()
        {
            Assert.AreEqual(
                "jobName",
                Assert.Throws<ArgumentException>(
                    () => new CiBuildMetadata("jenkins", "game\nbuild", "42", null, "abc123")).ParamName);
            Assert.AreEqual(
                "buildUrl",
                Assert.Throws<ArgumentException>(
                    () => new CiBuildMetadata("jenkins", "game/build", "42", "jenkins/job/42", "abc123")).ParamName);
            Assert.AreEqual(
                "buildUrl",
                Assert.Throws<ArgumentException>(
                    () => new CiBuildMetadata("jenkins", "game/build", "42", "ftp://jenkins/job/42", "abc123")).ParamName);
        }

        [Test]
        public void Constructor_RejectsProfileFromAnotherChannel()
        {
            var profile = new ChannelProfile("prod-profile", "prod");

            var exception = Assert.Throws<ArgumentException>(() => CreateContext(profile: profile));

            Assert.AreEqual("profile", exception.ParamName);
        }

        [Test]
        public void Constructor_RejectsInvalidOutputRootAndUndefinedEnvironment()
        {
            Assert.AreEqual(
                "outputRoot",
                Assert.Throws<ArgumentException>(() => CreateContext(outputRoot: string.Empty)).ParamName);
            Assert.AreEqual(
                "outputRoot",
                Assert.Throws<ArgumentException>(() => CreateContext(outputRoot: "Build\nChannel")).ParamName);
            Assert.AreEqual(
                "environment",
                Assert.Throws<ArgumentException>(
                    () => CreateContext(environment: (ChannelBuildEnvironment)int.MaxValue)).ParamName);
        }

        private static ChannelBuildContext CreateContext(
            string channel = "dev",
            ChannelBuildEnvironment environment = ChannelBuildEnvironment.Dev,
            BuildTarget buildTarget = BuildTarget.Android,
            string version = "1.2.3",
            int playerBuildNumber = 1,
            string outputRoot = "Build/Channel",
            string remoteRoot = null,
            long? minimumClientBuild = null,
            long? maximumClientBuild = null,
            ChannelProfile profile = null,
            IReadOnlyDictionary<string, string> arguments = null)
        {
            return new ChannelBuildContext(
                channel,
                environment,
                buildTarget,
                version,
                playerBuildNumber,
                outputRoot,
                remoteRoot: remoteRoot,
                minimumClientBuild: minimumClientBuild,
                maximumClientBuild: maximumClientBuild,
                profile: profile,
                arguments: arguments);
        }
    }
}
