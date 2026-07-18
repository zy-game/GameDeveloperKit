using System;
using UnityEditor;

namespace GameDeveloperKit.ChannelBuild
{
    public sealed class ChannelBuildSigningInput
    {
        private ChannelBuildSigningInput(
            ChannelBuildSigningKind kind,
            string keystorePath,
            string keystorePassword,
            string keyAlias,
            string keyAliasPassword,
            string appleTeamId,
            string provisioningProfileId,
            ProvisioningProfileType provisioningProfileType)
        {
            Kind = kind;
            KeystorePath = keystorePath;
            KeystorePassword = keystorePassword;
            KeyAlias = keyAlias;
            KeyAliasPassword = keyAliasPassword;
            AppleTeamId = appleTeamId;
            ProvisioningProfileId = provisioningProfileId;
            ProvisioningProfileType = provisioningProfileType;
        }

        internal ChannelBuildSigningKind Kind { get; }
        internal string KeystorePath { get; }
        internal string KeystorePassword { get; }
        internal string KeyAlias { get; }
        internal string KeyAliasPassword { get; }
        internal string AppleTeamId { get; }
        internal string ProvisioningProfileId { get; }
        internal ProvisioningProfileType ProvisioningProfileType { get; }

        public static ChannelBuildSigningInput Android(
            string keystorePath,
            string keystorePassword,
            string keyAlias,
            string keyAliasPassword)
        {
            return new ChannelBuildSigningInput(
                ChannelBuildSigningKind.Android,
                RequireSecret(keystorePath, nameof(keystorePath)),
                RequireSecret(keystorePassword, nameof(keystorePassword)),
                RequireSecret(keyAlias, nameof(keyAlias)),
                RequireSecret(keyAliasPassword, nameof(keyAliasPassword)),
                null,
                null,
                ProvisioningProfileType.Automatic);
        }

        public static ChannelBuildSigningInput IosAutomatic(string appleTeamId)
        {
            return new ChannelBuildSigningInput(
                ChannelBuildSigningKind.IosAutomatic,
                null,
                null,
                null,
                null,
                RequireSecret(appleTeamId, nameof(appleTeamId)),
                null,
                ProvisioningProfileType.Automatic);
        }

        public static ChannelBuildSigningInput IosManual(
            string appleTeamId,
            string provisioningProfileId,
            ProvisioningProfileType provisioningProfileType)
        {
            if (provisioningProfileType != ProvisioningProfileType.Development &&
                provisioningProfileType != ProvisioningProfileType.Distribution)
            {
                throw new ArgumentException(
                    "Manual iOS signing requires a development or distribution profile type.",
                    nameof(provisioningProfileType));
            }

            return new ChannelBuildSigningInput(
                ChannelBuildSigningKind.IosManual,
                null,
                null,
                null,
                null,
                RequireSecret(appleTeamId, nameof(appleTeamId)),
                RequireSecret(provisioningProfileId, nameof(provisioningProfileId)),
                provisioningProfileType);
        }

        public override string ToString()
        {
            return Kind.ToString();
        }

        private static string RequireSecret(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Signing input cannot be empty.", parameterName);
            }
            if (value.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                throw new ArgumentException("Signing input cannot contain line breaks.", parameterName);
            }

            return value;
        }
    }

    internal enum ChannelBuildSigningKind
    {
        Android = 0,
        IosAutomatic = 1,
        IosManual = 2
    }
}
