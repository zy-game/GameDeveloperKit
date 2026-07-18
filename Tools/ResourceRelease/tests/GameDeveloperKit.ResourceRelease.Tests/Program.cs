using System.Security.Cryptography;
using System.Text.Json;
using GameDeveloperKit.ResourceRelease;

namespace GameDeveloperKit.ResourceRelease.Tests;

internal static class Program
{
    private static int Main()
    {
        var tests = new Action[]
        {
            BuildPlan_ValidReportCreatesDeterministicResourcePlan,
            BuildPlan_RejectsMalformedEvidenceAndRange,
            Execute_UploadsVerifiesSignsAndReusesImmutableObjects,
            Execute_ConflictOrETagMismatchDoesNotReplacePointer,
            SigningPayload_MatchesRuntimeGoldenVectorAndSignatureVerifies
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

        Console.WriteLine($"Resource release tests: {tests.Length - failed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    private static void BuildPlan_ValidReportCreatesDeterministicResourcePlan()
    {
        using var fixture = new ReportFixture();
        var plan = ReleasePlanBuilder.Build(
            fixture.ReportPath,
            fixture.OutputRoot,
            100,
            199,
            "resource-prod-2026",
            "etag-1");

        Equal("dev", plan.Channel);
        Equal("Android", plan.Platform);
        Equal("1.2.3", plan.Version);
        Equal("dev/Android/publish.json", plan.PointerKey);
        Equal("etag-1", plan.ExpectedPointerETag);
        Equal(2, plan.Artifacts.Count);
        Equal("resource-artifact", plan.Artifacts[0].Kind);
        Equal("resource-manifest", plan.Artifacts[1].Kind);
        Equal(
            "dev/Android/1.2.3/resources/dev/Android/1.2.3/manifest.json",
            plan.Artifacts[1].RemoteKey);
    }

    private static void BuildPlan_RejectsMalformedEvidenceAndRange()
    {
        using var fixture = new ReportFixture();
        Throws<ArgumentOutOfRangeException>(() => ReleasePlanBuilder.Build(
            fixture.ReportPath, fixture.OutputRoot, 200, 199, "key", null));

        fixture.WriteReport(hashOverride: new string('0', 64));
        Throws<InvalidDataException>(() => ReleasePlanBuilder.Build(
            fixture.ReportPath, fixture.OutputRoot, 100, 199, "key", null));

        fixture.WriteReport(pathOverride: "../escape.json");
        Throws<InvalidDataException>(() => ReleasePlanBuilder.Build(
            fixture.ReportPath, fixture.OutputRoot, 100, 199, "key", null));

        fixture.WriteReport(includeUnknownProperty: true);
        Throws<InvalidDataException>(() => ReleasePlanBuilder.Build(
            fixture.ReportPath, fixture.OutputRoot, 100, 199, "key", null));
    }

    private static void Execute_UploadsVerifiesSignsAndReusesImmutableObjects()
    {
        using var fixture = new ReportFixture();
        var plan = fixture.BuildPlan(null);
        var provider = new FakeProvider();
        using var rsa = RSA.Create(2048);
        var privatePem = rsa.ExportPkcs8PrivateKeyPem();
        var service = new ResourceReleaseService(provider);

        var first = service.ExecuteAsync(plan, System.Text.Encoding.UTF8.GetBytes(privatePem)).GetAwaiter().GetResult();

        Equal(3, first.UploadedObjectCount);
        Equal(0, first.ReusedObjectCount);
        Equal("dev/Android/1.2.3/channel-release.json", first.DescriptorKey);
        Equal("dev/Android/publish.json", first.PointerKey);
        Equal(1, provider.PointerWrites);
        True(provider.Events[^1] == "conditional:dev/Android/publish.json");

        provider.RequiredPointerETag = provider.PointerETag;
        var retryPlan = fixture.BuildPlan(provider.PointerETag);
        var second = service.ExecuteAsync(retryPlan, System.Text.Encoding.UTF8.GetBytes(privatePem)).GetAwaiter().GetResult();
        Equal(0, second.UploadedObjectCount);
        Equal(3, second.ReusedObjectCount);
        Equal(2, provider.PointerWrites);
    }

    private static void Execute_ConflictOrETagMismatchDoesNotReplacePointer()
    {
        using var fixture = new ReportFixture();
        using var rsa = RSA.Create(2048);
        var privatePem = System.Text.Encoding.UTF8.GetBytes(rsa.ExportPkcs8PrivateKeyPem());

        var conflict = new FakeProvider();
        var first = fixture.BuildPlan(null).Artifacts[0];
        conflict.Seed(first.RemoteKey, new string('f', 64), first.SizeBytes, "conflict");
        Throws<InvalidDataException>(() =>
            new ResourceReleaseService(conflict).ExecuteAsync(fixture.BuildPlan(null), privatePem).GetAwaiter().GetResult());
        Equal(0, conflict.PointerWrites);

        var etag = new FakeProvider { RequiredPointerETag = "current-etag" };
        Throws<InvalidOperationException>(() =>
            new ResourceReleaseService(etag).ExecuteAsync(fixture.BuildPlan("stale-etag"), privatePem).GetAwaiter().GetResult());
        Equal(0, etag.PointerWrites);
        True(etag.Objects.Count > 0);
    }

    private static void SigningPayload_MatchesRuntimeGoldenVectorAndSignatureVerifies()
    {
        var hash = new string('a', 64);
        var payload = ResourceReleaseService.BuildSigningPayload(
            1, "dev", "Android", "1.2.3", hash.ToUpperInvariant(), 100, 199);
        var expected = System.Text.Encoding.UTF8.GetBytes(
            "gdk-resource-publish-v1\n1\ndev\nAndroid\n1.2.3\n" + hash + "\n100\n199");
        True(payload.SequenceEqual(expected));

        using var fixture = new ReportFixture();
        using var rsa = RSA.Create(2048);
        var pointer = ResourceReleaseService.CreateSignedPointer(
            fixture.BuildPlan(null),
            fixture.ManifestHash,
            System.Text.Encoding.UTF8.GetBytes(rsa.ExportPkcs8PrivateKeyPem()));
        True(rsa.VerifyData(
            ResourceReleaseService.BuildSigningPayload(
                pointer.ProtocolVersion,
                pointer.Channel,
                pointer.Platform,
                pointer.Version,
                pointer.ManifestSha256,
                pointer.MinimumClientBuild,
                pointer.MaximumClientBuild),
            Convert.FromBase64String(pointer.Signature),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
        Throws<ArgumentException>(() => ResourceReleaseService.CreateSignedPointer(
            fixture.BuildPlan(null), fixture.ManifestHash, "invalid"u8));
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (EqualityComparer<T>.Default.Equals(expected, actual) is false)
        {
            throw new InvalidOperationException($"Expected '{expected}', actual '{actual}'.");
        }
    }

    private static void True(bool value)
    {
        if (value is false)
        {
            throw new InvalidOperationException("Expected true.");
        }
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException("Expected exception " + typeof(T).Name + ".");
    }

    private sealed class ReportFixture : IDisposable
    {
        internal ReportFixture()
        {
            OutputRoot = Path.Combine(Path.GetTempPath(), "gdk-release-tests-" + Guid.NewGuid().ToString("N"));
            var resourceRoot = Path.Combine(OutputRoot, "resources", "dev", "Android", "1.2.3");
            Directory.CreateDirectory(resourceRoot);
            ManifestPath = Path.Combine(resourceRoot, "manifest.json");
            BundlePath = Path.Combine(resourceRoot, "bundle.bin");
            File.WriteAllText(ManifestPath, "manifest");
            File.WriteAllText(BundlePath, "bundle");
            ReportPath = Path.Combine(OutputRoot, "channel-build-report.json");
            WriteReport();
        }

        internal string OutputRoot { get; }
        internal string ReportPath { get; }
        internal string ManifestHash => Sha256(ManifestPath);
        private string ManifestPath { get; }
        private string BundlePath { get; }

        internal void WriteReport(
            string? hashOverride = null,
            string? pathOverride = null,
            bool includeUnknownProperty = false)
        {
            var manifest = Artifact("resource-manifest", ManifestPath, hashOverride, pathOverride);
            var bundle = Artifact("resource-artifact", BundlePath, null, null);
            var report = new Dictionary<string, object?>
            {
                ["schemaVersion"] = 1,
                ["status"] = "succeeded",
                ["failureKind"] = "none",
                ["exitCode"] = 0,
                ["context"] = new Dictionary<string, object>
                {
                    ["channel"] = "dev", ["platform"] = "Android", ["version"] = "1.2.3"
                },
                ["ci"] = null,
                ["artifacts"] = new[] { manifest, bundle },
                ["steps"] = Array.Empty<object>(),
                ["warnings"] = Array.Empty<object>(),
                ["startedAtUtc"] = "2026-07-18T00:00:00.0000000Z",
                ["finishedAtUtc"] = "2026-07-18T00:00:01.0000000Z"
            };
            if (includeUnknownProperty)
            {
                report["unknown"] = true;
            }
            File.WriteAllText(ReportPath, JsonSerializer.Serialize(report));
        }

        internal ReleasePlan BuildPlan(string? expectedETag)
        {
            WriteReport();
            return ReleasePlanBuilder.Build(ReportPath, OutputRoot, 100, 199, "key-2026", expectedETag);
        }

        public void Dispose()
        {
            if (Directory.Exists(OutputRoot))
            {
                Directory.Delete(OutputRoot, true);
            }
        }

        private Dictionary<string, object> Artifact(
            string kind,
            string path,
            string? hashOverride,
            string? pathOverride)
        {
            return new Dictionary<string, object>
            {
                ["kind"] = kind,
                ["path"] = pathOverride ?? Path.GetRelativePath(OutputRoot, path).Replace('\\', '/'),
                ["sha256"] = hashOverride ?? Sha256(path),
                ["sizeBytes"] = new FileInfo(path).Length
            };
        }

        private static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }

    private sealed class FakeProvider : IResourceReleaseProvider
    {
        internal Dictionary<string, RemoteObjectInfo> Objects { get; } = new(StringComparer.Ordinal);
        internal List<string> Events { get; } = new();
        internal string? RequiredPointerETag { get; set; }
        internal string? PointerETag { get; private set; }
        internal int PointerWrites { get; private set; }

        public Task<RemoteObjectInfo> HeadAsync(string key, CancellationToken cancellationToken)
        {
            Events.Add("head:" + key);
            return Task.FromResult(Objects.TryGetValue(key, out var value)
                ? value
                : new RemoteObjectInfo(key, false, null, 0, null));
        }

        public Task<RemoteObjectInfo> PutImmutableAsync(ReleaseObject item, CancellationToken cancellationToken)
        {
            Events.Add("put:" + item.Key);
            if (Objects.TryGetValue(item.Key, out var existing))
            {
                if (existing.Sha256 != item.Sha256 || existing.SizeBytes != item.SizeBytes)
                {
                    throw new InvalidOperationException("immutable conflict");
                }
                return Task.FromResult(existing);
            }
            var value = new RemoteObjectInfo(item.Key, true, item.Sha256, item.SizeBytes, "etag-" + Objects.Count);
            Objects.Add(item.Key, value);
            return Task.FromResult(value);
        }

        public Task<string?> ReadTextAsync(string key, CancellationToken cancellationToken)
        {
            Events.Add("read:" + key);
            return Task.FromResult<string?>(null);
        }

        public Task<RemoteObjectInfo> PutTextConditionalAsync(
            string key,
            string content,
            string? expectedETag,
            CancellationToken cancellationToken)
        {
            Events.Add("conditional:" + key);
            if (RequiredPointerETag != expectedETag)
            {
                throw new InvalidOperationException("etag mismatch");
            }
            PointerWrites++;
            PointerETag = "pointer-" + PointerWrites;
            return Task.FromResult(new RemoteObjectInfo(
                key,
                true,
                Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content))).ToLowerInvariant(),
                System.Text.Encoding.UTF8.GetByteCount(content),
                PointerETag));
        }

        internal void Seed(string key, string hash, long size, string etag)
        {
            Objects[key] = new RemoteObjectInfo(key, true, hash, size, etag);
        }
    }
}
