using System;

namespace GameDeveloperKit.Media
{
    /// <summary>
    /// A media object's path relative to its public delivery root.
    /// </summary>
    public readonly struct MediaPath : IEquatable<MediaPath>
    {
        public MediaPath(string value)
        {
            Value = Normalize(value);
        }

        public string Value { get; }

        public bool Equals(MediaPath other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MediaPath other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        private static string Normalize(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var path = value.Trim();
            if (path.Length == 0)
            {
                throw new ArgumentException("Media path cannot be empty.", nameof(value));
            }

            if (path[0] == '/' ||
                path.IndexOf('\\') >= 0 ||
                path.IndexOf(':') >= 0 ||
                path.IndexOf('?') >= 0 ||
                path.IndexOf('#') >= 0)
            {
                throw new ArgumentException("Media path must be a relative URL path without a scheme, query, or fragment.", nameof(value));
            }

            var segments = path.Split('/');
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (segment.Length == 0 ||
                    string.Equals(segment, ".", StringComparison.Ordinal) ||
                    string.Equals(segment, "..", StringComparison.Ordinal))
                {
                    throw new ArgumentException("Media path contains an empty or unsafe segment.", nameof(value));
                }
            }

            return string.Join("/", segments);
        }
    }
}
