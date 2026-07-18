using System;

namespace GameDeveloperKit.Playable
{
    public sealed class AudioPlayableRequest
    {
        public AudioPlayableRequest(string location, AudioPlayableOptions options = null)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("Location cannot be empty.", nameof(location));
            }

            Location = location;
            Options = options ?? new AudioPlayableOptions();
        }

        public string Location { get; }

        public AudioPlayableOptions Options { get; }
    }
}
