using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDeveloperKit.Editor
{
    internal sealed partial class ResourceBuildPipeline
    {
        private sealed class TaskUpdateGlobalManifest : ISbpBuildTask
        {
            public string TaskName => "Update Global Manifest";

            public ResourceBuildTaskResult Run(ResourceBuildPipelineContext context)
            {
                var globalManifest = LoadGlobalManifest(context.GlobalManifestPath);
                if (globalManifest.Packages == null)
                {
                    globalManifest.Packages = new List<ResourcePackageVersionInfo>();
                }

                var packageInfo = globalManifest.Packages.FirstOrDefault(
                    info => string.Equals(info.Name, context.PackageName, StringComparison.Ordinal));
                if (packageInfo == null)
                {
                    packageInfo = new ResourcePackageVersionInfo
                    {
                        Name = context.PackageName,
                        Versions = new List<ResourceVersionDetail>()
                    };
                    globalManifest.Packages.Add(packageInfo);
                }

                packageInfo.CurrentVersion = context.PackageVersion;
                packageInfo.PackageRole = context.Request.Package.Role.ToString();
                packageInfo.Versions ??= new List<ResourceVersionDetail>();

                var detail = new ResourceVersionDetail
                {
                    Version = context.PackageVersion,
                    BuildTimeUtc = DateTime.UtcNow.ToString("O"),
                    SizeBytes = context.BuiltBundles.Sum(static item => item.SizeBytes),
                    BundleCount = context.BuiltBundles.Count,
                    ManifestPath = $"{context.PackageName}/{context.PackageVersion}/{context.Request.Package.ResolveManifestRelativePath()}".Replace('\\', '/')
                };

                var existingIndex = packageInfo.Versions.FindIndex(item => string.Equals(item.Version, detail.Version, StringComparison.Ordinal));
                if (existingIndex >= 0)
                {
                    packageInfo.Versions[existingIndex] = detail;
                }
                else
                {
                    packageInfo.Versions.Insert(0, detail);
                }

                globalManifest.UpdateTimeUtc = DateTime.UtcNow.ToString("O");
                SaveGlobalManifest(context.GlobalManifestPath, globalManifest);
                context.Log($"Global manifest updated: {context.GlobalManifestPath}");
                return ResourceBuildTaskResult.Succeed();
            }
        }
    }
}
