using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 任务：创建构建报告
    /// </summary>
    public class TaskCreateReport : IBuildTask
    {
        public string TaskName => "Create Report";

        public TaskResult Run(BuildContext context)
        {
            Debug.Log("[TaskCreateReport] Run 方法开始执行");
            context.Log("开始创建构建报告...");

            try
            {
                var sb = new StringBuilder();

                // 报告头
                sb.AppendLine("========================================");
                sb.AppendLine("AssetBundle 构建报告");
                sb.AppendLine("========================================");
                sb.AppendLine();

                // 构建信息
                sb.AppendLine("【构建信息】");
                sb.AppendLine($"包名称: {context.PackageName}");
                sb.AppendLine($"包版本: {context.PackageVersion}");
                sb.AppendLine($"包类型: {context.PackageSettings?.packageType}");
                sb.AppendLine($"构建目标: {context.BuildTarget}");
                sb.AppendLine($"输出路径: {context.OutputPath}");
                sb.AppendLine($"开始时间: {context.BuildStartTime:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"结束时间: {context.BuildEndTime:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"耗时: {(context.BuildEndTime - context.BuildStartTime).TotalSeconds:F2} 秒");
                sb.AppendLine();

                // 资源统计
                sb.AppendLine("【资源统计】");
                sb.AppendLine($"收集资源数量: {context.CollectedAssets?.Count ?? 0}");
                sb.AppendLine($"Bundle 分组数量: {context.BundleGroups?.Count ?? 0}");
                if (context.BundleGroups != null)
                {
                    foreach (var kvp in context.BundleGroups)
                    {
                        sb.AppendLine($"  - {kvp.Key}: {kvp.Value.Count} 个资源");
                    }
                }
                sb.AppendLine();

                // Bundle 统计
                sb.AppendLine("【Bundle 统计】");
                sb.AppendLine($"Bundle 数量: {context.BuiltBundles.Count}");
                sb.AppendLine($"总大小: {FormatBytes(context.TotalSize)}");
                sb.AppendLine();

                sb.AppendLine("【Bundle 详情】");
                foreach (var bundleName in context.BuiltBundles)
                {
                    if (context.BundleSizes.TryGetValue(bundleName, out var size))
                    {
                        sb.AppendLine($"  ● {bundleName}: {FormatBytes(size)}");
                        
                        // 列出 Bundle 中的资源
                        if (context.BundleAssetMap != null && context.BundleAssetMap.TryGetValue(bundleName, out var assets))
                        {
                            foreach (var assetPath in assets)
                            {
                                sb.AppendLine($"      - {assetPath}");
                            }
                        }
                    }
                }
                sb.AppendLine();

                // 构建日志
                if (context.BuildLogs.Count > 0)
                {
                    sb.AppendLine("【构建日志】");
                    foreach (var log in context.BuildLogs)
                    {
                        sb.AppendLine(log);
                    }
                    sb.AppendLine();
                }

                // 保存报告到输出目录（兼容旧的方式）
                var reportPath = Path.Combine(context.OutputPath, $"{context.PackageName}_BuildReport.txt");
                File.WriteAllText(reportPath, sb.ToString());

                context.Log($"构建报告已创建: {reportPath}");

                // ========== 保存构建历史记录 ==========
                Debug.Log("[TaskCreateReport] 准备调用 SaveBuildHistory...");
                SaveBuildHistory(context, sb.ToString());
                Debug.Log("[TaskCreateReport] SaveBuildHistory 调用完成");

                return TaskResult.Succeed();
            }
            catch (System.Exception ex)
            {
                return TaskResult.Failed($"创建构建报告时发生错误: {ex.Message}");
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

        /// <summary>
        /// 保存构建历史记录
        /// </summary>
        private void SaveBuildHistory(BuildContext context, string reportContent)
        {
            try
            {
                Debug.Log("[BuildHistory] ========== 开始保存构建历史记录 ==========");
                context.Log("开始保存构建历史记录...");
                
                // 检查时间是否有效
                if (context.BuildEndTime == default(DateTime))
                {
                    context.LogWarning("BuildEndTime 未设置，使用当前时间");
                    context.BuildEndTime = DateTime.Now;
                }
                
                if (context.BuildStartTime == default(DateTime))
                {
                    context.LogWarning("BuildStartTime 未设置，使用 BuildEndTime");
                    context.BuildStartTime = context.BuildEndTime;
                }

                // 创建历史记录
                var record = new BuildHistoryRecord
                {
                    buildId = Guid.NewGuid().ToString(),
                    buildTime = context.BuildEndTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    packageName = context.PackageName,
                    packageVersion = context.PackageVersion,
                    success = true,
                    buildDuration = (float)(context.BuildEndTime - context.BuildStartTime).TotalSeconds,
                    totalBundles = context.BuiltBundles.Count,
                    totalSize = context.TotalSize,
                    bundles = new List<BundleSnapshot>()
                };
                
                context.Log($"构建记录时间: {record.buildTime}, 耗时: {record.buildDuration:F2}s");

                // 收集Bundle快照
                foreach (var bundleName in context.BuiltBundles)
                {
                    var snapshot = new BundleSnapshot
                    {
                        bundleName = bundleName,
                        size = context.BundleSizes.GetValueOrDefault(bundleName, 0),
                        assetPaths = GetAssetsInBundle(context, bundleName),
                        bundleHash = GetBundleFileHash(context.OutputPath, bundleName)
                    };
                    record.bundles.Add(snapshot);
                }

                // 创建历史记录文件夹
                var historyFolder = Path.Combine("Build/Backup", record.buildTime.Replace(":", "-"));
                Debug.Log($"[BuildHistory] 创建历史记录文件夹: {historyFolder}");
                
                if (!Directory.Exists(historyFolder))
                {
                    Directory.CreateDirectory(historyFolder);
                    Debug.Log($"[BuildHistory] 历史记录文件夹已创建");
                }

                // 创建资源快照
                Debug.Log($"[BuildHistory] 开始创建资源快照，Bundle数量: {record.bundles.Count}");
                record.assetSnapshots = BuildHistoryManager.CreateAssetSnapshots(record.bundles);
                Debug.Log($"[BuildHistory] 资源快照创建完成，资源数: {record.assetSnapshots.Count}");

                // 保存历史记录
                Debug.Log($"[BuildHistory] 保存历史记录到文件夹: {historyFolder}");
                BuildHistoryManager.SaveBuildRecord(record, historyFolder);

                // 保存构建报告到历史记录文件夹
                var historyReportPath = Path.Combine(historyFolder, "BuildReport.txt");
                File.WriteAllText(historyReportPath, reportContent);
                Debug.Log($"[BuildHistory] 构建报告已保存到: {historyReportPath}");

                context.Log($"构建历史记录已保存: {record.buildId} (资源数: {record.assetSnapshots.Count})");
                Debug.Log($"[BuildHistory] ========== 构建历史记录保存完成 ==========");
            }
            catch (Exception ex)
            {
                context.LogWarning($"保存构建历史记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取Bundle中的资源列表
        /// </summary>
        private List<string> GetAssetsInBundle(BuildContext context, string bundleName)
        {
            // 优先从 BundleAssetMap 获取
            if (context.BundleAssetMap != null && context.BundleAssetMap.TryGetValue(bundleName, out var assets))
            {
                return new List<string>(assets);
            }

            // 从 AssetBundleBuilds 获取
            if (context.AssetBundleBuilds != null && context.AssetBundleBuilds.TryGetValue(bundleName, out var bundleBuild))
            {
                return new List<string>(bundleBuild.assetNames);
            }

            return new List<string>();
        }

        /// <summary>
        /// 计算Bundle文件哈希
        /// </summary>
        private string GetBundleFileHash(string outputPath, string bundleName)
        {
            var bundlePath = Path.Combine(outputPath, bundleName);
            return BuildHistoryManager.CalculateFileHash(bundlePath);
        }
    }
}
