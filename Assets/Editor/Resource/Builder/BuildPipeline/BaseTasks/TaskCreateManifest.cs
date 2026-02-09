using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameDeveloperKit.Resource;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 任务：创建清单文件
    /// </summary>
    public class TaskCreateManifest : IBuildTask
    {
        public string TaskName => "Create Manifest";

        public TaskResult Run(BuildContext context)
        {
            context.Log("开始创建清单文件...");

            try
            {
                var manifest = new PackageManifest
                {
                    name = context.PackageName,
                    version = context.PackageVersion,
                };

                var settings = ResourceBuilderSettings.Instance;
                var bundleExtension = settings.bundleExtension;

                // 加载之前的 manifest（如果存在）用于版本比较
                var previousManifest = LoadPreviousManifest(context);

                // 添加 Bundle 信息
                var bundleList = new List<BundleManifest>();
                
                foreach (var bundleFileName in context.BuiltBundles)
                {
                    var bundlePath = Path.Combine(context.OutputPath, bundleFileName);
                    if (!File.Exists(bundlePath))
                    {
                        context.LogWarning($"Bundle 文件不存在: {bundleFileName}");
                        continue;
                    }

                    // 获取不带扩展名的 bundle 名称
                    var bundleNameWithoutExt = bundleFileName.Replace(bundleExtension, "");

                    var fileInfo = new FileInfo(bundlePath);
                    var currentHash = ComputeFileHash(bundlePath);
                    
                    // 计算 bundle 版本：如果 hash 变化则提升版本
                    var bundleVersion = CalculateBundleVersion(bundleFileName, currentHash, previousManifest, context.PackageVersion);

                    var bundleInfo = new BundleManifest
                    {
                        name = bundleFileName,
                        version = bundleVersion,
                        hash = currentHash,
                        size = fileInfo.Length,
                        resources = GetBundleResources(context, bundleNameWithoutExt),
                        dependencies = GetBundleDependencies(context, bundleNameWithoutExt, bundleExtension)
                    };

                    bundleList.Add(bundleInfo);
                }

                manifest.bundles = bundleList.ToArray();

                // 保存清单（文件名全小写）
                var manifestFileName = $"{context.PackageName.ToLower()}.json";
                var manifestPath = Path.Combine(context.OutputPath, manifestFileName);
                var json = JsonUtility.ToJson(manifest, true);
                File.WriteAllText(manifestPath, json);

                context.Log($"清单文件已创建: {manifestPath}");
                context.Log($"  包含 {manifest.bundles.Length} 个 Bundle");

                return TaskResult.Succeed();
            }
            catch (System.Exception ex)
            {
                return TaskResult.Failed($"创建清单文件时发生错误: {ex.Message}");
            }
        }

        private AssetInfo[] GetBundleResources(BuildContext context, string bundleName)
        {
            // 从 AssetBundleBuilds 中获取资源信息
            if (!context.AssetBundleBuilds.TryGetValue(bundleName, out var bundleBuild))
            {
                return new AssetInfo[0];
            }

            var resources = new List<AssetInfo>();
            foreach (var assetPath in bundleBuild.assetNames)
            {
                var guid = AssetDatabase.AssetPathToGUID(assetPath);
                var assetName = Path.GetFileNameWithoutExtension(assetPath);
                
                // 从 addressableNames 中查找地址，如果没有则使用资源名称
                var address = assetName;
                if (bundleBuild.addressableNames != null)
                {
                    var index = System.Array.IndexOf(bundleBuild.assetNames, assetPath);
                    if (index >= 0 && index < bundleBuild.addressableNames.Length)
                    {
                        address = bundleBuild.addressableNames[index];
                    }
                }

                // 从 PackageSettings 中查找资源所属的 Group，获取标签
                var labels = GetAssetLabels(context, assetPath);

                resources.Add(new AssetInfo
                {
                    name = assetName,
                    address = address,
                    guid = guid,
                    path = assetPath,
                    labels = labels
                });
            }

            return resources.ToArray();
        }

        private string[] GetAssetLabels(BuildContext context, string assetPath)
        {
            // 从收集的资源中查找标签
            if (context.CollectedAssets != null)
            {
                var asset = context.CollectedAssets.FirstOrDefault(a => a.assetPath == assetPath);
                if (asset != null && asset.labels != null && asset.labels.Length > 0)
                {
                    return asset.labels;
                }
            }

            return new string[0];
        }

        private string[] GetBundleDependencies(BuildContext context, string bundleName, string bundleExtension)
        {
            // 从 SBP 构建结果中获取依赖信息
            if (context.SBPBuildResults == null)
            {
                return new string[0];
            }

            if (!context.SBPBuildResults.BundleInfos.TryGetValue(bundleName, out var bundleInfo))
            {
                return new string[0];
            }

            // 添加扩展名到依赖的 bundle 名称
            if (bundleInfo.Dependencies != null && bundleInfo.Dependencies.Length > 0)
            {
                return bundleInfo.Dependencies.Select(dep => dep + bundleExtension).ToArray();
            }

            return new string[0];
        }

        private PackageManifest LoadPreviousManifest(BuildContext context)
        {
            try
            {
                var manifestFileName = $"{context.PackageName.ToLower()}.json";
                var manifestPath = Path.Combine(context.OutputPath, manifestFileName);
                
                if (!File.Exists(manifestPath))
                {
                    return null;
                }

                var json = File.ReadAllText(manifestPath);
                return JsonUtility.FromJson<PackageManifest>(json);
            }
            catch (System.Exception ex)
            {
                context.LogWarning($"无法加载之前的 manifest: {ex.Message}");
                return null;
            }
        }

        private string CalculateBundleVersion(string bundleName, string currentHash, PackageManifest previousManifest, string packageVersion)
        {
            // 如果没有之前的 manifest，使用 package 版本
            if (previousManifest == null || previousManifest.bundles == null)
            {
                return packageVersion;
            }

            // 查找之前的 bundle 信息
            var previousBundle = System.Array.Find(previousManifest.bundles, b => b.name == bundleName);
            if (previousBundle == null)
            {
                // 新 bundle，使用 package 版本
                return packageVersion;
            }

            // 如果 hash 没有变化，保持原版本
            if (previousBundle.hash == currentHash)
            {
                return previousBundle.version;
            }

            // hash 变化了，提升版本号
            return IncrementVersion(previousBundle.version);
        }

        private string IncrementVersion(string version)
        {
            // 解析版本号 (例如: "1.0.0" -> [1, 0, 0])
            var parts = version.Split('.');
            if (parts.Length != 3)
            {
                // 版本格式不正确，返回默认版本
                return "1.0.1";
            }

            if (int.TryParse(parts[0], out int major) &&
                int.TryParse(parts[1], out int minor) &&
                int.TryParse(parts[2], out int patch))
            {
                // 提升补丁版本号
                patch++;
                return $"{major}.{minor}.{patch}";
            }

            return "1.0.1";
        }

        private string ComputeFileHash(string filePath)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = md5.ComputeHash(stream);
                    return System.BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }
    }
}
