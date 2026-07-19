using System;
using System.Collections.Generic;
using GameDeveloperKit.ChannelBuild;
using NUnit.Framework;
using UnityEditor;

namespace GameDeveloperKit.Tests
{
    public sealed class ChannelBuildPlatformResponderTests
    {
        [Test]
        public void Runner_AppliesAndroidOptionsAndSigningThenRestores()
        {
            var previous = AndroidState(7, false, AndroidArchitecture.ARMv7, "old-key", "old-pass");
            var gateway = new FakeGateway(previous) { ExistingFile = true };
            var profile = Profile(new Dictionary<string, string>
            {
                ["android.buildAppBundle"] = "true",
                ["android.targetArchitectures"] = "ARM64,X86_64"
            });
            var context = Context(BuildTarget.Android, profile, 42);
            var secret = "secret-value-never-output";
            var signing = ChannelBuildSigningInput.Android(
                "C:\\ci\\signing.keystore",
                secret,
                "release",
                secret);

            var execution = ChannelBuildResponderRunner.Execute(
                context,
                Responders(new ChannelBuildPlatformResponder(signing, gateway)),
                operationContext =>
                {
                    Assert.AreEqual(42, gateway.Current.AndroidBundleVersionCode);
                    Assert.IsTrue(gateway.Current.AndroidBuildAppBundle);
                    Assert.AreEqual(
                        AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64,
                        gateway.Current.AndroidTargetArchitectures);
                    Assert.AreEqual("C:\\ci\\signing.keystore", gateway.Current.AndroidKeystoreName);
                    Assert.AreEqual(secret, gateway.Current.AndroidKeystorePassword);
                    Assert.AreEqual("release", gateway.Current.AndroidKeyAlias);
                    Assert.AreEqual(secret, gateway.Current.AndroidKeyAliasPassword);
                    return Step(ChannelBuildResponderPhase.Operation, false, "expected failure");
                });

            Assert.IsFalse(execution.Success);
            Assert.AreSame(previous, gateway.Current);
            Assert.AreEqual("Android", signing.ToString());
            foreach (var result in execution.Results)
            {
                StringAssert.DoesNotContain(secret, result.Message ?? string.Empty);
                foreach (var output in result.Outputs)
                {
                    StringAssert.DoesNotContain(secret, output.Key);
                    StringAssert.DoesNotContain(secret, output.Value);
                }
            }
        }

        [Test]
        public void Runner_AppliesIosAutomaticAndManualSigningThenRestores()
        {
            AssertIosSigning(
                ChannelBuildSigningInput.IosAutomatic("TEAM-A"),
                true,
                string.Empty,
                ProvisioningProfileType.Automatic);
            AssertIosSigning(
                ChannelBuildSigningInput.IosManual(
                    "TEAM-M",
                    "PROFILE-M",
                    ProvisioningProfileType.Distribution),
                false,
                "PROFILE-M",
                ProvisioningProfileType.Distribution);
        }

        [Test]
        public void EmptyOptionsAndSigning_OnlyChangeBuildNumber()
        {
            var androidPrevious = AndroidState(3, true, AndroidArchitecture.ARMv7, "old-key", "old-pass");
            var androidGateway = new FakeGateway(androidPrevious);
            var android = new ChannelBuildPlatformResponder(null, androidGateway);
            var androidContext = Context(BuildTarget.Android, Profile(), 11);
            Assert.IsTrue(android.Prepare(androidContext).Success);
            Assert.IsTrue(android.Apply(androidContext).Success);
            Assert.AreEqual(11, androidGateway.Current.AndroidBundleVersionCode);
            Assert.IsTrue(androidGateway.Current.AndroidBuildAppBundle);
            Assert.AreEqual("old-key", androidGateway.Current.AndroidKeystoreName);
            Assert.IsTrue(android.Restore(androidContext).Success);
            Assert.AreSame(androidPrevious, androidGateway.Current);

            var iosPrevious = IosState("3", iOSSdkVersion.DeviceSDK, "TEAM", false, "PROFILE");
            var iosGateway = new FakeGateway(iosPrevious);
            var ios = new ChannelBuildPlatformResponder(null, iosGateway);
            var iosContext = Context(BuildTarget.iOS, Profile(), 12);
            Assert.IsTrue(ios.Prepare(iosContext).Success);
            Assert.IsTrue(ios.Apply(iosContext).Success);
            Assert.AreEqual("12", iosGateway.Current.IosBuildNumber);
            Assert.AreEqual("TEAM", iosGateway.Current.IosAppleTeamId);
            Assert.AreEqual("PROFILE", iosGateway.Current.IosProvisioningProfileId);
        }

