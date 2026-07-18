using System.Text;
using GameDeveloperKit.ResourceRelease;
using GameDeveloperKit.ResourceRelease.Cos;

namespace GameDeveloperKit.ResourceRelease.Cos.Tests;

internal static class Program
{
    private static int Main()
    {
        var tests = new Action[]
        {
            Head_MapsMetadataAndNotFound,
            PutImmutable_SendsRequiredHeadersAndMapsConflict,
            PutTextConditional_UsesCreateOrMatchCondition,
            ReadText_MapsContentAndNotFound,
            Inputs_AreRejectedBeforeGatewayCalls
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

        Console.WriteLine($"COS release adapter tests: {tests.Length - failed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    private static void Head_MapsMetadataAndNotFound()
    {
        var gateway = new FakeGateway
        {
            HeadResult = Result(12, "\"etag-1\"", "ABCDEF")
        };
        var provider = new CosReleaseProvider(gateway);

        var result = provider.HeadAsync("dev/Android/publish.json", default).GetAwaiter().GetResult();

        True(result.Exists);
        Equal(12L, result.SizeBytes);
        Equal("ABCDEF", result.Sha256);
        Equal("etag-1", result.ETag);
        gateway.HeadException = new CosReleaseProvider.CosGatewayException(404, "missing");
        result = provider.HeadAsync("dev/Android/missing.json", default).GetAwaiter().GetResult();
        True(result.Exists is false);
        Equal<string?>(null, result.ETag);
        gateway.HeadException = null;
        gateway.HeadResult = Result(0, null, null);
        result = provider.HeadAsync("dev/Android/no-etag.json", default).GetAwaiter().GetResult();
        Equal<string?>(null, result.ETag);
    }

    private static void PutImmutable_SendsRequiredHeadersAndMapsConflict()
    {
        using var file = new TemporaryFile("resource-data");
        var gateway = new FakeGateway { PutResult = Result(file.Size, "etag-2", new string('a', 64)) };
        var provider = new CosReleaseProvider(gateway);
        var item = new ReleaseObject(
            "dev/Android/1.0/resource.bundle",
            file.Path,
            new string('a', 64),
            file.Size,
            "application/octet-stream");

        var result = provider.PutImmutableAsync(item, default).GetAwaiter().GetResult();

        Equal("*", gateway.LastHeaders[CosReleaseProvider.IfNoneMatchHeader]);
        Equal(item.Sha256, gateway.LastHeaders[CosReleaseProvider.HashMetadataHeader]);
        Equal(item.ContentType, gateway.LastHeaders[CosReleaseProvider.ContentTypeHeader]);
        Equal("etag-2", result.ETag);
        gateway.PutException = new CosReleaseProvider.CosGatewayException(412, "conflict");
        Throws<InvalidOperationException>(() =>
            provider.PutImmutableAsync(item, default).GetAwaiter().GetResult());
    }

    private static void PutTextConditional_UsesCreateOrMatchCondition()
    {
        var gateway = new FakeGateway { PutResult = Result(2, "etag-3", null) };
        var provider = new CosReleaseProvider(gateway);

        provider.PutTextConditionalAsync("dev/Android/publish.json", "{}", null, default)
            .GetAwaiter().GetResult();
        Equal("*", gateway.LastHeaders[CosReleaseProvider.IfNoneMatchHeader]);
        True(gateway.LastHeaders.ContainsKey(CosReleaseProvider.IfMatchHeader) is false);
        Equal("application/json", gateway.LastHeaders[CosReleaseProvider.ContentTypeHeader]);

        provider.PutTextConditionalAsync("dev/Android/publish.json", "{}", "etag-current", default)
            .GetAwaiter().GetResult();
        Equal("etag-current", gateway.LastHeaders[CosReleaseProvider.IfMatchHeader]);
        True(gateway.LastHeaders.ContainsKey(CosReleaseProvider.IfNoneMatchHeader) is false);
        gateway.PutException = new CosReleaseProvider.CosGatewayException(412, "stale");
        Throws<InvalidOperationException>(() => provider.PutTextConditionalAsync(
            "dev/Android/publish.json", "{}", "etag-stale", default).GetAwaiter().GetResult());
    }

    private static void ReadText_MapsContentAndNotFound()
    {
        var gateway = new FakeGateway { Bytes = Encoding.UTF8.GetBytes("{\"version\":1}") };
        var provider = new CosReleaseProvider(gateway);

        Equal("{\"version\":1}", provider.ReadTextAsync("dev/Android/publish.json", default)
            .GetAwaiter().GetResult());
        gateway.GetException = new CosReleaseProvider.CosGatewayException(404, "missing");
        Equal<string?>(null, provider.ReadTextAsync("dev/Android/missing.json", default)
            .GetAwaiter().GetResult());
    }

    private static void Inputs_AreRejectedBeforeGatewayCalls()
    {
        Throws<ArgumentException>(() => new CosReleaseProvider("", "secret", "region", "bucket"));
        Throws<ArgumentException>(() => new CosReleaseProvider("id", "", "region", "bucket"));
        Throws<ArgumentException>(() => new CosReleaseProvider("id", "secret", "", "bucket"));
        Throws<ArgumentException>(() => new CosReleaseProvider("id", "secret", "region", ""));

        var gateway = new FakeGateway();
        var provider = new CosReleaseProvider(gateway);
        Throws<ArgumentException>(() => provider.HeadAsync("../escape", default).GetAwaiter().GetResult());
        Throws<ArgumentException>(() => provider.PutTextConditionalAsync(
            "/absolute", "{}", null, default).GetAwaiter().GetResult());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Throws<OperationCanceledException>(() => provider.ReadTextAsync(
            "valid/key", cancellation.Token).GetAwaiter().GetResult());
        Equal(0, gateway.CallCount);
    }

    private static CosReleaseProvider.CosObjectResult Result(long size, string? etag, string? hash)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (hash != null)
        {
            headers[CosReleaseProvider.HashMetadataHeader] = hash;
        }
        return new CosReleaseProvider.CosObjectResult(size, etag, headers);
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

    private sealed class FakeGateway : CosReleaseProvider.ICosGateway
    {
        internal CosReleaseProvider.CosObjectResult HeadResult { get; set; } = Result(0, null, null);
        internal CosReleaseProvider.CosObjectResult PutResult { get; set; } = Result(0, null, null);
        internal CosReleaseProvider.CosGatewayException? HeadException { get; set; }
        internal CosReleaseProvider.CosGatewayException? PutException { get; set; }
        internal CosReleaseProvider.CosGatewayException? GetException { get; set; }
        internal byte[] Bytes { get; set; } = Array.Empty<byte>();
        internal IReadOnlyDictionary<string, string> LastHeaders { get; private set; } =
            new Dictionary<string, string>();
        internal int CallCount { get; private set; }

        public CosReleaseProvider.CosObjectResult Head(string key)
        {
            CallCount++;
            if (HeadException != null)
            {
                throw HeadException;
            }
            return HeadResult;
        }

        public CosReleaseProvider.CosObjectResult PutFile(
            string key,
            string path,
            IReadOnlyDictionary<string, string> headers)
        {
            return Put(headers);
        }

        public CosReleaseProvider.CosObjectResult PutBytes(
            string key,
            byte[] bytes,
            IReadOnlyDictionary<string, string> headers)
        {
            return Put(headers);
        }

        public byte[] GetBytes(string key)
        {
            CallCount++;
            if (GetException != null)
            {
                throw GetException;
            }
            return Bytes;
        }

        private CosReleaseProvider.CosObjectResult Put(IReadOnlyDictionary<string, string> headers)
        {
            CallCount++;
            LastHeaders = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
            if (PutException != null)
            {
                throw PutException;
            }
            return PutResult;
        }
    }

    private sealed class TemporaryFile : IDisposable
    {
        internal TemporaryFile(string content)
        {
            Path = System.IO.Path.GetTempFileName();
            File.WriteAllText(Path, content);
            Size = new FileInfo(Path).Length;
        }

        internal string Path { get; }
        internal long Size { get; }

        public void Dispose()
        {
            File.Delete(Path);
        }
    }
}
