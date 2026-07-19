using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace GameDeveloperKit.ChannelBuild
{
    public static class ChannelBuildReportWriter
    {
        public const int CurrentSchemaVersion = 1;

        public static ChannelBuildArtifact CaptureArtifact(
            string kind,
            string outputRoot,
            string artifactPath)
        {
            ChannelBuildContext.RequireSafeSegment(kind, nameof(kind));
            ChannelBuildContext.RequireText(outputRoot, nameof(outputRoot));
            ChannelBuildContext.RequireText(artifactPath, nameof(artifactPath));

            var rootPath = Path.GetFullPath(outputRoot);
            var filePath = Path.GetFullPath(artifactPath);
            var rootPrefix = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (filePath.StartsWith(rootPrefix, comparison) is false)
            {
                throw new ArgumentException("Artifact path must remain inside output root.", nameof(artifactPath));
            }

            if (System.IO.File.Exists(filePath) is false)
            {
                throw new FileNotFoundException("Channel build artifact was not found.", filePath);
            }

            string hash;
            long size;
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha256 = SHA256.Create())
            {
                size = stream.Length;
                hash = BitConverter.ToString(sha256.ComputeHash(stream))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }

            var relativePath = filePath.Substring(rootPrefix.Length)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            return new ChannelBuildArtifact(kind, relativePath, hash, size);
        }

        public static void Write(string reportPath, ChannelBuildReport report)
        {
            ChannelBuildContext.RequireText(reportPath, nameof(reportPath));
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var fullPath = Path.GetFullPath(reportPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("Report path directory is invalid.", nameof(reportPath));
            }

            Directory.CreateDirectory(directory);
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Culture = System.Globalization.CultureInfo.InvariantCulture,
                DateFormatString = "o",
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                NullValueHandling = NullValueHandling.Include
            };
            var json = JsonConvert.SerializeObject(report, Formatting.Indented, settings);
            var tempPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                System.IO.File.WriteAllText(tempPath, json, new UTF8Encoding(false));
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Replace(tempPath, fullPath, null);
                }
                else
                {
                    System.IO.File.Move(tempPath, fullPath);
                }
            }
            finally
            {
                if (System.IO.File.Exists(tempPath))
                {
                    System.IO.File.Delete(tempPath);
                }
            }
        }
    }
}