        [Test]
        public void Prepare_RejectsUnsupportedUnknownCrossPlatformAndInvalidValues()
        {
            AssertPrepareFailure(BuildTarget.StandaloneWindows64, Profile());
            AssertPrepareFailure(BuildTarget.Android, Profile(Options("android.unknown", "true")));
            AssertPrepareFailure(BuildTarget.Android, Profile(Options("ios.sdkVersion", "DeviceSDK")));
            AssertPrepareFailure(BuildTarget.Android, Profile(Options("android.buildAppBundle", "yes")));
            AssertPrepareFailure(BuildTarget.Android, Profile(Options("android.targetArchitectures", "7")));
            AssertPrepareFailure(BuildTarget.iOS, Profile(Options("ios.sdkVersion", "1")));
        }

        [Test]
        public void Prepare_RejectsSigningTargetMismatchAndInvalidKeystoreLocator()
        {
            var iosSigning = ChannelBuildSigningInput.IosAutomatic("TEAM");
            AssertPrepareFailure(BuildTarget.Android, Profile(), iosSigning, true);

            var androidSigning = ChannelBuildSigningInput.Android(
                "C:\\ci\\missing.keystore",
                "store-pass",
                "release",
                "alias-pass");
            AssertPrepareFailure(BuildTarget.iOS, Profile(), androidSigning, true);
            AssertPrepareFailure(BuildTarget.Android, Profile(), androidSigning, false);
        }

        [Test]
        public void SigningFactories_RejectIncompleteOrInvalidInputs()
        {
            Assert.Throws<ArgumentException>(() =>
                ChannelBuildSigningInput.Android("", "password", "alias", "password"));
            Assert.Throws<ArgumentException>(() => ChannelBuildSigningInput.IosAutomatic("\n"));
            Assert.Throws<ArgumentException>(() => ChannelBuildSigningInput.IosManual(
                "TEAM", "PROFILE", ProvisioningProfileType.Automatic));
        }

        [Test]
        public void Runner_ApplyExceptionStillRestoresAndResponderRejectsReuse()
        {
            var previous = AndroidState(3, false, AndroidArchitecture.ARMv7, "old-key", "old-pass");
            var gateway = new FakeGateway(previous) { ThrowAfterApply = true };
            var context = Context(BuildTarget.Android, Profile(), 10);
            var responder = new ChannelBuildPlatformResponder(null, gateway);

            Assert.Throws<GameException>(() => ChannelBuildResponderRunner.Execute(
                context,
                Responders(responder),
                operationContext => Step(ChannelBuildResponderPhase.Operation)));
            Assert.AreSame(previous, gateway.Current);
            Assert.AreEqual(2, gateway.ApplyCount);

            var second = new ChannelBuildPlatformResponder(null, new FakeGateway(previous));
            Assert.IsTrue(second.Prepare(context).Success);
            Assert.Throws<GameException>(() => second.Prepare(context));
            Assert.Throws<GameException>(() => second.Apply(Context(BuildTarget.Android, Profile(), 10)));
        }

        private static void AssertIosSigning(
            ChannelBuildSigningInput signing,
            bool automatic,
            string profileId,
            ProvisioningProfileType profileType)
        {
            var previous = IosState("4", iOSSdkVersion.DeviceSDK, "OLD", false, "OLD-PROFILE");
            var gateway = new FakeGateway(previous);
            var context = Context(
                BuildTarget.iOS,
                Profile(Options("ios.sdkVersion", "SimulatorSDK")),
                42);

            var execution = ChannelBuildResponderRunner.Execute(
                context,
                Responders(new ChannelBuildPlatformResponder(signing, gateway)),
                operationContext =>
                {
                    Assert.AreEqual("42", gateway.Current.IosBuildNumber);
                    Assert.AreEqual(iOSSdkVersion.SimulatorSDK, gateway.Current.IosSdkVersion);
                    Assert.AreEqual(automatic, gateway.Current.IosAutomaticSigning);
                    Assert.AreEqual(profileId, gateway.Current.IosProvisioningProfileId);
                    Assert.AreEqual(profileType, gateway.Current.IosProvisioningProfileType);
                    return Step(ChannelBuildResponderPhase.Operation);
                });

            Assert.IsTrue(execution.Success);
            Assert.AreSame(previous, gateway.Current);
        }

