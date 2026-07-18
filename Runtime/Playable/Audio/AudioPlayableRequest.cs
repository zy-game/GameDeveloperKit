using System;

namespace GameDeveloperKit.Playable
{
    public enum AudioLocationKind
    {
        Resource = 0,
        StreamingAssets = 1,
        Url = 2
    }

    public sealed class AudioPlayableRequest
    {
        public AudioPlayableRequest(string location, AudioPlayableOptions options = null)
            : this(location, AudioLocationKind.Resource, options)
        {
        }

        public AudioPlayableRequest(
            string location,
            AudioLocationKind locationKind,
            AudioPlayableOptions options = null)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("Location cannot be empty.", nameof(location));
            }

            if (Enum.IsDefined(typeof(AudioLocationKind), locationKind) is false)
            {
                throw new ArgumentOutOfRangeException(nameof(locationKind));
            }

            var normalized = location.Trim();
            ValidateLocation(normalized, locationKind);
            Location = normalized;
            LocationKind = locationKind;
            Options = options ?? new AudioPlayableOptions();
        }

        public string Location { get; }

        public AudioLocationKind LocationKind { get; }

        public AudioPlayableOptions Options { get; }

        private static void ValidateLocation(string location, AudioLocationKind locationKind)
        {
            if (locationKind == AudioLocationKind.Url)
            {
                if (Uri.TryCreate(location, UriKind.Absolute, out var uri) is false ||
                    string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) is false ||
                    string.IsNullOrWhiteSpace(uri.Host) ||
                    string.IsNullOrWhiteSpace(uri.UserInfo) is false)
                {
                    throw new ArgumentException("Audio URL must be an absolute HTTPS URL.", nameof(location));
                }

                return;
            }

            if (locationKind != AudioLocationKind.StreamingAssets)
            {
                return;
            }

            var value = location.Replace('\\', '/');
            if (value.StartsWith("/", StringComparison.Ordinal) ||
                value.IndexOf("://", StringComparison.Ordinal) >= 0 ||
                value.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("StreamingAssets/", StringComparison.OrdinalIgnoreCase) ||
                value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':')
            {
                throw new ArgumentException("StreamingAssets audio location must be a normalized relative path.", nameof(location));
            }

            var segments = value.Split('/');
            for (var i = 0; i < segments.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(segments[i]) || segments[i] == "." || segments[i] == "..")
                {
                    throw new ArgumentException("StreamingAssets audio location contains an unsafe path segment.", nameof(location));
                }
            }
        }
    }
}
