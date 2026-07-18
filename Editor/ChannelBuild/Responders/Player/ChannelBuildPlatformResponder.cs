using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;

namespace GameDeveloperKit.ChannelBuild
{
    public sealed class ChannelBuildPlatformResponder : IChannelBuildResponder
    {
        public const string ResponderId = "platform";

        private const string AndroidBuildAppBundle = "android.buildAppBundle";
        private const string AndroidTargetArchitectures = "android.targetArchitectures";
        private const string IosSdkVersion = "ios.sdkVersion";

        private static readonly IReadOnlyList<string> Dependencies =
            Array.AsReadOnly(new[] { ChannelBuildBrandingResponder.ResponderId });

        private readonly ChannelBuildSigningInput m_SigningInput;
        private readonly IPlatformGateway m_Gateway;
        private ChannelBuildContext m_Context;
        private PlatformState m_Previous;
        private PlatformState m_Applied;
        private bool m_Prepared;
        private bool m_Mutated;

        public ChannelBuildPlatformResponder(ChannelBuildSigningInput signingInput = null)
            : this(signingInput, new UnityPlatformGateway())
        {
        }

        internal ChannelBuildPlatformResponder(
            ChannelBuildSigningInput signingInput,
            IPlatformGateway gateway)
        {
            m_SigningInput = signingInput;
            m_Gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        public string Id => ResponderId;
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => Dependencies;

        public ChannelBuildStepResult Prepare(ChannelBuildContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (m_Prepared)
            {
                throw new GameException("Channel platform responder can only be prepared once.");
            }

            if (context.BuildTarget != BuildTarget.Android && context.BuildTarget != BuildTarget.iOS)
            {
                return Failure(ChannelBuildResponderPhase.Prepare, "Channel platform target is not supported.");
            }

            try
            {
                var previous = m_Gateway.Capture(context.BuildTarget);
                if (TryCreateAppliedState(context, previous, out var applied, out var error) is false)
                {
                    return Failure(ChannelBuildResponderPhase.Prepare, error);
                }

                m_Context = context;
                m_Previous = previous;
                m_Applied = applied;
                m_Prepared = true;
                return new ChannelBuildStepResult(ResponderId, ChannelBuildResponderPhase.Prepare, true);
            }
            catch
            {
                return Failure(
                    ChannelBuildResponderPhase.Prepare,
                    "Channel platform settings could not be prepared.");
            }
        }

        public ChannelBuildStepResult Apply(ChannelBuildContext context)
        {
            ValidateContext(context);
            if (m_Mutated)
            {
                throw new GameException("Channel platform responder can only be applied once.");
            }

            m_Mutated = true;
            m_Gateway.Apply(m_Applied);
            return new ChannelBuildStepResult(
                ResponderId,
                ChannelBuildResponderPhase.Apply,
                true,
                outputs: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["platform.applied"] = context.BuildTarget == BuildTarget.Android
                        ? "buildNumber,buildAppBundle,targetArchitectures,signing"
                        : "buildNumber,sdkVersion,signing"
                });
        }

        public ChannelBuildStepResult Restore(ChannelBuildContext context)
        {
            ValidateContext(context);
            if (m_Mutated)
            {
                m_Gateway.Apply(m_Previous);
                m_Mutated = false;
            }

            return new ChannelBuildStepResult(ResponderId, ChannelBuildResponderPhase.Restore, true);
        }

        private bool TryCreateAppliedState(
            ChannelBuildContext context,
            PlatformState previous,
            out PlatformState applied,
            out string error)
        {
            var options = context.Profile?.PlatformOptions ??
                new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in options)
            {
                if (IsAllowedOption(context.BuildTarget, pair.Key) is false)
                {
                    applied = null;
                    error = "Channel platform option is not supported for the build target: " + pair.Key;
                    return false;
                }
            }

            if (context.BuildTarget == BuildTarget.Android)
            {
                return TryCreateAndroidState(context, previous, options, out applied, out error);
            }

            return TryCreateIosState(context, previous, options, out applied, out error);
        }

