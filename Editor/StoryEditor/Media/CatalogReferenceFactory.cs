using System;
using System.Collections.Generic;
using GameDeveloperKit.Story.Media;

namespace GameDeveloperKit.StoryEditor.Media
{
    public static class CatalogReferenceFactory
    {
        public static VideoReference CreateVideoReference(CatalogItem item, string cdnBaseUrl)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (item.Kind != MediaKind.Video)
            {
                throw new CatalogException(CatalogErrorKind.UnsupportedMediaKind, "Catalog item is not a video.");
            }

            if (string.IsNullOrWhiteSpace(item.MediaId))
            {
                throw new CatalogException(CatalogErrorKind.InvalidResponse, "Catalog video requires a stable media ID.");
            }

            var primaryLocation = ExpandHttpsLocation(cdnBaseUrl, item.Location);
            var renditions = new List<VideoRendition>(item.Renditions.Count);
            if (item.Format == VideoFormat.Mp4 && item.Width > 0 && item.Height > 0 && item.DurationMs > 0)
            {
                renditions.Add(new VideoRendition(
                    FormatLabel(item.Height),
                    item.MediaId,
                    primaryLocation,
                    item.Width,
                    item.Height,
                    item.Bitrate,
                    item.DurationMs));
            }

            for (var i = 0; i < item.Renditions.Count; i++)
            {
                var rendition = item.Renditions[i];
                renditions.Add(new VideoRendition(
                    rendition.Label,
                    string.IsNullOrWhiteSpace(rendition.MediaId) ? item.MediaId : rendition.MediaId,
                    ExpandHttpsLocation(cdnBaseUrl, rendition.Location),
                    rendition.Width,
                    rendition.Height,
                    rendition.Bitrate,
                    rendition.DurationMs));
            }

            try
            {
                return new VideoReference(
                    new MediaReference(MediaKind.Video, MediaSource.Cdn, item.MediaId, primaryLocation),
                    item.Format,
                    renditions);
            }
            catch (ArgumentException exception)
            {
                throw new CatalogException(CatalogErrorKind.InvalidLocation, exception.Message, exception);
            }
        }

        public static MediaReference CreateAudioReference(CatalogItem item, string cdnBaseUrl)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (item.Kind != MediaKind.Audio)
            {
                throw new CatalogException(CatalogErrorKind.UnsupportedMediaKind, "Catalog item is not audio.");
            }

            try
            {
                return new MediaReference(
                    MediaKind.Audio,
                    MediaSource.Cdn,
                    item.MediaId,
                    ExpandHttpsLocation(cdnBaseUrl, item.Location));
            }
            catch (ArgumentException exception)
            {
                throw new CatalogException(CatalogErrorKind.InvalidLocation, exception.Message, exception);
            }
        }

        private static string FormatLabel(int height)
        {
            switch (height)
            {
                case 480: return "480P";
                case 720: return "720P";
                case 1080: return "1080P";
                case 1440: return "2K";
                case 2160: return "4K";
                default: return $"{height}P";
            }
        }

        public static string ExpandHttpsLocation(string cdnBaseUrl, string location)
        {
            if (Uri.TryCreate(location?.Trim(), UriKind.Absolute, out var absolute))
            {
                if (string.Equals(absolute.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(absolute.Host) is false &&
                    string.IsNullOrWhiteSpace(absolute.UserInfo))
                {
                    return location.Trim();
                }

                throw new CatalogException(CatalogErrorKind.InvalidLocation, "Catalog location must use HTTPS.");
            }

            if (Uri.TryCreate(cdnBaseUrl?.Trim(), UriKind.Absolute, out var baseUri) is false ||
                string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) is false ||
                string.IsNullOrWhiteSpace(baseUri.Host) ||
                string.IsNullOrWhiteSpace(baseUri.UserInfo) is false)
            {
                throw new CatalogException(CatalogErrorKind.InvalidSettings, "CDN base URL must be an absolute HTTPS URL.");
            }

            var relative = location?.Trim().Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(relative) || relative.StartsWith("/", StringComparison.Ordinal))
            {
                throw new CatalogException(CatalogErrorKind.InvalidLocation, "Catalog relative location is invalid.");
            }

            var pathEnd = relative.IndexOfAny(new[] { '?', '#' });
            var encodedPath = pathEnd >= 0 ? relative.Substring(0, pathEnd) : relative;
            ValidatePercentEncoding(encodedPath);
            string decodedPath;
            try
            {
                decodedPath = Uri.UnescapeDataString(encodedPath);
            }
            catch (UriFormatException exception)
            {
                throw new CatalogException(CatalogErrorKind.InvalidLocation, "Catalog location contains invalid URL encoding.", exception);
            }
            if (decodedPath.IndexOf('\\') >= 0)
            {
                throw new CatalogException(CatalogErrorKind.InvalidLocation, "Catalog location contains an unsafe path separator.");
            }

            var segments = decodedPath.Split('/');
            for (var i = 0; i < segments.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(segments[i]) ||
                    string.Equals(segments[i], ".", StringComparison.Ordinal) ||
                    string.Equals(segments[i], "..", StringComparison.Ordinal))
                {
                    throw new CatalogException(CatalogErrorKind.InvalidLocation, "Catalog location contains an unsafe path segment.");
                }
            }

            return new Uri(EnsureTrailingSlash(baseUri), relative).AbsoluteUri;
        }

        private static Uri EnsureTrailingSlash(Uri uri)
        {
            return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
                ? uri
                : new Uri(uri.AbsoluteUri + "/", UriKind.Absolute);
        }

        private static void ValidatePercentEncoding(string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != '%')
                {
                    continue;
                }

                if (i + 2 >= value.Length || IsHex(value[i + 1]) is false || IsHex(value[i + 2]) is false)
                {
                    throw new CatalogException(CatalogErrorKind.InvalidLocation, "Catalog location contains invalid URL encoding.");
                }

                i += 2;
            }
        }

        private static bool IsHex(char value)
        {
            return value >= '0' && value <= '9' ||
                   value >= 'a' && value <= 'f' ||
                   value >= 'A' && value <= 'F';
        }
    }
}
