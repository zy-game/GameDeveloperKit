using System.IO;
using GameDeveloperKit.Runtime;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor
{
    internal sealed partial class ResourceBuildPipeline
    {
        private sealed class TaskPublishBuiltin : ISbpBuildTask
        {
            public string TaskName => "Publish Builtin Files";

            public ResourceBuildTaskResult Run(ResourceBuildPipelineContext context)
            {
                var package = context.Request.Package;
                if (package.Role != ResourcePackageRole.Builtin)
                {
                    context.Log("Skip publish to StreamingAssets (package role is not Builtin).");
                    return ResourceBuildTaskResult.Succeed();
                }

                var targetRoot = Path.Combine(
                    Application.streamingAssetsPath,
                    package.ResolveStreamingAssetsRelativeRoot().Replace('/', Path.DirectorySeparatorChar));
                var targetBundlesRoot = Path.Combine(targetRoot, "bundles");
                if (Directory.Exists(targetBundlesRoot))
                {
                    Directory.Delete(targetBundlesRoot, true);
                }

                CopyDirectory(context.BundleOutputRoot, targetBundlesRoot);
                var targetManifestPath = Path.Combine(targetRoot, package.ResolveManifestRelativePath().Replace('/', Path.DirectorySeparatorChar));
                var manifestDirectory = Path.GetDirectoryName(targetManifestPath);
                if (!string.IsNullOrWhiteSpace(manifestDirectory))
                {
                    Directory.CreateDirectory(manifestDirectory);
                }

                File.Copy(context.PackageManifestPath, targetManifestPath, true);
                context.Log($"Published to StreamingAssets: {targetRoot}");
                AssetDatabase.Refresh();
                return ResourceBuildTaskResult.Succeed();
            }
        }
    }
}
