using System;
using System.IO;
using System.Linq;
using GameDeveloperKit.Resource;
using UnityEngine;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 全局清单编辑器工具
    /// 提供编辑器中操作全局清单的便捷方法
    /// </summary>
    public static class GlobalManifestEditor
    {
        /// <summary>
        /// 加载全局清单
        /// </summary>
        public static VersionManifest LoadGlobalManifest()
        {
            var settings = ResourceBuilderSettings.Instance;
            var manifestPath = Path.Combine(settings.outputPath, "manifest.json");
            
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"[GlobalManifestEditor] 全局清单文件不存在: {manifestPath}");
                return null;
            }
            
            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonUtility.FromJson<VersionManifest>(json);
                
                if (manifest != null)
                {
                    Debug.Log($"[GlobalManifestEditor] 已加载全局清单，包含 {manifest.packages?.Length ?? 0} 个package");
                }
                
                return manifest;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GlobalManifestEditor] 加载全局清单失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 保存全局清单
        /// </summary>
        public static void SaveGlobalManifest(VersionManifest manifest)
        {
            if (manifest == null)
            {
                Debug.LogError("[GlobalManifestEditor] 无法保存空的全局清单");
                return;
            }
            
            var settings = ResourceBuilderSettings.Instance;
            var manifestPath = Path.Combine(settings.outputPath, "manifest.json");
            
            try
            {
                // 更新时间戳
                manifest.updateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                
                var json = JsonUtility.ToJson(manifest, true);
                
                // 确保目录存在
                var directory = Path.GetDirectoryName(manifestPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                File.WriteAllText(manifestPath, json);
                Debug.Log($"[GlobalManifestEditor] 全局清单已保存: {manifestPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GlobalManifestEditor] 保存全局清单失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 设置package的currentVersion
        /// </summary>
        /// <param name="packageName">Package名称</param>
        /// <param name="version">版本号</param>
        /// <returns>是否设置成功</returns>
        public static bool SetPackageCurrentVersion(string packageName, string version)
        {
            var manifest = LoadGlobalManifest();
            if (manifest == null)
            {
                Debug.LogError("[GlobalManifestEditor] 无法加载全局清单");
                return false;
            }
            
            var packageInfo = manifest.packages?.FirstOrDefault(p => p.name == packageName);
            if (packageInfo == null)
            {
                Debug.LogError($"[GlobalManifestEditor] 未找到Package: {packageName}");
                return false;
            }
            
            // 验证版本是否存在于历史版本中
            if (packageInfo.versions == null || !packageInfo.versions.Any(v => v.version == version))
            {
                Debug.LogError($"[GlobalManifestEditor] 版本 {version} 不存在于Package {packageName} 的历史版本中");
                return false;
            }
            
            packageInfo.currentVersion = version;
            SaveGlobalManifest(manifest);
            
            Debug.Log($"[GlobalManifestEditor] Package '{packageName}' 的currentVersion已设置为: {version}");
            return true;
        }
        
        /// <summary>
        /// 获取package的所有可用版本
        /// </summary>
        /// <param name="packageName">Package名称</param>
        /// <returns>版本号数组</returns>
        public static string[] GetAvailableVersions(string packageName)
        {
            var manifest = LoadGlobalManifest();
            if (manifest == null)
                return new string[0];
            
            var packageInfo = manifest.packages?.FirstOrDefault(p => p.name == packageName);
            if (packageInfo == null || packageInfo.versions == null)
                return new string[0];
            
            return packageInfo.versions.Select(v => v.version).ToArray();
        }
        
        /// <summary>
        /// 获取package的版本信息
        /// </summary>
        /// <param name="packageName">Package名称</param>
        /// <returns>PackageVersionInfo，如果不存在则返回null</returns>
        public static PackageVersionInfo GetPackageInfo(string packageName)
        {
            var manifest = LoadGlobalManifest();
            if (manifest == null)
                return null;
            
            return manifest.packages?.FirstOrDefault(p => p.name == packageName);
        }
        
        /// <summary>
        /// 获取特定版本的详细信息
        /// </summary>
        /// <param name="packageName">Package名称</param>
        /// <param name="version">版本号</param>
        /// <returns>VersionDetail，如果不存在则返回null</returns>
        public static VersionDetail GetVersionDetail(string packageName, string version)
        {
            var packageInfo = GetPackageInfo(packageName);
            if (packageInfo == null || packageInfo.versions == null)
                return null;
            
            return packageInfo.versions.FirstOrDefault(v => v.version == version);
        }
    }
}
