using System.Text.Json;

namespace GameDeveloperKit.ResourceRelease.Cli;

internal static class ReleaseCliApplication
{
    private const string SecretIdVariable = "GDK_COS_SECRET_ID";
    private const string SecretKeyVariable = "GDK_COS_SECRET_KEY";

    private static readonly string[] StageOptions =
    {
        "--report", "--output-root", "--minimum-client-build", "--maximum-client-build",
        "--region", "--bucket", "--result"
    };

    private static readonly string[] PromoteOptions =
    {
        "--channel", "--platform", "--version", "--region", "--bucket", "--key-id",
        "--signing-key-file", "--result"
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
            if (arguments.Length == 0)
            {
                throw new ArgumentException("A command is required.");
            }
            return arguments[0] switch
            {
                "stage" => await StageAsync(
                    arguments, getEnvironmentVariable, createProvider, output).ConfigureAwait(false),
                "promote" => await PromoteAsync(
                    arguments, getEnvironmentVariable, createProvider, output).ConfigureAwait(false),
                _ => throw new ArgumentException("Command must be 'stage' or 'promote'.")
            };
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync("Resource release command failed: " + exception.Message)
                .ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<int> StageAsync(
        string[] arguments,
        Func<string, string?> getEnvironmentVariable,
        Func<string, string, string, string, IResourceReleaseProvider> createProvider,
        TextWriter output)
    {
            var options = Parse(arguments, "stage", StageOptions);
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

    private static async Task<int> PromoteAsync(
        string[] arguments,
        Func<string, string?> getEnvironmentVariable,
        Func<string, string, string, string, IResourceReleaseProvider> createProvider,
        TextWriter output)
    {
        var options = Parse(arguments, "promote", PromoteOptions);
        var resultPath = RequireJsonPath(options["--result"], "result");
        var signingKeyPath = RequireFile(options["--signing-key-file"], "signing key file");
        var privateKeyPem = await File.ReadAllBytesAsync(signingKeyPath).ConfigureAwait(false);
        var secretId = RequireSecret(getEnvironmentVariable(SecretIdVariable), SecretIdVariable);
        var secretKey = RequireSecret(getEnvironmentVariable(SecretKeyVariable), SecretKeyVariable);
        var provider = createProvider(
            secretId,
            secretKey,
            RequireText(options["--region"], "region"),
            RequireText(options["--bucket"], "bucket"));
        var result = await new ResourceReleaseService(provider).PromoteAsync(
            options["--channel"],
            options["--platform"],
            options["--version"],
            options["--key-id"],
            privateKeyPem).ConfigureAwait(false);
        WriteResult(resultPath, new
        {
            schemaVersion = 1,
            status = "promoted",
            result.Channel,
            result.Platform,
            result.Version,
            result.DescriptorKey,
            result.PointerKey,
            result.PointerETag
        });
        await output.WriteLineAsync($"Promoted resource release {result.Channel}/{result.Platform}/{result.Version}.")
            .ConfigureAwait(false);
        return 0;
    }

    private static Dictionary<string, string> Parse(
        string[] arguments,
        string command,
        IReadOnlyCollection<string> requiredOptions)
    {
        if (!string.Equals(arguments[0], command, StringComparison.Ordinal))
        {
            throw new ArgumentException("CLI command is invalid.");
        }
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < arguments.Length; index += 2)
        {
            if (index + 1 >= arguments.Length || !requiredOptions.Contains(arguments[index]) ||
                !options.TryAdd(arguments[index], RequireText(arguments[index + 1], arguments[index])))
            {
                throw new ArgumentException("CLI options are unknown, duplicated, or missing a value.");
            }
        }
        if (options.Count != requiredOptions.Count || requiredOptions.Any(option => !options.ContainsKey(option)))
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
        value = RequireJsonPath(value, "result");
        var rootPrefix = outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!value.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Result path must remain inside output root.");
        }
        return value;
    }

    private static string RequireJsonPath(string value, string name)
    {
        value = Path.GetFullPath(RequireText(value, name));
        if (!string.Equals(Path.GetExtension(value), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(name + " must use .json extension.");
        }
        return value;
    }

    private static string RequireFile(string value, string name)
    {
        value = Path.GetFullPath(RequireText(value, name));
        if (!File.Exists(value))
        {
            throw new FileNotFoundException(name + " does not exist.", value);
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
        WriteResult(path, new
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
        });
    }

    private static void WriteResult(string path, object value)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new ArgumentException("Result directory is invalid.");
        Directory.CreateDirectory(directory);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
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
