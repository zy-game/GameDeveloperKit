using System.IO;
using GameDeveloperKit.Debugger;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace GameDeveloperKit.Tests
{
    public abstract class RuntimeTestBase
    {
        private const string FrameworkAssetsRoot = "Assets/GameDeveloperKit";
        private const string FrameworkPackageRoot = "Packages/com.gamedeveloperkit.framework";

        [SetUp]
        public void RuntimeTestBaseSetUp()
        {
            var test = TestContext.CurrentContext.Test;
            LogTestMessage($"[TEST START] {test.ClassName}.{test.MethodName}", false);
        }

        [TearDown]
        public void RuntimeTestBaseTearDown()
        {
            var context = TestContext.CurrentContext;
            var test = context.Test;
            var result = context.Result;
            var status = result.Outcome.Status;
            var message = string.IsNullOrEmpty(result.Message) ? string.Empty : $" - {result.Message}";
            if (status == TestStatus.Passed)
            {
                LogTestMessage($"[TEST END] {test.ClassName}.{test.MethodName}: {result.Outcome}{message}", false);
                return;
            }

            LogTestMessage($"[TEST END] {test.ClassName}.{test.MethodName}: {result.Outcome}{message}", true);
        }

        private static void LogTestMessage(string message, bool warning)
        {
            if (!App.TryGetRegistered<DebugModule>(out var debug))
            {
                TestContext.Progress.WriteLine(message);
                return;
            }

            if (warning)
            {
                debug.Warning(message);
                return;
            }

            debug.Info(message);
        }

        protected static string FrameworkAssetPath(string relativePath)
        {
            return $"{ResolveFrameworkAssetRoot()}/{NormalizeRelativePath(relativePath)}";
        }

        protected static string ResolveFrameworkAssetPath(string path)
        {
            var normalizedPath = NormalizePath(path);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return normalizedPath;
            }

            const string assetsRootWithSlash = FrameworkAssetsRoot + "/";
            if (normalizedPath.StartsWith(assetsRootWithSlash, System.StringComparison.Ordinal))
            {
                return FrameworkAssetPath(normalizedPath.Substring(assetsRootWithSlash.Length));
            }

            const string packageRootWithSlash = FrameworkPackageRoot + "/";
            if (normalizedPath.StartsWith(packageRootWithSlash, System.StringComparison.Ordinal))
            {
                return FrameworkAssetPath(normalizedPath.Substring(packageRootWithSlash.Length));
            }

            return normalizedPath;
        }

        protected static string FrameworkFilePath(string relativePath)
        {
            var normalizedRelativePath = NormalizeRelativePath(relativePath);
#if UNITY_EDITOR
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(App).Assembly);
            if (string.IsNullOrWhiteSpace(packageInfo?.resolvedPath) is false)
            {
                var packageFilePath = Path.Combine(packageInfo.resolvedPath, normalizedRelativePath);
                if (System.IO.File.Exists(packageFilePath) || Directory.Exists(packageFilePath))
                {
                    return NormalizePath(packageFilePath);
                }
            }
#endif

            var assetsFilePath = Path.Combine(FrameworkAssetsRoot, normalizedRelativePath);
            if (System.IO.File.Exists(assetsFilePath) || Directory.Exists(assetsFilePath))
            {
                return NormalizePath(assetsFilePath);
            }

            return NormalizePath(Path.Combine(FrameworkPackageRoot, normalizedRelativePath));
        }

        private static string ResolveFrameworkAssetRoot()
        {
#if UNITY_EDITOR
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(App).Assembly);
            if (string.IsNullOrWhiteSpace(packageInfo?.assetPath) is false)
            {
                return NormalizePath(packageInfo.assetPath);
            }

            if (UnityEditor.AssetDatabase.IsValidFolder(FrameworkPackageRoot))
            {
                return FrameworkPackageRoot;
            }
#endif

            return FrameworkAssetsRoot;
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            return NormalizePath(relativePath).Trim('/');
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }
}
