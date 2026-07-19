using System.Security.Cryptography;
using System.Text.Json;

namespace GameDeveloperKit.ResourceRelease.Cli.Tests;

internal static class Program
{
    private static int Main()
    {
        var tests = new Action[]
        {
            Stage_WritesStrictEvidenceWithoutPointer,
            Stage_RejectsMissingSecretBeforeProviderCreation,
            Stage_RejectsUnknownOrIncompleteOptions,
            Stage_RejectsResultOutsideOutputRoot,
            Promote_WritesStrictEvidenceAndPointer,
            Promote_RejectsMissingOrInvalidSigningKey
        };
        var failed = 0;
        foreach (var test in tests)
        {
            try
            {
                test();
                Console.WriteLine("PASS " + test.Method.Name);
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine("FAIL " + test.Method.Name + ": " + exception);
            }
        }
        Console.WriteLine($"Resource release CLI tests: {tests.Length - failed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    private static void Stage_WritesStrictEvidenceWithoutPointer()
    {
        using var fixture = new Fixture();
        var provider = new FakeProvider();
        var output = new StringWriter();
        var error = new StringWriter();
        var created = 0;

        var exitCode = ReleaseCliApplication.RunAsync(
            fixture.Arguments,
            name => name == "GDK_COS_SECRET_ID" ? "secret-id-value" : "secret-key-value",
            (secretId, secretKey, region, bucket) =>
            {
                Equal("secret-id-value", secretId);
                Equal("secret-key-value", secretKey);
                Equal("ap-test", region);
                Equal("bucket-123", bucket);
                created++;
                return provider;
            },
            output,
            error).GetAwaiter().GetResult();

        Equal(0, exitCode);
        Equal(1, created);
        Equal(0, provider.PointerWrites);
        Equal(string.Empty, error.ToString());
        True(output.ToString().Contains("dev/Android/1.2.3", StringComparison.Ordinal));
        var secretOutput = output + error.ToString() + File.ReadAllText(fixture.ResultPath);
        True(!secretOutput.Contains("secret-id-value", StringComparison.Ordinal));
        True(!secretOutput.Contains("secret-key-value", StringComparison.Ordinal));

        using var document = JsonDocument.Parse(File.ReadAllBytes(fixture.ResultPath));
        var root = document.RootElement;
        Equal(9, root.EnumerateObject().Count());
        Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Equal("staged", root.GetProperty("status").GetString());
        Equal("dev", root.GetProperty("channel").GetString());
        Equal("Android", root.GetProperty("platform").GetString());
        Equal("1.2.3", root.GetProperty("version").GetString());
        Equal(3, root.GetProperty("uploadedObjectCount").GetInt32());
        Equal(0, root.GetProperty("reusedObjectCount").GetInt32());
        Equal(64, root.GetProperty("descriptorSha256").GetString()!.Length);
    }

    private static void Stage_RejectsMissingSecretBeforeProviderCreation()
    {
        using var fixture = new Fixture();
        var created = 0;
        var error = new StringWriter();
        var exitCode = ReleaseCliApplication.RunAsync(
            fixture.Arguments,
            _ => null,
            (_, _, _, _) =>
            {
                created++;
                return new FakeProvider();
            },
            TextWriter.Null,
            error).GetAwaiter().GetResult();

        Equal(1, exitCode);
        Equal(0, created);
        True(!File.Exists(fixture.ResultPath));
        True(!error.ToString().Contains("secret-id-value", StringComparison.Ordinal));
    }

    private static void Stage_RejectsUnknownOrIncompleteOptions()
    {
        var created = 0;
        foreach (var arguments in new[]
        {
            Array.Empty<string>(),
            new[] { "promote" },
            new[] { "stage", "--unknown", "value" },
            new[] { "stage", "--report" }
        })
        {
            var exitCode = ReleaseCliApplication.RunAsync(
                arguments,
                _ => "secret",
                (_, _, _, _) =>
                {
                    created++;
                    return new FakeProvider();
                },
                TextWriter.Null,
                TextWriter.Null).GetAwaiter().GetResult();
            Equal(1, exitCode);
        }
        Equal(0, created);
    }

    private static void Stage_RejectsResultOutsideOutputRoot()
    {
        using var fixture = new Fixture();
        var arguments = fixture.Arguments.ToArray();
        arguments[^1] = Path.Combine(Path.GetDirectoryName(fixture.Root)!, "escaped-result.json");
        var created = 0;
        var exitCode = ReleaseCliApplication.RunAsync(
            arguments,
            _ => "secret",
            (_, _, _, _) =>
            {
                created++;
                return new FakeProvider();
            },
            TextWriter.Null,
            TextWriter.Null).GetAwaiter().GetResult();

        Equal(1, exitCode);
        Equal(0, created);
    }

    private static void Promote_WritesStrictEvidenceAndPointer()
    {
        using var fixture = new Fixture();
        var provider = new FakeProvider();
        Equal(0, Run(fixture.Arguments, fixture, provider));
        using var rsa = RSA.Create(2048);
        File.WriteAllText(fixture.SigningKeyPath, rsa.ExportPkcs8PrivateKeyPem());

        var exitCode = Run(fixture.PromoteArguments, fixture, provider);

        Equal(0, exitCode);
        Equal(1, provider.PointerWrites);
        True(!File.ReadAllText(fixture.PromotionResultPath).Contains("PRIVATE KEY", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(File.ReadAllBytes(fixture.PromotionResultPath));
        var root = document.RootElement;
        Equal(8, root.EnumerateObject().Count());
        Equal("promoted", root.GetProperty("status").GetString());
        Equal("dev", root.GetProperty("channel").GetString());
        Equal("Android", root.GetProperty("platform").GetString());
        Equal("1.2.3", root.GetProperty("version").GetString());
        Equal("dev/Android/publish.json", root.GetProperty("pointerKey").GetString());
        True(rsa.VerifyData(
            provider.PointerPayload!,
            provider.PointerSignature!,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
    }

    private static void Promote_RejectsMissingOrInvalidSigningKey()
    {
        using var fixture = new Fixture();
        var provider = new FakeProvider();
        Equal(0, Run(fixture.Arguments, fixture, provider));

        Equal(1, Run(fixture.PromoteArguments, fixture, provider));
        Equal(0, provider.PointerWrites);
        File.WriteAllText(fixture.SigningKeyPath, "not-a-private-key");
        Equal(1, Run(fixture.PromoteArguments, fixture, provider));
        Equal(0, provider.PointerWrites);
    }

    private static int Run(string[] arguments, Fixture fixture, FakeProvider provider)
    {
        return ReleaseCliApplication.RunAsync(
            arguments,
            name => name == "GDK_COS_SECRET_ID" ? "secret-id-value" : "secret-key-value",
            (_, _, _, _) => provider,
            TextWriter.Null,
            TextWriter.Null).GetAwaiter().GetResult();
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', actual '{actual}'.");
        }
    }

    private static void True(bool value)
    {
        if (!value)
        {
            throw new InvalidOperationException("Expected true.");
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal Fixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "gdk-release-cli-" + Guid.NewGuid().ToString("N"));
            var resourceRoot = Path.Combine(Root, "resources", "dev", "Android", "1.2.3");
            Directory.CreateDirectory(resourceRoot);
            var manifest = Path.Combine(resourceRoot, "manifest.json");
            var bundle = Path.Combine(resourceRoot, "bundle.bin");
            File.WriteAllText(manifest, "manifest");
            File.WriteAllText(bundle, "bundle");
            var report = Path.Combine(Root, "channel-build-report.json");
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                status = "succeeded",
                failureKind = "none",
                exitCode = 0,
                context = new { channel = "dev", platform = "Android", version = "1.2.3" },
                ci = (object?)null,
                artifacts = new[]
                {
                    Artifact("resource-manifest", manifest),
                    Artifact("resource-artifact", bundle)
                },
                steps = Array.Empty<object>(),
                warnings = Array.Empty<object>(),
                startedAtUtc = "2026-07-18T00:00:00.0000000Z",
                finishedAtUtc = "2026-07-18T00:00:01.0000000Z"
            }));
            ResultPath = Path.Combine(Root, "staged-release.json");
            SigningKeyPath = Path.Combine(Root, "signing-key.pem");
            PromotionResultPath = Path.Combine(Root, "promotion-result.json");
            Arguments = new[]
            {
                "stage", "--report", report, "--output-root", Root,
                "--minimum-client-build", "100", "--maximum-client-build", "199",
                "--region", "ap-test", "--bucket", "bucket-123", "--result", ResultPath
            };
            PromoteArguments = new[]
            {
                "promote", "--channel", "dev", "--platform", "Android", "--version", "1.2.3",
                "--region", "ap-test", "--bucket", "bucket-123", "--key-id", "key-2026",
                "--signing-key-file", SigningKeyPath, "--result", PromotionResultPath
            };
        }

        internal string Root { get; }
        internal string ResultPath { get; }
        internal string SigningKeyPath { get; }
        internal string PromotionResultPath { get; }
        internal string[] Arguments { get; }
        internal string[] PromoteArguments { get; }

        public void Dispose()
        {
            Directory.Delete(Root, true);
        }

        private object Artifact(string kind, string path)
        {
            return new
            {
                kind,
                path = Path.GetRelativePath(Root, path).Replace('\\', '/'),
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
                sizeBytes = new FileInfo(path).Length
            };
        }
    }

    private sealed class FakeProvider : IResourceReleaseProvider
    {
        private readonly Dictionary<string, RemoteObjectInfo> m_Objects = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> m_TextObjects = new(StringComparer.Ordinal);
        internal int PointerWrites { get; private set; }
        internal byte[]? PointerPayload { get; private set; }
        internal byte[]? PointerSignature { get; private set; }

        public Task<RemoteObjectInfo> HeadAsync(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(m_Objects.TryGetValue(key, out var value)
                ? value
                : new RemoteObjectInfo(key, false, null, 0, null));
        }

        public Task<RemoteObjectInfo> PutImmutableAsync(ReleaseObject item, CancellationToken cancellationToken)
        {
            var value = new RemoteObjectInfo(item.Key, true, item.Sha256, item.SizeBytes, "etag");
            m_Objects[item.Key] = value;
            if (item.ContentType == "application/json")
            {
                m_TextObjects[item.Key] = File.ReadAllText(item.LocalPath);
            }
            return Task.FromResult(value);
        }

        public Task<string?> ReadTextAsync(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(m_TextObjects.TryGetValue(key, out var value) ? value : null);
        }

        public Task<RemoteObjectInfo> PutTextConditionalAsync(
            string key,
            string content,
            string? expectedETag,
            CancellationToken cancellationToken)
        {
            PointerWrites++;
            using var document = JsonDocument.Parse(content);
            var pointer = document.RootElement;
            PointerPayload = ResourceReleaseService.BuildSigningPayload(
                pointer.GetProperty("protocolVersion").GetInt32(),
                pointer.GetProperty("channel").GetString()!,
                pointer.GetProperty("platform").GetString()!,
                pointer.GetProperty("version").GetString()!,
                pointer.GetProperty("manifestSha256").GetString()!,
                pointer.GetProperty("minimumClientBuild").GetInt64(),
                pointer.GetProperty("maximumClientBuild").GetInt64());
            PointerSignature = Convert.FromBase64String(pointer.GetProperty("signature").GetString()!);
            var etag = "pointer-" + PointerWrites;
            m_Objects[key] = new RemoteObjectInfo(
                key,
                true,
                Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content))).ToLowerInvariant(),
                System.Text.Encoding.UTF8.GetByteCount(content),
                etag);
            return Task.FromResult(m_Objects[key]);
        }
    }
}
