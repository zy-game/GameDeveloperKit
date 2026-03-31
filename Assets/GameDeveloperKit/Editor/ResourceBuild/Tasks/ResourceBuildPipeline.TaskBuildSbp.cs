using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;

namespace GameDeveloperKit.Editor
{
#if UNITY_2018_3_OR_NEWER
    using BuildCompression = UnityEngine.BuildCompression;
#else
    using BuildCompression = UnityEditor.Build.Content.BuildCompression;
#endif

    internal sealed partial class ResourceBuildPipeline
    {

        private sealed class TaskBuildSbp : ISbpBuildTask
        {
            public string TaskName => "Build AssetBundles (SBP)";

            public ResourceBuildTaskResult Run(ResourceBuildPipelineContext context)
            {
                if (context.AssetBundleBuilds.Count == 0)
                {
                    return ResourceBuildTaskResult.Failed("No AssetBundleBuilds to run SBP.");
                }

                var target = context.Request.BuildTarget;
                var group = BuildPipeline.GetBuildTargetGroup(target);
                var parameters = new BundleBuildParameters(target, group, context.BundleOutputRoot)
                {
                    UseCache = !context.Request.ForceRebuild,
                    AppendHash = true,
                    BundleCompression = BuildCompression.LZ4
                };

                var content = new BundleBuildContent(context.AssetBundleBuilds.ToArray());
                var returnCode = ContentPipeline.BuildAssetBundles(parameters, content, out var results);
                if (returnCode != ReturnCode.Success || results == null)
                {
                    return ResourceBuildTaskResult.Failed($"SBP build failed. ReturnCode={returnCode}.");
                }

                context.SbpBuildResults = results;
                context.Log($"SBP bundles: {results.BundleInfos.Count}");
                return ResourceBuildTaskResult.Succeed();
            }
        }
    }
}
