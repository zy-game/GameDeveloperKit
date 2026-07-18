using System.Collections.Generic;

namespace GameDeveloperKit.ChannelBuild
{
    public sealed partial class ChannelPlayerBuildService
    {
        public ChannelPlayerBuildService()
            : this(CreateDefaultResponders, new UnityPlayerBuildGateway())
        {
        }

        private static IReadOnlyList<IChannelBuildResponder> CreateDefaultResponders(
            ChannelBuildSigningInput signingInput)
        {
            return new IChannelBuildResponder[]
            {
                new ChannelBuildResourceResponder(),
                new ChannelBuildDefinesResponder(),
                new ChannelBuildConfigResponder(),
                new ChannelBuildBrandingResponder(),
                new ChannelBuildPlatformResponder(signingInput)
            };
        }
    }
}
