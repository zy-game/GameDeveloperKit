using System.Collections.Generic;
using UnityEditor;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// Scriptable Build Pipeline 构建管线
    /// </summary>
    public class SBPBuildPipeline
    {
        /// <summary>
        /// 执行 SBP 构建
        /// </summary>
        public static bool Build(PackageSettings package, string outputPath, BuildTarget buildTarget, bool forceRebuild)
        {
            var globalSettings = ResourceBuilderSettings.Instance;
            
            // 创建构建上下文
            // 注意：outputPath 已经在 AssetBundleBuilder.BuildWithSBP 中包含了版本路径
            var context = new BuildContext
            {
                PackageSettings = package,
                PackageName = package.packageName,
                PackageVersion = package.version,
                OutputPath = outputPath,
                BuildTarget = buildTarget,
                ForceRebuild = forceRebuild,
                BuildStartTime = System.DateTime.Now,
                GlobalSettings = globalSettings
            };

            context.Log("========================================");
            context.Log($"开始 SBP 构建: {package.packageName}");
            context.Log("========================================");

            // 创建任务列表
            var tasks = CreateTasks();

            // 按顺序执行任务
            foreach (var task in tasks)
            {
                context.Log($"\n>>> 执行任务: {task.TaskName}");

                var result = task.Run(context);

                // 输出警告
                if (result.Warnings.Count > 0)
                {
                    foreach (var warning in result.Warnings)
                    {
                        context.LogWarning(warning);
                    }
                }

                // 检查任务结果
                if (!result.Success)
                {
                    context.LogError($"任务失败: {task.TaskName}");
                    context.LogError($"错误信息: {result.ErrorMessage}");
                    context.BuildEndTime = System.DateTime.Now;

                    // 创建失败报告
                    CreateFailureReport(context, task.TaskName, result.ErrorMessage);

                    return false;
                }

                context.Log($"<<< 任务完成: {task.TaskName}");
            }

            context.BuildEndTime = System.DateTime.Now;
            var duration = (context.BuildEndTime - context.BuildStartTime).TotalSeconds;

            context.Log("\n========================================");
            context.Log($"SBP 构建成功!");
            context.Log($"耗时: {duration:F2} 秒");
            context.Log("========================================");

            return true;
        }

        /// <summary>
        /// 创建任务列表
        /// </summary>
        private static List<IBuildTask> CreateTasks()
        {
            return new List<IBuildTask>
            {
                // 1. 准备阶段
                new TaskPrepare_SBP(),

                // 2. 收集资源
                new TaskGetBuildMap(),

                // 3. 执行 SBP 构建
                new TaskBuilding_SBP(),

                // 4. 重命名 Bundle 文件（添加扩展名）
                new TaskRenameBundles_SBP(),

                // 5. 更新 Bundle 信息
                new TaskUpdateBundleInfo_SBP(),

                // 6. 创建清单
                new TaskCreateManifest(),

                // 7. 更新全局清单
                new TaskUpdateGlobalManifest(),

                // 8. 拷贝到 StreamingAssets
                new TaskCopyBuildinFiles(),

                // 9. 创建构建报告
                new TaskCreateReport()
            };
        }

        /// <summary>
        /// 创建失败报告
        /// </summary>
        private static void CreateFailureReport(BuildContext context, string failedTask, string errorMessage)
        {
            try
            {
                var reportPath = System.IO.Path.Combine(context.OutputPath, $"{context.PackageName}_BuildFailure.txt");
                var sb = new System.Text.StringBuilder();

                sb.AppendLine("========================================");
                sb.AppendLine("AssetBundle 构建失败报告");
                sb.AppendLine("========================================");
                sb.AppendLine();
                sb.AppendLine($"包名称: {context.PackageName}");
                sb.AppendLine($"构建时间: {context.BuildStartTime:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"失败任务: {failedTask}");
                sb.AppendLine($"错误信息: {errorMessage}");
                sb.AppendLine();
                sb.AppendLine("构建日志:");
                foreach (var log in context.BuildLogs)
                {
                    sb.AppendLine(log);
                }

                var reportContent = sb.ToString();
                System.IO.File.WriteAllText(reportPath, reportContent);
                UnityEngine.Debug.Log($"失败报告已保存: {reportPath}");
                
                // 保存失败记录到 Build History（同时保存报告文件）
                SaveFailureToBuildHistory(context, failedTask, errorMessage, reportContent);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"创建失败报告时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存失败记录到构建历史
        /// </summary>
        private static void SaveFailureToBuildHistory(BuildContext context, string failedTask, string errorMessage, string reportContent)
        {
            try
            {
                UnityEngine.Debug.Log("[BuildHistory] ========== 保存失败构建记录 ==========");
                
                // 确保时间有效
                if (context.BuildEndTime == default(System.DateTime))
                {
                    context.BuildEndTime = System.DateTime.Now;
                }
                
                if (context.BuildStartTime == default(System.DateTime))
                {
                    context.BuildStartTime = context.BuildEndTime;
                }

                // 创建失败记录
                var record = new BuildHistoryRecord
                {
                    buildId = System.Guid.NewGuid().ToString(),
                    buildTime = context.BuildEndTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    packageName = context.PackageName,
                    packageVersion = context.PackageVersion,
                    success = false,  // 失败状态
                    buildDuration = (float)(context.BuildEndTime - context.BuildStartTime).TotalSeconds,
                    totalBundles = 0,
                    totalSize = 0,
                    bundles = new System.Collections.Generic.List<BundleSnapshot>(),
                    assetSnapshots = new System.Collections.Generic.List<AssetSnapshot>(),
                    failedTask = failedTask,  // 保存失败任务名
                    errorMessage = errorMessage  // 保存错误信息
                };

                // 创建历史记录文件夹（失败也创建，方便查看）
                var historyFolder = System.IO.Path.Combine("Build/Backup", record.buildTime.Replace(":", "-"));
                if (!System.IO.Directory.Exists(historyFolder))
                {
                    System.IO.Directory.CreateDirectory(historyFolder);
                }

                // 保存历史记录
                BuildHistoryManager.SaveBuildRecord(record, historyFolder);

                // 保存失败报告到历史记录文件夹
                var historyReportPath = System.IO.Path.Combine(historyFolder, "BuildReport.txt");
                System.IO.File.WriteAllText(historyReportPath, reportContent);
                UnityEngine.Debug.Log($"[BuildHistory] 失败报告已保存到: {historyReportPath}");

                UnityEngine.Debug.Log($"[BuildHistory] ✓ 失败记录已保存: {record.buildId}");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[BuildHistory] 保存失败记录时出错: {ex.Message}");
            }
        }
    }
}
