using System;
using System.Globalization;

namespace GameDeveloperKit.MediaEditor
{
    internal static class FfmpegProgressParser
    {
        public static bool TryParse(
            string line,
            double durationSeconds,
            out float progress,
            out TimeSpan processedTime)
        {
            progress = 0f;
            processedTime = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(line) || durationSeconds <= 0d)
            {
                return false;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0 || separator == line.Length - 1)
            {
                return false;
            }

            var name = line.Substring(0, separator);
            var value = line.Substring(separator + 1);
            if (string.Equals(name, "out_time", StringComparison.Ordinal))
            {
                if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out processedTime) is false)
                {
                    return false;
                }
            }
            else if (string.Equals(name, "out_time_us", StringComparison.Ordinal) ||
                     string.Equals(name, "out_time_ms", StringComparison.Ordinal))
            {
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds) is false)
                {
                    return false;
                }

                processedTime = TimeSpan.FromTicks(Math.Max(0L, microseconds) * 10L);
            }
            else
            {
                return false;
            }

            progress = (float)Math.Max(0d, Math.Min(1d, processedTime.TotalSeconds / durationSeconds));
            return true;
        }
    }
}
