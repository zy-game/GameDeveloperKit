using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 构建历史记录管理器
    /// </summary>
    public static class BuildHistoryManager
    {
        private const int MaxHistoryRecords = 10;
        private const string HistoryFilePrefix = "BuildHistory_";
        private const string HistoryFileExtension = ".json";

        /// <summary>
        /// 创建资源快照
        /// </summary>
        public static List<AssetSnapshot> CreateAssetSnapshots(List<BundleSnapshot> bundles)
        {
            Debug.Log($"[BuildHistory] CreateAssetSnapshots 被调用, Bundle数量: {bundles?.Count ?? 0}");
            
            var snapshots = new List<AssetSnapshot>();
            var processedAssets = new HashSet<string>();

            foreach (var bundle in bundles)
            {
                if (bundle.assetPaths == null) continue;

                foreach (var assetPath in bundle.assetPaths)
                {
                    if (processedAssets.Contains(assetPath))
                        continue;

                    processedAssets.Add(assetPath);

                    try
                    {
                        var snapshot = CreateAssetSnapshot(assetPath);
                        if (snapshot != null)
                        {
                            snapshots.Add(snapshot);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[BuildHistory] Failed to create snapshot for {assetPath}: {ex.Message}");
                    }
                }
            }

            return snapshots;
        }

        /// <summary>
        /// 创建单个资源的快照
        /// </summary>
        private static AssetSnapshot CreateAssetSnapshot(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
                return null;

            var hash = AssetDatabase.GetAssetDependencyHash(assetPath);
            var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var assetTypeName = assetType?.Name ?? "Unknown";
            var bundleName = AssetDatabase.GetImplicitAssetBundleName(assetPath);

            var snapshot = new AssetSnapshot
            {
                assetPath = assetPath,
                assetGuid = AssetDatabase.AssetPathToGUID(assetPath),
                contentHash = hash.ToString(),
                fileSize = new FileInfo(assetPath).Length,
                lastModified = File.GetLastWriteTime(assetPath).ToString("yyyy-MM-dd HH:mm:ss"),
                assetType = assetTypeName,
                bundleName = bundleName
            };

            return snapshot;
        }

        /// <summary>
        /// 对比两个文件，生成差异列表
        /// </summary>
        public static List<FileDiffLine> CompareFiles(string oldFilePath, string newFilePath)
        {
            var differences = new List<FileDiffLine>();

            try
            {
                if (!File.Exists(oldFilePath) || !File.Exists(newFilePath))
                {
                    Debug.LogWarning($"[BuildHistory] 文件不存在: {oldFilePath} 或 {newFilePath}");
                    return differences;
                }

                var oldLines = File.ReadAllLines(oldFilePath);
                var newLines = File.ReadAllLines(newFilePath);

                // 简单的逐行对比
                var oldDict = new Dictionary<string, int>();
                for (int i = 0; i < oldLines.Length; i++)
                {
                    oldDict[oldLines[i]] = i;
                }

                var newDict = new Dictionary<string, int>();
                for (int i = 0; i < newLines.Length; i++)
                {
                    newDict[newLines[i]] = i;
                }

                // 找出差异
                int maxLineNumber = Math.Max(oldLines.Length, newLines.Length);
                for (int i = 0; i < maxLineNumber; i++)
                {
                    string oldLine = i < oldLines.Length ? oldLines[i] : null;
                    string newLine = i < newLines.Length ? newLines[i] : null;

                    if (oldLine == null && newLine != null)
                    {
                        // 新增行
                        differences.Add(new FileDiffLine
                        {
                            type = "added",
                            line = newLine,
                            lineNumber = i + 1
                        });
                    }
                    else if (oldLine != null && newLine == null)
                    {
                        // 删除行
                        differences.Add(new FileDiffLine
                        {
                            type = "removed",
                            line = oldLine,
                            lineNumber = i + 1
                        });
                    }
                    else if (oldLine != newLine)
                    {
                        // 修改行
                        differences.Add(new FileDiffLine
                        {
                            type = "modified",
                            line = $"{oldLine} → {newLine}",
                            lineNumber = i + 1
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildHistory] 对比文件失败: {ex.Message}");
            }

            return differences;
        }



        /// <summary>
        /// 计算文件哈希
        /// </summary>
        public static string CalculateFileHash(string filePath)
        {
            if (!File.Exists(filePath))
                return string.Empty;

            try
            {
                using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BuildHistory] Failed to calculate hash for {filePath}: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 保存构建记录（直接保存到 Backup 目录）
        /// </summary>
        public static void SaveBuildRecord(BuildHistoryRecord record, string backupFolder)
        {
            try
            {
                Debug.Log($"[BuildHistory] SaveBuildRecord 被调用, 备份文件夹: {backupFolder}, 资源快照数: {record.assetSnapshots?.Count ?? 0}");
                
                var historyFile = Path.Combine(backupFolder, "BuildHistory.json");
                var json = JsonUtility.ToJson(record, true);

                Debug.Log($"[BuildHistory] 保存历史记录到: {historyFile}");
                File.WriteAllText(historyFile, json);

                Debug.Log($"[BuildHistory] ✓ 历史记录已保存: {record.buildId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildHistory] 保存历史记录失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 加载所有历史记录（从 Build/Backup 目录）
        /// </summary>
        public static List<BuildHistoryRecord> LoadHistory(string packageName)
        {
            var records = new List<BuildHistoryRecord>();
            var backupRoot = "Build/Backup";
            
            Debug.Log($"[BuildHistory] 从 Backup 目录加载历史: {backupRoot}");

            if (!Directory.Exists(backupRoot))
            {
                Debug.Log($"[BuildHistory] Backup 目录不存在");
                return records;
            }

            try
            {
                // 获取所有备份文件夹（按时间排序）
                var backupDirs = Directory.GetDirectories(backupRoot)
                    .OrderByDescending(d => d)  // 最新的在前
                    .Take(MaxHistoryRecords)
                    .ToList();

                Debug.Log($"[BuildHistory] 找到 {backupDirs.Count} 个备份文件夹");

                foreach (var dir in backupDirs)
                {
                    var historyFile = Path.Combine(dir, "BuildHistory.json");
                    if (File.Exists(historyFile))
                    {
                        try
                        {
                            var json = File.ReadAllText(historyFile);
                            var record = JsonUtility.FromJson<BuildHistoryRecord>(json);
                            if (record != null)
                            {
                                records.Add(record);
                                Debug.Log($"[BuildHistory] 加载记录: {Path.GetFileName(dir)} - {record.buildId}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[BuildHistory] 加载记录失败 ({historyFile}): {ex.Message}");
                        }
                    }
                }

                Debug.Log($"[BuildHistory] 总共加载了 {records.Count} 条历史记录");
                return records;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildHistory] 加载历史记录失败: {ex.Message}\n{ex.StackTrace}");
                return new List<BuildHistoryRecord>();
            }
        }

        /// <summary>
        /// 获取最新的构建记录
        /// </summary>
        public static BuildHistoryRecord GetLatestBuildRecord(string packageName)
        {
            var records = LoadHistory(packageName);
            
            // 过滤指定 package 的记录，并按时间排序
            var packageRecords = records
                .Where(r => string.IsNullOrEmpty(packageName) || r.packageName == packageName)
                .OrderByDescending(r => r.buildTime)
                .ToList();
            
            return packageRecords.FirstOrDefault();
        }

        /// <summary>
        /// 对比两次构建 - 精确到资源内容级别
        /// </summary>
        public static BuildCompareResult CompareBuild(BuildHistoryRecord current, BuildHistoryRecord previous)
        {
            if (current == null || previous == null)
            {
                Debug.LogWarning("[BuildHistory] Cannot compare null records");
                return null;
            }

            var result = new BuildCompareResult
            {
                current = current,
                previous = previous
            };

            // 对比Bundle
            CompareBundles(current, previous, result);

            // 对比资源
            CompareAssets(current, previous, result);

            // 统计信息
            result.totalSizeDiff = current.totalSize - previous.totalSize;
            result.buildTimeDiff = current.buildDuration - previous.buildDuration;

            return result;
        }

        /// <summary>
        /// 对比Bundle
        /// </summary>
        private static void CompareBundles(BuildHistoryRecord current, BuildHistoryRecord previous, BuildCompareResult result)
        {
            var currentBundleNames = new HashSet<string>(current.bundles.Select(b => b.bundleName));
            var previousBundleNames = new HashSet<string>(previous.bundles.Select(b => b.bundleName));

            // 新增的Bundle
            result.bundlesAdded = currentBundleNames.Except(previousBundleNames).ToList();

            // 删除的Bundle
            result.bundlesRemoved = previousBundleNames.Except(currentBundleNames).ToList();

            // Bundle大小变化
            var commonBundles = currentBundleNames.Intersect(previousBundleNames);
            foreach (var bundleName in commonBundles)
            {
                var currentBundle = current.bundles.FirstOrDefault(b => b.bundleName == bundleName);
                var previousBundle = previous.bundles.FirstOrDefault(b => b.bundleName == bundleName);

                if (currentBundle != null && previousBundle != null)
                {
                    var sizeDiff = currentBundle.size - previousBundle.size;
                    if (sizeDiff != 0)
                    {
                        result.bundleSizeChanges[bundleName] = sizeDiff;
                    }
                }
            }
        }

        /// <summary>
        /// 对比资源
        /// </summary>
        private static void CompareAssets(BuildHistoryRecord current, BuildHistoryRecord previous, BuildCompareResult result)
        {
            Debug.Log($"[BuildHistory] 开始对比资源: Current={current.assetSnapshots.Count}, Previous={previous.assetSnapshots.Count}");
            
            // 创建资源字典以便快速查找
            var currentAssets = current.assetSnapshots.ToDictionary(a => a.assetPath, a => a);
            var previousAssets = previous.assetSnapshots.ToDictionary(a => a.assetPath, a => a);

            var allAssetPaths = new HashSet<string>(currentAssets.Keys);
            allAssetPaths.UnionWith(previousAssets.Keys);
            
            Debug.Log($"[BuildHistory] 总共需要对比 {allAssetPaths.Count} 个资源");

            foreach (var assetPath in allAssetPaths)
            {
                var hasOld = previousAssets.TryGetValue(assetPath, out var oldSnapshot);
                var hasNew = currentAssets.TryGetValue(assetPath, out var newSnapshot);

                if (!hasOld && hasNew)
                {
                    // 新增资源
                    result.assetChanges.Add(new AssetChangeDetail
                    {
                        type = AssetChangeType.Added,
                        assetPath = assetPath,
                        assetType = newSnapshot.assetType,
                        toBundle = FindBundleForAsset(current.bundles, assetPath),
                        newHash = newSnapshot.contentHash,
                        newSize = newSnapshot.fileSize
                    });
                    result.assetsAdded++;
                }
                else if (hasOld && !hasNew)
                {
                    // 删除资源
                    result.assetChanges.Add(new AssetChangeDetail
                    {
                        type = AssetChangeType.Removed,
                        assetPath = assetPath,
                        assetType = oldSnapshot.assetType,
                        fromBundle = FindBundleForAsset(previous.bundles, assetPath),
                        oldHash = oldSnapshot.contentHash,
                        oldSize = oldSnapshot.fileSize
                    });
                    result.assetsRemoved++;
                }
                else if (hasOld && hasNew)
                {
                    // 检查内容是否修改 (关键!)
                    if (oldSnapshot.contentHash != newSnapshot.contentHash)
                    {
                        Debug.Log($"[BuildHistory] 检测到修改: {assetPath}");
                        Debug.Log($"[BuildHistory]   旧Hash: {oldSnapshot.contentHash}");
                        Debug.Log($"[BuildHistory]   新Hash: {newSnapshot.contentHash}");
                        
                        result.assetChanges.Add(new AssetChangeDetail
                        {
                            type = AssetChangeType.ContentModified,
                            assetPath = assetPath,
                            assetType = newSnapshot.assetType,
                            fromBundle = FindBundleForAsset(previous.bundles, assetPath),
                            toBundle = FindBundleForAsset(current.bundles, assetPath),
                            oldHash = oldSnapshot.contentHash,
                            newHash = newSnapshot.contentHash,
                            oldSize = oldSnapshot.fileSize,
                            newSize = newSnapshot.fileSize,
                            sizeDiff = newSnapshot.fileSize - oldSnapshot.fileSize
                        });
                        result.assetsModified++;
                    }
                    else
                    {
                        // 检查是否移动Bundle
                        var oldBundle = FindBundleForAsset(previous.bundles, assetPath);
                        var newBundle = FindBundleForAsset(current.bundles, assetPath);
                        if (oldBundle != newBundle && !string.IsNullOrEmpty(oldBundle) && !string.IsNullOrEmpty(newBundle))
                        {
                            result.assetChanges.Add(new AssetChangeDetail
                            {
                                type = AssetChangeType.BundleMoved,
                                assetPath = assetPath,
                                assetType = newSnapshot.assetType,
                                fromBundle = oldBundle,
                                toBundle = newBundle,
                                oldSize = oldSnapshot.fileSize,
                                newSize = newSnapshot.fileSize
                            });
                            result.assetsMoved++;
                        }
                    }
                }
            }
            
            Debug.Log($"[BuildHistory] 对比完成: Added={result.assetsAdded}, Removed={result.assetsRemoved}, Modified={result.assetsModified}, Moved={result.assetsMoved}");
        }



        /// <summary>
        /// 查找资源所属的Bundle
        /// </summary>
        private static string FindBundleForAsset(List<BundleSnapshot> bundles, string assetPath)
        {
            foreach (var bundle in bundles)
            {
                if (bundle.assetPaths != null && bundle.assetPaths.Contains(assetPath))
                {
                    return bundle.bundleName;
                }
            }
            return null;
        }

        /// <summary>
        /// 清理旧记录（删除最旧的备份文件夹）
        /// </summary>
        public static void CleanOldRecords(string packageName, int maxRecords = MaxHistoryRecords)
        {
            var backupRoot = "Build/Backup";
            if (!Directory.Exists(backupRoot))
                return;

            try
            {
                var backupDirs = Directory.GetDirectories(backupRoot)
                    .OrderByDescending(d => d)
                    .ToList();

                if (backupDirs.Count > maxRecords)
                {
                    // 删除最旧的备份
                    for (int i = maxRecords; i < backupDirs.Count; i++)
                    {
                        Directory.Delete(backupDirs[i], true);
                        Debug.Log($"[BuildHistory] 删除旧备份: {Path.GetFileName(backupDirs[i])}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildHistory] 清理旧记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除所有历史记录（删除 Backup 目录）
        /// </summary>
        public static void ClearHistory(string packageName)
        {
            var backupRoot = "Build/Backup";
            if (Directory.Exists(backupRoot))
            {
                try
                {
                    Directory.Delete(backupRoot, true);
                    Debug.Log($"[BuildHistory] 已清除所有历史备份");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BuildHistory] 清除历史失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 导出对比报告为文本
        /// </summary>
        public static string ExportCompareReport(BuildCompareResult compareResult)
        {
            if (compareResult == null)
                return "No comparison result available";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("========================================");
            sb.AppendLine("构建对比报告");
            sb.AppendLine("========================================");
            sb.AppendLine();

            sb.AppendLine($"当前构建: {compareResult.current.packageName} v{compareResult.current.packageVersion}");
            sb.AppendLine($"构建时间: {compareResult.current.buildTime}");
            sb.AppendLine($"对比构建: {compareResult.previous.packageName} v{compareResult.previous.packageVersion}");
            sb.AppendLine($"构建时间: {compareResult.previous.buildTime}");
            sb.AppendLine();

            sb.AppendLine("【变动摘要】");
            sb.AppendLine($"Bundles: +{compareResult.bundlesAdded.Count} 新增, -{compareResult.bundlesRemoved.Count} 删除, ~{compareResult.bundleSizeChanges.Count} 修改");
            sb.AppendLine($"Assets:  +{compareResult.assetsAdded} 新增, -{compareResult.assetsRemoved} 删除, ~{compareResult.assetsModified} 修改, ↔{compareResult.assetsMoved} 移动");
            sb.AppendLine($"Size:    {FormatSizeDiff(compareResult.totalSizeDiff)}");
            sb.AppendLine($"Time:    {FormatTimeDiff(compareResult.buildTimeDiff)}");
            sb.AppendLine();

            if (compareResult.bundlesAdded.Count > 0)
            {
                sb.AppendLine("【新增Bundle】");
                foreach (var bundle in compareResult.bundlesAdded)
                {
                    sb.AppendLine($"  + {bundle}");
                }
                sb.AppendLine();
            }

            if (compareResult.bundlesRemoved.Count > 0)
            {
                sb.AppendLine("【删除Bundle】");
                foreach (var bundle in compareResult.bundlesRemoved)
                {
                    sb.AppendLine($"  - {bundle}");
                }
                sb.AppendLine();
            }

            var addedAssets = compareResult.assetChanges.Where(c => c.type == AssetChangeType.Added).ToList();
            if (addedAssets.Count > 0)
            {
                sb.AppendLine($"【新增资源】({addedAssets.Count})");
                foreach (var change in addedAssets)
                {
                    sb.AppendLine($"  + {change.assetPath}");
                    sb.AppendLine($"    → {change.toBundle} ({FormatBytes(change.newSize)})");
                }
                sb.AppendLine();
            }

            var removedAssets = compareResult.assetChanges.Where(c => c.type == AssetChangeType.Removed).ToList();
            if (removedAssets.Count > 0)
            {
                sb.AppendLine($"【删除资源】({removedAssets.Count})");
                foreach (var change in removedAssets)
                {
                    sb.AppendLine($"  - {change.assetPath}");
                    sb.AppendLine($"    ← {change.fromBundle} ({FormatBytes(change.oldSize)})");
                }
                sb.AppendLine();
            }

            var modifiedAssets = compareResult.assetChanges.Where(c => c.type == AssetChangeType.ContentModified).ToList();
            if (modifiedAssets.Count > 0)
            {
                sb.AppendLine($"【修改资源】({modifiedAssets.Count})");
                foreach (var change in modifiedAssets)
                {
                    sb.AppendLine($"  ~ {change.assetPath}");
                    sb.AppendLine($"    {change.GetDetailedDescription()}");
                    sb.AppendLine($"    Bundle: {change.fromBundle}, Size: {FormatBytes(change.oldSize)} → {FormatBytes(change.newSize)}");
                }
                sb.AppendLine();
            }

            var movedAssets = compareResult.assetChanges.Where(c => c.type == AssetChangeType.BundleMoved).ToList();
            if (movedAssets.Count > 0)
            {
                sb.AppendLine($"【移动资源】({movedAssets.Count})");
                foreach (var change in movedAssets)
                {
                    sb.AppendLine($"  ↔ {change.assetPath}");
                    sb.AppendLine($"    {change.fromBundle} → {change.toBundle}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F2} KB";
            else if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / 1024.0 / 1024.0:F2} MB";
            else
                return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
        }

        private static string FormatSizeDiff(long sizeDiff)
        {
            var sign = sizeDiff >= 0 ? "+" : "";
            var formatted = FormatBytes(Math.Abs(sizeDiff));
            return $"{sign}{formatted}";
        }

        private static string FormatTimeDiff(float timeDiff)
        {
            var sign = timeDiff >= 0 ? "+" : "";
            return $"{sign}{timeDiff:F2}s";
        }
    }
}
