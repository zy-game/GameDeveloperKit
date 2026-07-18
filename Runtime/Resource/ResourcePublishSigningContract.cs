using System;
using System.Globalization;
using System.Text;

namespace GameDeveloperKit.Resource
{
    internal static class ResourcePublishSigningContract
    {
        internal const int CurrentProtocolVersion = 1;
        private const string SigningDomain = "gdk-resource-publish-v1";

        internal static byte[] BuildPayload(
            int protocolVersion,
            string channel,
            string platform,
            string version,
            string manifestSha256,
            long minimumClientBuild,
            long maximumClientBuild)
        {
            if (manifestSha256 == null)
            {
                throw new ArgumentNullException(nameof(manifestSha256));
            }

            var canonical = string.Join(
                "\n",
                SigningDomain,
                protocolVersion.ToString(CultureInfo.InvariantCulture),
                channel,
                platform,
                version,
                manifestSha256.ToLowerInvariant(),
                minimumClientBuild.ToString(CultureInfo.InvariantCulture),
                maximumClientBuild.ToString(CultureInfo.InvariantCulture));
            return Encoding.UTF8.GetBytes(canonical);
        }
    }
}
