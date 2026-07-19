using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEditor;

namespace GameDeveloperKit.ChannelBuild
{
    public sealed class ChannelBuildContext
    {
        public ChannelBuildContext(
            string channel,
            ChannelBuildEnvironment environment,
            BuildTarget buildTarget,
            string version,
            int playerBuildNumber,
            string outputRoot,
            string flavor = null,
            string remoteRoot = null,
            long? minimumClientBuild = null,
            long? maximumClientBuild = null,
            ChannelProfile profile = null,
            IReadOnlyDictionary<string, string> arguments = null,
            CiBuildMetadata ci = null)
        {
            Channel = RequireSafeSegment(channel, nameof(channel));
            if (Enum.IsDefined(typeof(ChannelBuildEnvironment), environment) is false)
            {
                throw new ArgumentException("Channel build environment is not defined.", nameof(environment));
            }

            if (buildTarget == BuildTarget.NoTarget || Enum.IsDefined(typeof(BuildTarget), buildTarget) is false)
            {
                throw new ArgumentException("Channel build target is not defined.", nameof(buildTarget));
            }

            if (playerBuildNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerBuildNumber),
                    playerBuildNumber,
                    "Player build number must be positive.");
            }

            ValidateClientBuildRange(
                minimumClientBuild,
                maximumClientBuild,
                nameof(minimumClientBuild),
                nameof(maximumClientBuild));
            if (profile != null && string.Equals(profile.Channel, Channel, StringComparison.Ordinal) is false)
            {
                throw new ArgumentException("Channel profile does not match the build channel.", nameof(profile));
            }

            ValidateDictionary(arguments, nameof(arguments), true);

            Environment = environment;
            Flavor = ValidateOptionalSafeSegment(flavor, nameof(flavor));
            BuildTarget = buildTarget;
            Platform = buildTarget.ToString();
            Version = RequireSafeSegment(version, nameof(version));
            PlayerBuildNumber = playerBuildNumber;
            OutputRoot = RequireText(outputRoot, nameof(outputRoot));
            RemoteRoot = ValidateOptionalHttpsUrl(remoteRoot, nameof(remoteRoot));
            MinimumClientBuild = minimumClientBuild;
            MaximumClientBuild = maximumClientBuild;
            Profile = profile;
            Arguments = CopyDictionary(arguments);
            Ci = ci;
        }

        public string Channel { get; }

        public ChannelBuildEnvironment Environment { get; }

        public string Flavor { get; }

        public BuildTarget BuildTarget { get; }

        public string Platform { get; }

        public string Version { get; }

        public int PlayerBuildNumber { get; }

        public string OutputRoot { get; }

        public string RemoteRoot { get; }

        public long? MinimumClientBuild { get; }

        public long? MaximumClientBuild { get; }

        public ChannelProfile Profile { get; }

        public IReadOnlyDictionary<string, string> Arguments { get; }

        public CiBuildMetadata Ci { get; }

        internal static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", parameterName);
            }

            ValidateNoLineBreak(value, parameterName);
            return value;
        }

        internal static string ValidateOptionalText(string value, string parameterName)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return RequireText(value, parameterName);
        }

        internal static string RequireSafeSegment(string value, string parameterName)
        {
            RequireText(value, parameterName);
            if (value == "." || value == "..")
            {
                throw new ArgumentException("Value cannot be a relative path segment.", parameterName);
            }

            foreach (var character in value)
            {
                if ((character >= 'a' && character <= 'z') ||
                    (character >= 'A' && character <= 'Z') ||
                    (character >= '0' && character <= '9') ||
                    character == '.' || character == '_' || character == '-')
                {
                    continue;
                }

                throw new ArgumentException(
                    "Value may only contain ASCII letters, digits, '.', '_' or '-'.",
                    parameterName);
            }

            return value;
        }

        internal static string ValidateOptionalSafeSegment(string value, string parameterName)
        {
            return string.IsNullOrEmpty(value) ? null : RequireSafeSegment(value, parameterName);
        }

        internal static string ValidateOptionalHttpUrl(string value, string parameterName)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            RequireText(value, parameterName);
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) is false ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("Value must be an absolute HTTP or HTTPS URL.", parameterName);
            }

            return value;
        }

        internal static void ValidateTextList(IReadOnlyList<string> values, string parameterName)
        {
            if (values == null)
            {
                return;
            }

            for (var i = 0; i < values.Count; i++)
            {
                RequireText(values[i], parameterName);
            }
        }

        internal static IReadOnlyList<string> CopyList(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new List<string>(values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                copy.Add(values[i]);
            }

            return copy.AsReadOnly();
        }

        internal static IReadOnlyDictionary<string, string> CopyDictionary(
            IReadOnlyDictionary<string, string> values)
        {
            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            if (values != null)
            {
                foreach (var pair in values)
                {
                    copy.Add(pair.Key, pair.Value);
                }
            }

            return new ReadOnlyDictionary<string, string>(copy);
        }

        internal static void ValidateDictionary(
            IReadOnlyDictionary<string, string> values,
            string parameterName,
            bool rejectSensitiveKeys)
        {
            if (values == null)
            {
                return;
            }

            foreach (var pair in values)
            {
                var key = RequireText(pair.Key, parameterName);
                if (rejectSensitiveKeys && IsSensitiveKey(key))
                {
                    throw new ArgumentException($"Sensitive key '{key}' is not allowed.", parameterName);
                }

                if (pair.Value == null)
                {
                    throw new ArgumentException($"Value for key '{key}' cannot be null.", parameterName);
                }

                ValidateNoLineBreak(pair.Value, parameterName);
            }
        }

        private static string ValidateOptionalHttpsUrl(string value, string parameterName)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            RequireText(value, parameterName);
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) is false ||
                uri.Scheme != Uri.UriSchemeHttps ||
                string.IsNullOrEmpty(uri.UserInfo) is false ||
                string.IsNullOrEmpty(uri.Query) is false ||
                string.IsNullOrEmpty(uri.Fragment) is false)
            {
                throw new ArgumentException(
                    "Value must be an absolute HTTPS base URL without credentials, query or fragment.",
                    parameterName);
            }

            return value;
        }

        private static void ValidateClientBuildRange(
            long? minimum,
            long? maximum,
            string minimumParameterName,
            string maximumParameterName)
        {
            if (minimum.HasValue != maximum.HasValue)
            {
                throw new ArgumentException(
                    "Minimum and maximum client build must be provided together.",
                    minimum.HasValue ? maximumParameterName : minimumParameterName);
            }

            if (minimum.HasValue is false)
            {
                return;
            }

            if (minimum.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    minimumParameterName,
                    minimum,
                    "Minimum client build must be positive.");
            }

            if (maximum.Value < minimum.Value)
            {
                throw new ArgumentOutOfRangeException(
                    maximumParameterName,
                    maximum,
                    "Maximum client build must be greater than or equal to minimum client build.");
            }
        }

        private static bool IsSensitiveKey(string key)
        {
            var markers = new[]
            {
                "secret",
                "password",
                "token",
                "privatekey",
                "private_key",
                "private-key",
                "private key",
                "credential",
                "accesskey",
                "access_key",
                "access-key",
                "access key",
                "signingkey",
                "signing_key",
                "signing-key",
                "signing key"
            };
            foreach (var marker in markers)
            {
                if (key.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateNoLineBreak(string value, string parameterName)
        {
            if (value != null && value.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                throw new ArgumentException("Value cannot contain line breaks.", parameterName);
            }
        }
    }
}
