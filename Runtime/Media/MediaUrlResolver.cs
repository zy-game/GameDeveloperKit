using System;

namespace GameDeveloperKit.Media
{
    /// <summary>
    /// Resolves a relative media object path to its final public URL.
    /// </summary>
    public static class MediaUrlResolver
    {
        public static string Resolve(MediaPath path, MediaDeliverySettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (string.IsNullOrWhiteSpace(path.Value))
            {
                throw new ArgumentException("Media path is not initialized.", nameof(path));
            }

            var baseUrl = settings.UsesCdn ? settings.CdnBaseUrl : settings.OriginBaseUrl;
            var finalUrl = $"{baseUrl}/{path.Value}";
            if (Uri.TryCreate(finalUrl, UriKind.Absolute, out var uri) is false ||
                string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) is false)
            {
                throw new GameException($"Resolved media URL is invalid. path:{path.Value}");
            }

            return uri.AbsoluteUri;
        }
    }
}
