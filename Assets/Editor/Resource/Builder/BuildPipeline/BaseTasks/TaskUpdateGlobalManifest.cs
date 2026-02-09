using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.Resource;
using UnityEngine;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 任务：更新全局清单
    /// 在每次构建完成后，更新全局manifest.json
    /// </summary>
    public class TaskUpdateGlobalManifest : IBuildTask
    {
        public string TaskName => "Update Global Manifest";

        public TaskResult Run(BuildContext context)
        {
            context.Log("开始更新全局清单...");

            try
            {
                // 1. 获取全局清单路径
                var globalSettings = ResourceBuilderSettings.Instance;
                var globalManifestPath = Path.Combine(globalSettings.outputPath, "manifest.json");
                
                context.Log($"全局清单路径: {globalManifestPath}");

                // 2. 读取或创建GlobalManifest
                var globalManifest = LoadOrCreateGlobalManifest(globalManifestPath);

                // 3. 更新package信息
                UpdatePackageInfo(globalManifest, context);

                // 4. 保存GlobalManifest
                var json = JsonUtility.ToJson(globalManifest, true);
                
                // 确保输出目录存在
                var directory = Path.GetDirectoryName(globalManifestPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                File.WriteAllText(globalManifestPath, json);

                context.Log($"✓ 全局清单已更新: {globalManifestPath}");
                context.Log($"  Package: {context.PackageName}");
                context.Log($"  Version: {context.PackageVersion}");
                context.Log($"  Type: {context.PackageSettings.packageType}");

                return TaskResult.Succeed();
            }
            catch (Exception ex)
            {
                return TaskResult.Failed($"更新全局清单时发生错误: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 加载或创建全局清单
        /// </summary>
        private VersionManifest LoadOrCreateGlobalManifest(string manifestPath)
        {
            if (File.Exists(manifestPath))
            {
                try
                {
                    var json = File.ReadAllText(manifestPath);
                    var manifest = JsonUtility.FromJson<VersionManifest>(json);
                    
                    if (manifest != null)
                    {
                        Debug.Log($"[TaskUpdateGlobalManifest] 已加载现有全局清单，包含 {manifest.packages?.Length ?? 0} 个package");
                        return manifest;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[TaskUpdateGlobalManifest] 加载全局清单失败，将创建新清单: {ex.Message}");
                }
            }

            // 创建新的全局清单
            Debug.Log("[TaskUpdateGlobalManifest] 创建新的全局清单");
            return new VersionManifest
            {
                version = "1.0",
                updateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                packages = new PackageVersionInfo[0]
            };
        }

        /// <summary>
        /// 更新package信息
        /// </summary>
        private void UpdatePackageInfo(VersionManifest manifest, BuildContext context)
        {
            // 查找或创建PackageVersionInfo
            var packageInfo = FindOrCreatePackageInfo(manifest, context.PackageName);

            // 更新基本信息
            packageInfo.name = context.PackageName;
            packageInfo.currentVersion = context.PackageVersion;
            packageInfo.packageType = context.PackageSettings.packageType.ToString();

            // 确保时间有效
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

            // 创建新的VersionDetail
            var versionDetail = new VersionDetail
            {
                version = context.PackageVersion,
                buildTime = context.BuildEndTime.ToString("yyyy-MM-dd HH:mm:ss"),
                size = context.TotalSize > 0 ? context.TotalSize : CalculateTotalSize(context),
                bundleCount = context.BuiltBundles?.Count ?? 0,
                manifestPath = $"{context.PackageName}/{context.PackageVersion}/{context.PackageName.ToLower()}.json"
            };
            
            context.Log($"  Bundle数量: {versionDetail.bundleCount}");
            context.Log($"  总大小: {versionDetail.size} bytes");

            // 插入到versions列表（保持按时间倒序）
            InsertVersionDetail(packageInfo, versionDetail);

            // 更新全局清单的更新时间
            manifest.updateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// 查找或创建PackageVersionInfo
        /// </summary>
        private PackageVersionInfo FindOrCreatePackageInfo(VersionManifest manifest, string packageName)
        {
            // 确保packages数组存在
            if (manifest.packages == null)
            {
                manifest.packages = new PackageVersionInfo[0];
            }

            // 查找现有的PackageVersionInfo
            var existingInfo = manifest.packages.FirstOrDefault(p => p.name == packageName);
            if (existingInfo != null)
            {
                return existingInfo;
            }

            // 创建新的PackageVersionInfo
            var newInfo = new PackageVersionInfo
            {
                name = packageName,
                currentVersion = "",
                packageType = "",
                versions = new VersionDetail[0]
            };

            // 添加到packages数组
            var packagesList = new List<PackageVersionInfo>(manifest.packages);
            packagesList.Add(newInfo);
            manifest.packages = packagesList.ToArray();

            return newInfo;
        }

        /// <summary>
        /// 插入版本详情（按时间倒序）
        /// </summary>
        private void InsertVersionDetail(PackageVersionInfo packageInfo, VersionDetail newDetail)
        {
            // 确保versions数组存在
            if (packageInfo.versions == null)
            {
                packageInfo.versions = new VersionDetail[0];
            }

            var versionsList = new List<VersionDetail>(packageInfo.versions);

            // 检查是否已存在相同版本
            var existingIndex = versionsList.FindIndex(v => v.version == newDetail.version);
            if (existingIndex >= 0)
            {
                // 更新现有版本
                versionsList[existingIndex] = newDetail;
                Debug.Log($"[TaskUpdateGlobalManifest] 更新现有版本: {newDetail.version}");
            }
            else
            {
                // 插入新版本（在最前面，保持倒序）
                versionsList.Insert(0, newDetail);
                Debug.Log($"[TaskUpdateGlobalManifest] 添加新版本: {newDetail.version}");
            }

            packageInfo.versions = versionsList.ToArray();
        }

        /// <summary>
        /// 计算总大小
        /// </summary>
        private long CalculateTotalSize(BuildContext context)
        {
            long totalSize = 0;

            if (context.BuiltBundles != null && context.BuiltBundles.Count > 0)
            {
                var settings = ResourceBuilderSettings.Instance;
                var bundleExtension = settings.bundleExtension;

                foreach (var bundleName in context.BuiltBundles)
                {
                    var bundlePath = Path.Combine(context.OutputPath, bundleName);
                    if (File.Exists(bundlePath))
                    {
                        var fileInfo = new FileInfo(bundlePath);
                        totalSize += fileInfo.Length;
                    }
                }
            }

            return totalSize;
        }
    }
}