        private static void AssertPrepareFailure(
            BuildTarget target,
            ChannelProfile profile,
            ChannelBuildSigningInput signing = null,
            bool existingFile = false)
        {
            var state = target == BuildTarget.iOS
                ? IosState("1", iOSSdkVersion.DeviceSDK, "OLD", false, "OLD-PROFILE")
                : AndroidState(1, false, AndroidArchitecture.ARMv7, "old-key", "old-pass");
            var gateway = new FakeGateway(state) { ExistingFile = existingFile };
            var result = new ChannelBuildPlatformResponder(signing, gateway)
                .Prepare(Context(target, profile, 2));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, gateway.ApplyCount);
        }

        private static IReadOnlyDictionary<string, string> Options(string key, string value)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal) { [key] = value };
        }

        private static ChannelProfile Profile(IReadOnlyDictionary<string, string> options = null)
        {
            return new ChannelProfile("dev-profile", "dev", platformOptions: options);
        }

        private static ChannelBuildContext Context(
            BuildTarget target,
            ChannelProfile profile,
            int buildNumber)
        {
            return new ChannelBuildContext(
                "dev",
                ChannelBuildEnvironment.Dev,
                target,
                "1.2.3",
                buildNumber,
                "Build/Channel",
                profile: profile);
        }

        private static IReadOnlyList<IChannelBuildResponder> Responders(
            ChannelBuildPlatformResponder responder)
        {
            return new IChannelBuildResponder[]
            {
                new SucceededDependency(ChannelBuildBrandingResponder.ResponderId),
                responder
            };
        }

        private static ChannelBuildStepResult Step(
            ChannelBuildResponderPhase phase,
            bool success = true,
            string message = null)
        {
            return new ChannelBuildStepResult("operation", phase, success, message);
        }

        private static ChannelBuildPlatformResponder.PlatformState AndroidState(
            int buildNumber,
            bool appBundle,
            AndroidArchitecture architectures,
            string keystore,
            string password)
        {
            return ChannelBuildPlatformResponder.PlatformState.Android(
                buildNumber,
                appBundle,
                architectures,
                keystore,
                password,
                "old-alias",
                password);
        }

        private static ChannelBuildPlatformResponder.PlatformState IosState(
            string buildNumber,
            iOSSdkVersion sdk,
            string team,
            bool automatic,
            string profile)
        {
            return ChannelBuildPlatformResponder.PlatformState.Ios(
                buildNumber,
                sdk,
                team,
                automatic,
                profile,
                automatic ? ProvisioningProfileType.Automatic : ProvisioningProfileType.Development);
        }

        private sealed class FakeGateway : ChannelBuildPlatformResponder.IPlatformGateway
        {
            internal FakeGateway(ChannelBuildPlatformResponder.PlatformState current)
            {
                Current = current;
            }

            internal bool ExistingFile { get; set; }
            internal bool ThrowAfterApply { get; set; }
            internal int ApplyCount { get; private set; }
            internal ChannelBuildPlatformResponder.PlatformState Current { get; private set; }

            public ChannelBuildPlatformResponder.PlatformState Capture(BuildTarget target) => Current;
            public bool FileExists(string path) => ExistingFile;

            public void Apply(ChannelBuildPlatformResponder.PlatformState state)
            {
                ApplyCount++;
                Current = state;
                if (ThrowAfterApply && ApplyCount == 1)
                {
                    throw new GameException("apply failed");
                }
            }
        }

        private sealed class SucceededDependency : IChannelBuildResponder
        {
            internal SucceededDependency(string id) => Id = id;
            public string Id { get; }
            public int Order => 0;
            public IReadOnlyList<string> DependsOn => Array.Empty<string>();
            public ChannelBuildStepResult Prepare(ChannelBuildContext context) => Result(ChannelBuildResponderPhase.Prepare);
            public ChannelBuildStepResult Apply(ChannelBuildContext context) => Result(ChannelBuildResponderPhase.Apply);
            public ChannelBuildStepResult Restore(ChannelBuildContext context) => Result(ChannelBuildResponderPhase.Restore);
            private ChannelBuildStepResult Result(ChannelBuildResponderPhase phase) =>
                new ChannelBuildStepResult(Id, phase, true);
        }
    }
}
