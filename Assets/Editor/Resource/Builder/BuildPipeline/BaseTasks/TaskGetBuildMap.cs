using System.Collections.Generic;
using System.Linq;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 任务：获取构建映射（收集资源）
    /// 使用新的 IAssetCollector 系统
    /// </summary>
    public class TaskGetBuildMap : IBuildTask
    {
        public string TaskName => "Get Build Map";

        public TaskResult Run(BuildContext context)
        {
            context.Log($"开始收集资源包 '{context.PackageName}' 的资源...");

            try
            {
                var package = context.PackageSettings;
                if (package == null)
                {
                    return TaskResult.Failed("PackageSettings 为空");
                }

                // 使用新的收集器系统收集资源
                var collectedAssets = package.CollectAssets();
                
                if (collectedAssets == null || collectedAssets.Count == 0)
                {
                    return TaskResult.Failed("没有收集到任何资源，请检查收集器配置");
                }

                // 存储到上下文
                context.CollectedAssets = collectedAssets;
                
                context.Log($"资源收集完成，共 {collectedAssets.Count} 个资源");

                // 输出资源类型统计
                var typeGroups = collectedAssets.GroupBy(a => a.assetType?.Name ?? "Unknown");
                foreach (var group in typeGroups.OrderByDescending(g => g.Count()))
                {
                    context.Log($"  - {group.Key}: {group.Count()} 个");
                }

                return TaskResult.Succeed();
            }
            catch (System.Exception ex)
            {
                return TaskResult.Failed($"收集资源时发生错误: {ex.Message}");
            }
        }
    }
}
