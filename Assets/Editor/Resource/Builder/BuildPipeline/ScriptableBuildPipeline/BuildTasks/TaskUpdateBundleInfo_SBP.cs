using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 任务：更新 SBP Bundle 信息
    /// </summary>
    public class TaskUpdateBundleInfo_SBP : IBuildTask
    {
        public string TaskName => "Update Bundle Info (SBP)";

        public TaskResult Run(BuildContext context)
        {
            context.Log("[SBP] 更新 Bundle 信息...");

            try
            {
                var results = context.SBPBuildResults;
                if (results == null)
                {
                    return TaskResult.Failed("[SBP] SBP 构建结果为空");
                }

                context.BuiltBundles.Clear();
                context.BundleSizes.Clear();
                context.BundleAssetMap.Clear();
                context.TotalSize = 0;

                var settings = ResourceBuilderSettings.Instance;
                var bundleExtension = settings.bundleExtension;

                // 遍历所有构建的 Bundle
                foreach (var bundleInfo in results.BundleInfos)
                {
                    var bundleName = bundleInfo.Key;
                    var info = bundleInfo.Value;

                    // 获取文件路径（添加扩展名）
                    var bundleFileName = bundleName + bundleExtension;
                    var bundlePath = Path.Combine(context.OutputPath, bundleFileName);

                    if (!File.Exists(bundlePath))
                    {
                        context.LogWarning($"[SBP] Bundle 文件不存在: {bundleFileName}");
                        continue;
                    }

                    var fileInfo = new FileInfo(bundlePath);
                    var fileSize = fileInfo.Length;

                    context.BuiltBundles.Add(bundleFileName);
                    context.BundleSizes[bundleFileName] = fileSize;
                    context.TotalSize += fileSize;

                    // 收集Bundle中的资源列表 (用于历史记录)
                    var assetsInBundle = new List<string>();
                    
                    // 从 AssetBundleBuilds 获取资源列表
                    if (context.AssetBundleBuilds.TryGetValue(bundleName, out var bundleBuild))
                    {
                        if (bundleBuild.assetNames != null)
                        {
                            assetsInBundle.AddRange(bundleBuild.assetNames);
                        }
                    }
                    
                    context.BundleAssetMap[bundleFileName] = assetsInBundle;

                    // 记录依赖信息
                    var dependencies = info.Dependencies;
                    if (dependencies != null && dependencies.Length > 0)
                    {
                        context.Log($"[SBP]   {bundleName} 依赖: {string.Join(", ", dependencies)}");
                    }
                }

                context.Log($"[SBP] Bundle 信息更新完成");
                context.Log($"[SBP]   Bundle 数量: {context.BuiltBundles.Count}");
                context.Log($"[SBP]   总大小: {FormatBytes(context.TotalSize)}");

                return TaskResult.Succeed();
            }
            catch (System.Exception ex)
            {
                return TaskResult.Failed($"[SBP] 更新 Bundle 信息时发生错误: {ex.Message}");
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
