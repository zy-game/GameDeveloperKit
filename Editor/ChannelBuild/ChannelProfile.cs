using System.Collections.Generic;

namespace GameDeveloperKit.ChannelBuild
{
    public sealed class ChannelProfile
    {
        public ChannelProfile(
            string id,
            string channel,
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
            Id = ChannelBuildContext.RequireSafeSegment(id, nameof(id));
            Channel = ChannelBuildContext.RequireSafeSegment(channel, nameof(channel));
            DisplayName = ChannelBuildContext.ValidateOptionalText(displayName, nameof(displayName));
            ProductName = ChannelBuildContext.ValidateOptionalText(productName, nameof(productName));
            ApplicationIdentifier = ChannelBuildContext.ValidateOptionalText(
                applicationIdentifier,
                nameof(applicationIdentifier));
            IconPath = ChannelBuildContext.ValidateOptionalText(iconPath, nameof(iconPath));
            SplashPath = ChannelBuildContext.ValidateOptionalText(splashPath, nameof(splashPath));
            ChannelBuildContext.ValidateTextList(defines, nameof(defines));
            ChannelBuildContext.ValidateDictionary(configOverrides, nameof(configOverrides), true);
            ChannelBuildContext.ValidateDictionary(resourceOverrides, nameof(resourceOverrides), true);
            ChannelBuildContext.ValidateDictionary(platformOptions, nameof(platformOptions), true);
            Defines = ChannelBuildContext.CopyList(defines);
            ConfigOverrides = ChannelBuildContext.CopyDictionary(configOverrides);
            ResourceOverrides = ChannelBuildContext.CopyDictionary(resourceOverrides);
            PlatformOptions = ChannelBuildContext.CopyDictionary(platformOptions);
        }

        public string Id { get; }

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
    }
}
