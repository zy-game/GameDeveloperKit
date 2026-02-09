using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// AssetBundle 构建器 - 使用 SBP Task 系统
    /// </summary>
    public static class AssetBundleBuilder
    {
        /// <summary>
        /// 构建报告
        /// </summary>
        public class BuildReport
        {
            public bool success;
            public string message;
            public int totalBundles;
            public int totalAssets;
            public long totalSize;
            public float buildTime;
            public Dictionary<string, long> bundleSizes;
            public Dictionary<string, List<string>> bundleAssets;
            public string outputPath;
        }
        
        /// <summary>
        /// 根据 PackageSettings 构建 AssetBundle
        /// 使用 Scriptable Build Pipeline (SBP) with Task System
        /// </summary>
        public static BuildReport BuildFromPackageSettings(PackageSettings packageSettings)
        {
            if (packageSettings == null)
            {
                return new BuildReport
                {
                    success = false,
                    message = "PackageSettings is null"
                };
            }
            
            // 验证收集器配置
            if (packageSettings.collector == null)
            {
                return new BuildReport
                {
                    success = false,
                    message = "Package 没有配置资源收集器"
                };
            }
            
            return BuildWithSBP(packageSettings);
        }
        
        /// <summary>
        /// 使用 SBP Task 系统构建
        /// </summary>
        private static BuildReport BuildWithSBP(PackageSettings packageSettings)
        {
            var startTime = DateTime.Now;
            var globalSettings = ResourceBuilderSettings.Instance;

            try
            {
                // 输出路径包含版本文件夹: Build/AssetBundles/PackageName/Version/
                var outputPath = Path.Combine(globalSettings.outputPath, packageSettings.packageName, packageSettings.version);
                var success = SBPBuildPipeline.Build(
                    packageSettings,
                    outputPath,
                    globalSettings.buildTarget,
                    globalSettings.forceRebuild
                );

                var endTime = DateTime.Now;
                var buildTime = (float)(endTime - startTime).TotalSeconds;

                if (success)
                {
                    // 读取构建结果
                    var builtBundles = Directory.GetFiles(outputPath, "*" + globalSettings.bundleExtension);
                    var bundleSizes = new Dictionary<string, long>();
                    long totalSize = 0;

                    foreach (var bundlePath in builtBundles)
                    {
                        var fileInfo = new FileInfo(bundlePath);
                        var bundleName = Path.GetFileName(bundlePath);
                        bundleSizes[bundleName] = fileInfo.Length;
                        totalSize += fileInfo.Length;
                    }

                    // 从最新的构建历史中获取 Bundle-Asset 映射
                    var bundleAssets = new Dictionary<string, List<string>>();
                    int totalAssets = 0;
                    
                    var latestRecord = BuildHistoryManager.GetLatestBuildRecord(packageSettings.packageName);
                    if (latestRecord != null && latestRecord.bundles != null)
                    {
                        foreach (var bundle in latestRecord.bundles)
                        {
                            if (bundle.assetPaths != null)
                            {
                                bundleAssets[bundle.bundleName] = bundle.assetPaths;
                                totalAssets += bundle.assetPaths.Count;
                            }
                        }
                    }

                    return new BuildReport
                    {
                        success = true,
                        message = "SBP build completed successfully",
                        totalBundles = builtBundles.Length,
                        totalAssets = totalAssets,
                        totalSize = totalSize,
                        buildTime = buildTime,
                        bundleSizes = bundleSizes,
                        bundleAssets = bundleAssets,
                        outputPath = outputPath
                    };
                }
                else
                {
                    return new BuildReport
                    {
                        success = false,
                        message = "SBP build failed. Check console for details.",
                        buildTime = buildTime,
                        outputPath = outputPath
                    };
                }
            }
            catch (Exception ex)
            {
                var endTime = DateTime.Now;
                var buildTime = (float)(endTime - startTime).TotalSeconds;

                return new BuildReport
                {
                    success = false,
                    message = $"SBP build exception: {ex.Message}",
                    buildTime = buildTime
                };
            }
        }
    }
}
