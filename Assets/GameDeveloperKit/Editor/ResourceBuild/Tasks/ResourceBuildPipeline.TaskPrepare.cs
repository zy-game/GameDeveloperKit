using System.IO;
using GameDeveloperKit.Runtime;
using UnityEngine;

namespace GameDeveloperKit.Editor
{
    internal sealed partial class ResourceBuildPipeline
    {
        private sealed class TaskPrepare : ISbpBuildTask
        {
            public string TaskName => "Prepare";

            public ResourceBuildTaskResult Run(ResourceBuildPipelineContext context)
            {
                var request = context.Request;
                var package = request.Package;
                if (request.Settings == null)
                {
                    return ResourceBuildTaskResult.Failed("Resource settings are unavailable.");
                }

                if (package == null)
                {
                    return ResourceBuildTaskResult.Failed("Package is null.");
                }

                if (string.IsNullOrWhiteSpace(package.PackageName))
                {
                    return ResourceBuildTaskResult.Failed("Package name can not be empty.");
                }

                ResourceCollectionService.NormalizePackage(package);
                package.Version = string.IsNullOrWhiteSpace(package.Version) ? "1.0.0" : package.Version;
                context.PackageName = package.PackageName;
                context.PackageVersion = package.Version;

                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var packageFolder = NormalizeToken(package.PackageName);
                var versionFolder = NormalizeToken(package.Version);
                context.BuildRoot = Path.Combine(projectRoot, "Library", "GameDeveloperKit", "ResourceBuild", packageFolder, versionFolder);
                context.BundleOutputRoot = Path.Combine(context.BuildRoot, "bundles");
                context.PackageManifestPath = Path.Combine(context.BuildRoot, package.ResolveManifestRelativePath().Replace('/', Path.DirectorySeparatorChar));
                context.GlobalManifestPath = Path.Combine(projectRoot, "Library", "GameDeveloperKit", "ResourceBuild", "manifest.json");
                context.HistoryRoot = Path.Combine(projectRoot, "Library", "GameDeveloperKit", "ResourceBuild", "BuildHistory");
                context.ReportPath = Path.Combine(context.BuildRoot, "build-report.json");
                context.ReportTextPath = Path.Combine(context.BuildRoot, "build-report.txt");

                if (request.ForceRebuild && Directory.Exists(context.BuildRoot))
                {
                    Directory.Delete(context.BuildRoot, true);
                }

                Directory.CreateDirectory(context.BuildRoot);
                Directory.CreateDirectory(context.BundleOutputRoot);
                Directory.CreateDirectory(context.HistoryRoot);
                context.Log($"Build root: {context.BuildRoot}");
                context.Log($"Bundle output: {context.BundleOutputRoot}");
                return ResourceBuildTaskResult.Succeed();
            }
        }
    }
}
