using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace GameDeveloperKit.ResourceRelease;

public static class ReleasePlanBuilder
{
    private const int ReportSchemaVersion = 1;

    public static ReleasePlan Build(
        string reportPath,
        string outputRoot,
        long minimumClientBuild,
        long maximumClientBuild,
        string keyId,
        string? expectedPointerETag)
    {
        RequireFile(reportPath, nameof(reportPath));
        var root = Path.GetFullPath(RequireText(outputRoot, nameof(outputRoot)));
        if (minimumClientBuild <= 0 || maximumClientBuild < minimumClientBuild)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumClientBuild),
                "Client build range is invalid.");
        }

        keyId = RequireSafeSegment(keyId, nameof(keyId));
        expectedPointerETag = NormalizeOptionalText(expectedPointerETag, nameof(expectedPointerETag));
        using var document = JsonDocument.Parse(File.ReadAllBytes(reportPath), new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        });
        var report = document.RootElement;
        RequireObject(report, "report");
        RequireExactProperties(
            report,
            "schemaVersion", "status", "failureKind", "exitCode", "context", "ci",
            "artifacts", "steps", "warnings", "startedAtUtc", "finishedAtUtc");
        if (RequireInt32(report, "schemaVersion") != ReportSchemaVersion ||
            RequireString(report, "status") != "succeeded" ||
            RequireString(report, "failureKind") != "none" ||
            RequireInt32(report, "exitCode") != 0)
        {
            throw new InvalidDataException("Channel build report is not a successful schema v1 report.");
        }

        var context = RequireProperty(report, "context");
        RequireObject(context, "context");
        RequireExactProperties(context, "channel", "platform", "version");
        var channel = RequireSafeSegment(RequireString(context, "channel"), "channel");
        var platform = RequireSafeSegment(RequireString(context, "platform"), "platform");
        var version = RequireSafeSegment(RequireString(context, "version"), "version");
        var versionPrefix = string.Join('/', channel, platform, version);

        var artifactArray = RequireProperty(report, "artifacts");
        if (artifactArray.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Report artifacts must be an array.");
        }

        var artifacts = new List<ReleaseArtifact>();
        var relativePaths = new HashSet<string>(StringComparer.Ordinal);
        var remoteKeys = new HashSet<string>(StringComparer.Ordinal);
        var manifestCount = 0;
        foreach (var item in artifactArray.EnumerateArray())
        {
            RequireObject(item, "artifact");
            RequireExactProperties(item, "kind", "path", "sha256", "sizeBytes");
            var kind = RequireString(item, "kind");
            if (kind != "resource-manifest" && kind != "resource-artifact")
            {
                continue;
            }

            var relativePath = NormalizeRelativePath(RequireString(item, "path"));
            if (relativePaths.Add(relativePath) is false)
            {
                throw new InvalidDataException("Resource artifact path is duplicated.");
            }
            var localPath = ResolveContainedFile(root, relativePath);
            var expectedHash = NormalizeSha256(RequireString(item, "sha256"));
            var expectedSize = RequireInt64(item, "sizeBytes");
            var actualSize = new FileInfo(localPath).Length;
            var actualHash = ComputeSha256(localPath);
            if (actualSize != expectedSize || actualHash != expectedHash)
            {
                throw new InvalidDataException("Resource artifact evidence does not match the local file.");
            }

            var remoteKey = versionPrefix + '/' + relativePath;
            if (remoteKeys.Add(remoteKey) is false)
            {
                throw new InvalidDataException("Resource remote key is duplicated.");
            }
            if (kind == "resource-manifest")
            {
                manifestCount++;
            }
            artifacts.Add(new ReleaseArtifact(
                kind,
                localPath,
                relativePath,
                remoteKey,
                actualHash,
                actualSize));
        }

        if (artifacts.Count == 0 || manifestCount != 1)
        {
            throw new InvalidDataException("Release requires exactly one resource manifest and at least one artifact.");
        }
        artifacts.Sort((left, right) => string.Compare(left.RemoteKey, right.RemoteKey, StringComparison.Ordinal));
        return new ReleasePlan(
            channel,
            platform,
            version,
            root,
            minimumClientBuild,
            maximumClientBuild,
            keyId,
            string.Join('/', channel, platform, "publish.json"),
            expectedPointerETag,
            artifacts);
    }

    internal static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    internal static string RequireSafeSegment(string value, string name)
    {
        value = RequireText(value, name);
        if (value is "." or ".." || value.Any(character =>
                char.IsAsciiLetterOrDigit(character) is false && character is not '.' and not '_' and not '-'))
        {
            throw new ArgumentException("Value must be a safe segment.", name);
        }
        return value;
    }

    private static string ResolveContainedFile(string root, string relativePath)
    {
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) is false || File.Exists(path) is false)
        {
            throw new FileNotFoundException("Resource artifact is missing or outside output root.");
        }
        return path;
    }

    private static string NormalizeRelativePath(string value)
    {
        value = RequireText(value, "path");
        if (Path.IsPathRooted(value) || value.Contains('\\'))
        {
            throw new InvalidDataException("Artifact path must be a forward-slash relative path.");
        }
        var segments = value.Split('/');
        if (segments.Any(segment => string.IsNullOrEmpty(segment) || segment is "." or ".."))
        {
            throw new InvalidDataException("Artifact path is not normalized.");
        }
        return string.Join('/', segments);
    }

    private static string NormalizeSha256(string value)
    {
        if (value.Length != 64 || value.Any(character => character is < '0' or > '9' and < 'a' or > 'f'))
        {
            throw new InvalidDataException("Artifact SHA-256 must be lowercase hexadecimal.");
        }
        return value;
    }

    private static string RequireText(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(['\r', '\n']) >= 0)
        {
            throw new ArgumentException("Value must be non-empty single-line text.", name);
        }
        return value;
    }

    private static string? NormalizeOptionalText(string? value, string name)
    {
        return value is null ? null : RequireText(value, name);
    }

    private static void RequireFile(string path, string name)
    {
        RequireText(path, name);
        if (File.Exists(path) is false)
        {
            throw new FileNotFoundException("Required file does not exist.", path);
        }
    }

    private static JsonElement RequireProperty(JsonElement value, string name)
    {
        return value.TryGetProperty(name, out var property)
            ? property
            : throw new InvalidDataException($"JSON member '{name}' is missing.");
    }

    private static string RequireString(JsonElement value, string name)
    {
        var property = RequireProperty(value, name);
        if (property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"JSON member '{name}' must be a string.");
        }
        return RequireText(property.GetString(), name);
    }

    private static int RequireInt32(JsonElement value, string name)
    {
        var property = RequireProperty(value, name);
        return property.TryGetInt32(out var number)
            ? number
            : throw new InvalidDataException($"JSON member '{name}' must be an integer.");
    }

    private static long RequireInt64(JsonElement value, string name)
    {
        var property = RequireProperty(value, name);
        return property.TryGetInt64(out var number) && number >= 0
            ? number
            : throw new InvalidDataException($"JSON member '{name}' must be a non-negative integer.");
    }

    private static void RequireObject(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"JSON value '{name}' must be an object.");
        }
    }

    private static void RequireExactProperties(JsonElement value, params string[] names)
    {
        var expected = new HashSet<string>(names, StringComparer.Ordinal);
        var actual = value.EnumerateObject().Select(property => property.Name).ToArray();
        if (actual.Length != expected.Count || actual.Any(name => expected.Contains(name) is false))
        {
            throw new InvalidDataException("JSON object contains missing or unknown members.");
        }
    }
}
