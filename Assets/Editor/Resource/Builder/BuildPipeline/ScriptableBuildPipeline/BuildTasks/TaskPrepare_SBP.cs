using System.IO;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 任务：SBP 构建准备
    /// </summary>
    public class TaskPrepare_SBP : IBuildTask
    {
        public string TaskName => "Prepare SBP Build";

        public TaskResult Run(BuildContext context)
        {
            context.Log("[SBP] 准备 SBP 构建环境...");

            try
            {
                // 确保输出目录存在
                if (!Directory.Exists(context.OutputPath))
                {
                    Directory.CreateDirectory(context.OutputPath);
                    context.Log($"[SBP] 创建输出目录: {context.OutputPath}");
                }

                // 清理旧文件（如果需要强制重建）
                if (context.ForceRebuild)
                {
                    context.Log("[SBP] 强制重建，清理输出目录...");
                    var files = Directory.GetFiles(context.OutputPath);
                    foreach (var file in files)
                    {
                        File.Delete(file);
                    }
                    context.Log($"[SBP] 已删除 {files.Length} 个旧文件");
                }

                // 设置 SBP 压缩选项
                var settings = ResourceBuilderSettings.Instance;
                context.Compression = ConvertCompression(context.PackageSettings.compression);

                context.Log($"[SBP] 压缩方式: {context.PackageSettings.compression}");
                context.Log($"[SBP] 依赖优化: {(settings.enableDependencyOptimization ? "启用" : "禁用")}");
                context.Log($"[SBP] 内容更新: {(settings.enableContentUpdate ? "启用" : "禁用")}");

                return TaskResult.Succeed();
            }
            catch (System.Exception ex)
            {
                return TaskResult.Failed($"[SBP] 准备构建环境时发生错误: {ex.Message}");
            }
        }

        private UnityEngine.BuildCompression ConvertCompression(UnityEngine.CompressionType compression)
        {
            switch (compression)
            {
                case UnityEngine.CompressionType.None:
                    return UnityEngine.BuildCompression.Uncompressed;
                case UnityEngine.CompressionType.Lz4:
                    return UnityEngine.BuildCompression.LZ4;
                case UnityEngine.CompressionType.Lzma:
                    return UnityEngine.BuildCompression.LZMA;
                default:
                    return UnityEngine.BuildCompression.LZ4;
            }
        }
    }
}
