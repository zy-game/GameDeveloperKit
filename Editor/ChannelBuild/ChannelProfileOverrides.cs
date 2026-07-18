using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GameDeveloperKit.ChannelBuild
{
    public sealed class ChannelProfileOverrides
    {
        public ChannelProfileOverrides(
            string channel = null,
            string displayName = null,
            string productName = null,
            string applicationIdentifier = null,
            string iconPath = null,
            string splashPath = null,
            IReadOnlyList<string> defines = null,
            IReadOnlyDictionary<string, string> configOverrides = null,
            IReadOnlyDictionary<string, string> resourceOverrides = null,
            IReadOnlyDictionary<string, string> platformOptions = null)
        {
            Channel = channel == null
                ? null
                : ChannelBuildContext.RequireSafeSegment(channel, nameof(channel));
            DisplayName = ValidateOptionalScalar(displayName, nameof(displayName));
            ProductName = ValidateOptionalScalar(productName, nameof(productName));
            ApplicationIdentifier = ValidateOptionalScalar(
                applicationIdentifier,
                nameof(applicationIdentifier));
            IconPath = ValidateOptionalScalar(iconPath, nameof(iconPath));
            SplashPath = ValidateOptionalScalar(splashPath, nameof(splashPath));
            ChannelBuildContext.ValidateTextList(defines, nameof(defines));
            ChannelBuildContext.ValidateDictionary(configOverrides, nameof(configOverrides), true);
            ChannelBuildContext.ValidateDictionary(resourceOverrides, nameof(resourceOverrides), true);
            ChannelBuildContext.ValidateDictionary(platformOptions, nameof(platformOptions), true);
            Defines = CopyOptionalList(defines);
            ConfigOverrides = CopyOptionalDictionary(configOverrides);
            ResourceOverrides = CopyOptionalDictionary(resourceOverrides);
            PlatformOptions = CopyOptionalDictionary(platformOptions);
        }

        public string Channel { get; }

        public string DisplayName { get; }

        public string ProductName { get; }

        public string ApplicationIdentifier { get; }

        public string IconPath { get; }

        public string SplashPath { get; }

        public IReadOnlyList<string> Defines { get; }

        public IReadOnlyDictionary<string, string> ConfigOverrides { get; }

        public IReadOnlyDictionary<string, string> ResourceOverrides { get; }

        public IReadOnlyDictionary<string, string> PlatformOptions { get; }

        private static string ValidateOptionalScalar(string value, string parameterName)
        {
            return value == null || value.Length == 0
                ? value
                : ChannelBuildContext.ValidateOptionalText(value, parameterName);
        }

        private static IReadOnlyList<string> CopyOptionalList(IReadOnlyList<string> values)
        {
            if (values == null)
            {
                return null;
            }

            var copy = new List<string>(values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                copy.Add(values[i]);
            }

            return copy.AsReadOnly();
        }

        private static IReadOnlyDictionary<string, string> CopyOptionalDictionary(
            IReadOnlyDictionary<string, string> values)
        {
            if (values == null)
            {
                return null;
            }

            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in values)
            {
                copy.Add(pair.Key, pair.Value);
            }

            return new ReadOnlyDictionary<string, string>(copy);
        }
    }
}
