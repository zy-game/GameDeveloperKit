using System.Text.Json;

namespace GameDeveloperKit.ResourceRelease;

internal static class ReleaseDescriptorReader
{
    internal static ReleaseDescriptor Read(string json, string channel, string platform, string version)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        });
        var root = document.RootElement;
        RequireObject(root);
        RequireExact(root, "schemaVersion", "channel", "platform", "version",
            "minimumClientBuild", "maximumClientBuild", "artifacts");
        if (RequireInt64(root, "schemaVersion") != 1 ||
            RequireString(root, "channel") != channel ||
            RequireString(root, "platform") != platform ||
            RequireString(root, "version") != version)
        {
            throw new InvalidDataException("Release descriptor identity or schema is invalid.");
        }
        var minimum = RequireInt64(root, "minimumClientBuild");
        var maximum = RequireInt64(root, "maximumClientBuild");
        if (minimum <= 0 || maximum < minimum)
        {
            throw new InvalidDataException("Release descriptor client range is invalid.");
        }
        var artifactValue = RequireProperty(root, "artifacts");
        if (artifactValue.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Release descriptor artifacts must be an array.");
        }
        var prefix = string.Join('/', channel, platform, version) + '/';
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var artifacts = new List<DescriptorArtifact>();
        var manifests = 0;
        foreach (var item in artifactValue.EnumerateArray())
        {
            RequireObject(item);
            RequireExact(item, "kind", "key", "sha256", "sizeBytes");
            var kind = RequireString(item, "kind");
            if (kind is not "resource-manifest" and not "resource-artifact")
            {
                throw new InvalidDataException("Release descriptor artifact kind is invalid.");
            }
            var key = RequireString(item, "key");
            if (!key.StartsWith(prefix, StringComparison.Ordinal) || !IsNormalizedKey(key) || !keys.Add(key))
            {
                throw new InvalidDataException("Release descriptor artifact key is invalid.");
            }
            var hash = NormalizeSha256(RequireString(item, "sha256"));
            var size = RequireInt64(item, "sizeBytes");
            if (size < 0)
            {
                throw new InvalidDataException("Release descriptor artifact size is invalid.");
            }
            if (kind == "resource-manifest")
            {
                manifests++;
            }
            artifacts.Add(new DescriptorArtifact(kind, key, hash, size));
        }
        if (artifacts.Count == 0 || manifests != 1)
        {
            throw new InvalidDataException("Release descriptor requires exactly one manifest.");
        }
        return new ReleaseDescriptor(channel, platform, version, minimum, maximum, artifacts);
    }

    private static bool IsNormalizedKey(string key)
    {
        return !key.StartsWith('/') && !key.Contains('\\') &&
            key.Split('/').All(segment => !string.IsNullOrEmpty(segment) && segment is not "." and not "..");
    }

    private static string NormalizeSha256(string value)
    {
        if (value.Length != 64 || value.Any(character => character is < '0' or > '9' and < 'a' or > 'f'))
        {
            throw new InvalidDataException("Release descriptor SHA-256 is invalid.");
        }
        return value;
    }

    private static JsonElement RequireProperty(JsonElement value, string name)
    {
        return value.TryGetProperty(name, out var property)
            ? property
            : throw new InvalidDataException("Release descriptor member is missing.");
    }

    private static string RequireString(JsonElement value, string name)
    {
        var property = RequireProperty(value, name);
        var text = property.ValueKind == JsonValueKind.String ? property.GetString() : null;
        if (string.IsNullOrWhiteSpace(text) || text.IndexOfAny(['\r', '\n']) >= 0)
        {
            throw new InvalidDataException("Release descriptor text member is invalid.");
        }
        return text;
    }

    private static long RequireInt64(JsonElement value, string name)
    {
        return RequireProperty(value, name).TryGetInt64(out var number)
            ? number
            : throw new InvalidDataException("Release descriptor integer member is invalid.");
    }

    private static void RequireObject(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Release descriptor value must be an object.");
        }
    }

    private static void RequireExact(JsonElement value, params string[] names)
    {
        var expected = new HashSet<string>(names, StringComparer.Ordinal);
        var actual = value.EnumerateObject().Select(property => property.Name).ToArray();
        if (actual.Length != expected.Count || actual.Any(name => !expected.Contains(name)))
        {
            throw new InvalidDataException("Release descriptor contains missing or unknown members.");
        }
    }
}

internal sealed record ReleaseDescriptor(
    string Channel,
    string Platform,
    string Version,
    long MinimumClientBuild,
    long MaximumClientBuild,
    IReadOnlyList<DescriptorArtifact> Artifacts);

internal sealed record DescriptorArtifact(string Kind, string Key, string Sha256, long SizeBytes);
