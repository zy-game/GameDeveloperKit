using System.IO;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 任务：重命名 Bundle 文件（添加扩展名）
    /// </summary>
    public class TaskRenameBundles_SBP : IBuildTask
    {
        public string TaskName => "Rename Bundle Files";

        public TaskResult Run(BuildContext context)
        {
            context.Log("[SBP] 开始重命名 Bundle 文件...");

            try
            {
                var settings = ResourceBuilderSettings.Instance;
                var bundleExtension = settings.bundleExtension;

                if (string.IsNullOrEmpty(bundleExtension))
                {
                    context.LogWarning("[SBP] Bundle 扩展名为空，跳过重命名");
                    return TaskResult.Succeed();
                }

                var outputPath = context.OutputPath;
                
                // 从 SBP 构建结果中获取 Bundle 名称列表
                if (context.SBPBuildResults == null)
                {
                    context.LogWarning("[SBP] SBP 构建结果为空，跳过重命名");
                    return TaskResult.Succeed();
                }

                int renamedCount = 0;

                foreach (var bundleInfo in context.SBPBuildResults.BundleInfos)
                {
                    var bundleName = bundleInfo.Key;
                    var sourcePath = Path.Combine(outputPath, bundleName);
                    var targetPath = sourcePath + bundleExtension;

                    if (!File.Exists(sourcePath))
                    {
                        context.LogWarning($"[SBP] 源文件不存在: {bundleName}");
                        continue;
                    }

                    // 如果目标文件已存在，先删除
                    if (File.Exists(targetPath))
                    {
                        File.Delete(targetPath);
                    }

                    File.Move(sourcePath, targetPath);
                    renamedCount++;

                    context.Log($"[SBP]   重命名: {bundleName} -> {bundleName}{bundleExtension}");
                }

                context.Log($"[SBP] Bundle 文件重命名完成，共重命名 {renamedCount} 个文件");

                return TaskResult.Succeed();
            }
            catch (System.Exception ex)
            {
                return TaskResult.Failed($"[SBP] 重命名 Bundle 文件时发生错误: {ex.Message}");
            }
        }
    }
}
