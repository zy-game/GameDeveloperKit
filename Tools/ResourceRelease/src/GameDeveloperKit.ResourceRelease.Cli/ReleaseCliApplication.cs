using System.Text.Json;

namespace GameDeveloperKit.ResourceRelease.Cli;

internal static class ReleaseCliApplication
{
    private const string SecretIdVariable = "GDK_COS_SECRET_ID";
    private const string SecretKeyVariable = "GDK_COS_SECRET_KEY";

    private static readonly string[] RequiredOptions =
    {
        "--report", "--output-root", "--minimum-client-build", "--maximum-client-build",
        "--region", "--bucket", "--result"
    };

    internal static async Task<int> RunAsync(
        string[] arguments,
        Func<string, string?> getEnvironmentVariable,
        Func<string, string, string, string, IResourceReleaseProvider> createProvider,
        TextWriter output,
        TextWriter error)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(arguments);
            ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
            ArgumentNullException.ThrowIfNull(createProvider);
            var options = Parse(arguments);
            var minimum = ParsePositiveInt64(options["--minimum-client-build"], "minimum client build");
            var maximum = ParsePositiveInt64(options["--maximum-client-build"], "maximum client build");
            var outputRoot = Path.GetFullPath(options["--output-root"]);
            var resultPath = RequireResultPath(options["--result"], outputRoot);
            var region = RequireText(options["--region"], "region");
            var bucket = RequireText(options["--bucket"], "bucket");
            var secretId = RequireSecret(getEnvironmentVariable(SecretIdVariable), SecretIdVariable);
            var secretKey = RequireSecret(getEnvironmentVariable(SecretKeyVariable), SecretKeyVariable);
            var plan = ReleasePlanBuilder.Build(
                options["--report"], outputRoot, minimum, maximum);
            var provider = createProvider(secretId, secretKey, region, bucket);
            var staged = await new ResourceReleaseService(provider).StageAsync(plan).ConfigureAwait(false);
            WriteResult(resultPath, staged);
            await output.WriteLineAsync($"Staged resource release {plan.Channel}/{plan.Platform}/{plan.Version}.")
                .ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync("Resource release stage failed: " + exception.Message)
                .ConfigureAwait(false);
            return 1;
        }
    }

    private static Dictionary<string, string> Parse(string[] arguments)
    {
        if (arguments.Length == 0 || !string.Equals(arguments[0], "stage", StringComparison.Ordinal))
        {
            throw new ArgumentException("Command must be 'stage'.");
        }
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < arguments.Length; index += 2)
        {
            if (index + 1 >= arguments.Length || !RequiredOptions.Contains(arguments[index], StringComparer.Ordinal) ||
                !options.TryAdd(arguments[index], RequireText(arguments[index + 1], arguments[index])))
            {
                throw new ArgumentException("CLI options are unknown, duplicated, or missing a value.");
            }
        }
        if (options.Count != RequiredOptions.Length || RequiredOptions.Any(option => !options.ContainsKey(option)))
        {
            throw new ArgumentException("CLI required options are incomplete.");
        }
        return options;
    }

    private static long ParsePositiveInt64(string value, string name)
    {
        return long.TryParse(value, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var result) && result > 0
            ? result
            : throw new ArgumentException(name + " must be a positive integer.");
    }

    private static string RequireResultPath(string value, string outputRoot)
    {
        value = Path.GetFullPath(RequireText(value, "result"));
        if (!string.Equals(Path.GetExtension(value), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Result path must use .json extension.");
        }
        var rootPrefix = outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!value.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Result path must remain inside output root.");
        }
        return value;
    }

    private static string RequireSecret(string? value, string name)
    {
        return RequireText(value, name);
    }

    private static string RequireText(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(['\r', '\n']) >= 0)
        {
            throw new ArgumentException(name + " must be non-empty single-line text.");
        }
        return value;
    }

    private static void WriteResult(string path, StagedReleaseResult result)
    {
        var value = new
        {
            schemaVersion = 1,
            status = "staged",
            channel = result.Plan.Channel,
            platform = result.Plan.Platform,
            version = result.Plan.Version,
            descriptorKey = result.DescriptorKey,
            descriptorSha256 = result.DescriptorSha256,
            uploadedObjectCount = result.UploadedObjectCount,
            reusedObjectCount = result.ReusedObjectCount
        };
        var directory = Path.GetDirectoryName(path) ?? throw new ArgumentException("Result directory is invalid.");
        Directory.CreateDirectory(directory);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                WriteIndented = true
            }) + "\n", new System.Text.UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
