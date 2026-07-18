using GameDeveloperKit.ResourceRelease.Cos;

namespace GameDeveloperKit.ResourceRelease.Cli;

internal static class Program
{
    private static int Main(string[] arguments)
    {
        return ReleaseCliApplication.RunAsync(
            arguments,
            Environment.GetEnvironmentVariable,
            static (secretId, secretKey, region, bucket) =>
                new CosReleaseProvider(secretId, secretKey, region, bucket),
            Console.Out,
            Console.Error).GetAwaiter().GetResult();
    }
}