        private bool TryCreateAndroidState(
            ChannelBuildContext context,
            PlatformState previous,
            IReadOnlyDictionary<string, string> options,
            out PlatformState applied,
            out string error)
        {
            var buildAppBundle = previous.AndroidBuildAppBundle;
            if (options.TryGetValue(AndroidBuildAppBundle, out var buildAppBundleText) &&
                bool.TryParse(buildAppBundleText, out buildAppBundle) is false)
            {
                applied = null;
                error = "Channel platform option android.buildAppBundle must be true or false.";
                return false;
            }

            var architectures = previous.AndroidTargetArchitectures;
            if (options.TryGetValue(AndroidTargetArchitectures, out var architectureText) &&
                TryParseArchitectures(architectureText, out architectures) is false)
            {
                applied = null;
                error = "Channel platform option android.targetArchitectures is invalid.";
                return false;
            }

            if (m_SigningInput != null && m_SigningInput.Kind != ChannelBuildSigningKind.Android)
            {
                applied = null;
                error = "Channel signing input does not match the Android build target.";
                return false;
            }
            if (m_SigningInput != null &&
                (System.IO.Path.IsPathRooted(m_SigningInput.KeystorePath) is false ||
                 m_Gateway.FileExists(m_SigningInput.KeystorePath) is false))
            {
                applied = null;
                error = "Channel Android signing keystore locator is invalid.";
                return false;
            }

            applied = PlatformState.Android(
                context.PlayerBuildNumber,
                buildAppBundle,
                architectures,
                m_SigningInput?.KeystorePath ?? previous.AndroidKeystoreName,
                m_SigningInput?.KeystorePassword ?? previous.AndroidKeystorePassword,
                m_SigningInput?.KeyAlias ?? previous.AndroidKeyAlias,
                m_SigningInput?.KeyAliasPassword ?? previous.AndroidKeyAliasPassword);
            error = null;
            return true;
        }

        private bool TryCreateIosState(
            ChannelBuildContext context,
            PlatformState previous,
            IReadOnlyDictionary<string, string> options,
            out PlatformState applied,
            out string error)
        {
            var sdkVersion = previous.IosSdkVersion;
            if (options.TryGetValue(IosSdkVersion, out var sdkText) &&
                (Enum.TryParse(sdkText, false, out sdkVersion) is false ||
                 Enum.IsDefined(typeof(iOSSdkVersion), sdkVersion) is false))
            {
                applied = null;
                error = "Channel platform option ios.sdkVersion is invalid.";
                return false;
            }

            if (m_SigningInput != null && m_SigningInput.Kind == ChannelBuildSigningKind.Android)
            {
                applied = null;
                error = "Channel signing input does not match the iOS build target.";
                return false;
            }

            var teamId = previous.IosAppleTeamId;
            var automaticSigning = previous.IosAutomaticSigning;
            var provisioningId = previous.IosProvisioningProfileId;
            var provisioningType = previous.IosProvisioningProfileType;
            if (m_SigningInput?.Kind == ChannelBuildSigningKind.IosAutomatic)
            {
                teamId = m_SigningInput.AppleTeamId;
                automaticSigning = true;
                provisioningId = string.Empty;
                provisioningType = ProvisioningProfileType.Automatic;
            }
            else if (m_SigningInput?.Kind == ChannelBuildSigningKind.IosManual)
            {
                teamId = m_SigningInput.AppleTeamId;
                automaticSigning = false;
                provisioningId = m_SigningInput.ProvisioningProfileId;
                provisioningType = m_SigningInput.ProvisioningProfileType;
            }

            applied = PlatformState.Ios(
                context.PlayerBuildNumber.ToString(CultureInfo.InvariantCulture),
                sdkVersion,
                teamId,
                automaticSigning,
                provisioningId,
                provisioningType);
            error = null;
            return true;
        }

        private static bool IsAllowedOption(BuildTarget target, string key)
        {
            if (target == BuildTarget.Android)
            {
                return key == AndroidBuildAppBundle || key == AndroidTargetArchitectures;
            }

            return key == IosSdkVersion;
        }

        private static bool TryParseArchitectures(string value, out AndroidArchitecture architectures)
        {
            architectures = AndroidArchitecture.None;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var tokens = value.Split(',');
            for (var i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i].Trim();
                if (Enum.TryParse(token, false, out AndroidArchitecture architecture) is false ||
                    Enum.IsDefined(typeof(AndroidArchitecture), architecture) is false ||
                    architecture == AndroidArchitecture.None || architecture == AndroidArchitecture.All)
                {
                    architectures = AndroidArchitecture.None;
                    return false;
                }

                architectures |= architecture;
            }

