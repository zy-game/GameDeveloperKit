using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 任务：执行 SBP 构建
    /// 使用新的收集器和打包策略系统
    /// </summary>
    public class TaskBuilding_SBP : IBuildTask
    {
        public string TaskName => "Build AssetBundles (SBP)";

        public TaskResult Run(BuildContext context)
        {
            context.Log("[SBP] 开始 SBP 构建...");

            try
            {
                // 应用打包策略，生成 Bundle 分组
                var bundleGroups = ApplyPackStrategy(context);
                if (bundleGroups == null || bundleGroups.Count == 0)
                {
                    return TaskResult.Failed("[SBP] 打包策略未生成任何 Bundle 分组");
                }
                
                context.BundleGroups = bundleGroups;
                context.Log($"[SBP] 打包策略生成 {bundleGroups.Count} 个 Bundle 分组");

                // 创建构建内容
                var buildContent = CreateBuildContent(context);
                if (buildContent == null)
                {
                    return TaskResult.Failed("[SBP] 创建构建内容失败");
                }

                context.Log($"[SBP] 构建内容: {buildContent.BundleLayout.Count} 个 Bundle");

                // 创建构建参数
                var buildParams = CreateBuildParameters(context);

                // 执行 SBP 构建
                context.Log("[SBP] 正在构建 AssetBundles...");

                var returnCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out var results);

                // 检查构建结果
                if (returnCode != ReturnCode.Success)
                {
                    return TaskResult.Failed($"[SBP] 构建失败，返回码: {returnCode}");
                }

                // 保存构建结果
                context.SBPBuildResults = results;

                context.Log($"[SBP] 构建成功，生成了 {results.BundleInfos.Count} 个 Bundle");

                return TaskResult.Succeed();
            }
            catch (System.Exception ex)
            {
                return TaskResult.Failed($"[SBP] 构建时发生错误: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private Dictionary<string, List<CollectedAsset>> ApplyPackStrategy(BuildContext context)
        {
            var package = context.PackageSettings;
            var assets = context.CollectedAssets;
            
            if (assets == null || assets.Count == 0)
            {
                context.LogWarning("[SBP] 没有收集到资源");
                return new Dictionary<string, List<CollectedAsset>>();
            }

            var strategy = package.GetPackStrategy();
            context.Log($"[SBP] 使用打包策略: {strategy.GetType().Name}");
            
            return strategy.Pack(assets);
        }

        private BundleBuildContent CreateBuildContent(BuildContext context)
        {
            var bundleBuilds = new List<AssetBundleBuild>();

            foreach (var kvp in context.BundleGroups)
            {
                var bundleName = kvp.Key;
                var bundleAssets = kvp.Value;

                if (bundleAssets == null || bundleAssets.Count == 0)
                    continue;

                // 创建 AssetBundleBuild
                var bundleBuild = new AssetBundleBuild
                {
                    assetBundleName = bundleName,
                    assetNames = bundleAssets.Select(a => a.assetPath).ToArray(),
                    addressableNames = bundleAssets.Select(a => a.address).ToArray()
                };

                bundleBuilds.Add(bundleBuild);
                context.AssetBundleBuilds[bundleName] = bundleBuild;
                
                // 记录 Bundle-Asset 映射
                context.BundleAssetMap[bundleName] = bundleAssets.Select(a => a.assetPath).ToList();
            }

            context.Log($"[SBP] 准备构建 {bundleBuilds.Count} 个 Bundle");

            return new BundleBuildContent(bundleBuilds.ToArray());
        }

        private BundleBuildParameters CreateBuildParameters(BuildContext context)
        {
            var settings = ResourceBuilderSettings.Instance;

            var parameters = new BundleBuildParameters(
                context.BuildTarget,
                BuildPipeline.GetBuildTargetGroup(context.BuildTarget),
                context.OutputPath
            );

            // 设置构建选项
            parameters.UseCache = !context.ForceRebuild;
            parameters.BundleCompression = context.Compression;

            // 设置依赖优化
            if (settings.enableDependencyOptimization)
            {
                parameters.ContentBuildFlags |= UnityEditor.Build.Content.ContentBuildFlags.StripUnityVersion;
            }

            return parameters;
        }
    }
}