            return architectures != AndroidArchitecture.None;
        }

        private static ChannelBuildStepResult Failure(
            ChannelBuildResponderPhase phase,
            string message)
        {
            var normalized = string.IsNullOrWhiteSpace(message)
                ? "Channel platform responder failed."
                : message.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return new ChannelBuildStepResult(ResponderId, phase, false, normalized);
        }

        private void ValidateContext(ChannelBuildContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (m_Prepared is false)
            {
                throw new GameException("Channel platform responder is not prepared.");
            }
            if (ReferenceEquals(context, m_Context) is false)
            {
                throw new GameException("Channel platform responder context does not match Prepare.");
            }
        }

        internal interface IPlatformGateway
        {
            PlatformState Capture(BuildTarget target);
            bool FileExists(string path);
            void Apply(PlatformState state);
        }

        internal sealed class PlatformState
        {
            private PlatformState(BuildTarget target)
            {
                Target = target;
            }

            internal BuildTarget Target { get; }
            internal int AndroidBundleVersionCode { get; private set; }
            internal bool AndroidBuildAppBundle { get; private set; }
            internal AndroidArchitecture AndroidTargetArchitectures { get; private set; }
            internal string AndroidKeystoreName { get; private set; }
            internal string AndroidKeystorePassword { get; private set; }
            internal string AndroidKeyAlias { get; private set; }
            internal string AndroidKeyAliasPassword { get; private set; }
            internal string IosBuildNumber { get; private set; }
            internal iOSSdkVersion IosSdkVersion { get; private set; }
            internal string IosAppleTeamId { get; private set; }
            internal bool IosAutomaticSigning { get; private set; }
            internal string IosProvisioningProfileId { get; private set; }
            internal ProvisioningProfileType IosProvisioningProfileType { get; private set; }

            internal static PlatformState Android(
                int buildNumber,
                bool buildAppBundle,
                AndroidArchitecture architectures,
                string keystoreName,
                string keystorePassword,
                string keyAlias,
                string keyAliasPassword)
            {
                return new PlatformState(BuildTarget.Android)
                {
                    AndroidBundleVersionCode = buildNumber,
                    AndroidBuildAppBundle = buildAppBundle,
                    AndroidTargetArchitectures = architectures,
                    AndroidKeystoreName = keystoreName,
                    AndroidKeystorePassword = keystorePassword,
                    AndroidKeyAlias = keyAlias,
                    AndroidKeyAliasPassword = keyAliasPassword
                };
            }

            internal static PlatformState Ios(
                string buildNumber,
                iOSSdkVersion sdkVersion,
                string appleTeamId,
                bool automaticSigning,
                string provisioningProfileId,
                ProvisioningProfileType provisioningProfileType)
            {
                return new PlatformState(BuildTarget.iOS)
                {
                    IosBuildNumber = buildNumber,
                    IosSdkVersion = sdkVersion,
                    IosAppleTeamId = appleTeamId,
                    IosAutomaticSigning = automaticSigning,
                    IosProvisioningProfileId = provisioningProfileId,
                    IosProvisioningProfileType = provisioningProfileType
                };
            }
        }

        private sealed class UnityPlatformGateway : IPlatformGateway
        {
            public PlatformState Capture(BuildTarget target)
            {
                if (target == BuildTarget.Android)
                {
                    return PlatformState.Android(
                        PlayerSettings.Android.bundleVersionCode,
                        EditorUserBuildSettings.buildAppBundle,
                        PlayerSettings.Android.targetArchitectures,
                        PlayerSettings.Android.keystoreName,
                        PlayerSettings.Android.keystorePass,
                        PlayerSettings.Android.keyaliasName,
                        PlayerSettings.Android.keyaliasPass);
                }

                return PlatformState.Ios(
                    PlayerSettings.iOS.buildNumber,
                    PlayerSettings.iOS.sdkVersion,
                    PlayerSettings.iOS.appleDeveloperTeamID,
                    PlayerSettings.iOS.appleEnableAutomaticSigning,
                    PlayerSettings.iOS.iOSManualProvisioningProfileID,
                    PlayerSettings.iOS.iOSManualProvisioningProfileType);
            }

            public bool FileExists(string path)
            {
                return System.IO.File.Exists(path);
            }

            public void Apply(PlatformState state)
            {
                var actions = state.Target == BuildTarget.Android
                    ? AndroidActions(state)
                    : IosActions(state);
                var failed = false;
                for (var i = 0; i < actions.Count; i++)
                {
                    try
                    {
                        actions[i]();
                    }
                    catch
                    {
                        failed = true;
                    }
                }

                if (failed)
                {
                    throw new GameException("Channel platform settings could not be applied.");
                }
            }

            private static IReadOnlyList<Action> AndroidActions(PlatformState state)
            {
                return new Action[]
                {
                    () => PlayerSettings.Android.bundleVersionCode = state.AndroidBundleVersionCode,
                    () => EditorUserBuildSettings.buildAppBundle = state.AndroidBuildAppBundle,
                    () => PlayerSettings.Android.targetArchitectures = state.AndroidTargetArchitectures,
                    () => PlayerSettings.Android.keystoreName = state.AndroidKeystoreName,
                    () => PlayerSettings.Android.keystorePass = state.AndroidKeystorePassword,
                    () => PlayerSettings.Android.keyaliasName = state.AndroidKeyAlias,
                    () => PlayerSettings.Android.keyaliasPass = state.AndroidKeyAliasPassword
                };
            }

            private static IReadOnlyList<Action> IosActions(PlatformState state)
            {
                return new Action[]
                {
                    () => PlayerSettings.iOS.buildNumber = state.IosBuildNumber,
                    () => PlayerSettings.iOS.sdkVersion = state.IosSdkVersion,
                    () => PlayerSettings.iOS.appleDeveloperTeamID = state.IosAppleTeamId,
                    () => PlayerSettings.iOS.appleEnableAutomaticSigning = state.IosAutomaticSigning,
                    () => PlayerSettings.iOS.iOSManualProvisioningProfileID = state.IosProvisioningProfileId,
                    () => PlayerSettings.iOS.iOSManualProvisioningProfileType = state.IosProvisioningProfileType
                };
            }
        }
    }
}
