using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// SBP 构建缓存管理器
    /// </summary>
    public static class SBPCacheManager
    {
        private static string CachePath => "Library/BuildCache";
        
        /// <summary>
        /// 清理构建缓存
        /// </summary>
        public static void ClearCache()
        {
            if (Directory.Exists(CachePath))
            {
                try
                {
                    Directory.Delete(CachePath, true);
                    Debug.Log("[SBP Cache] Build cache cleared successfully");
                    EditorUtility.DisplayDialog("SBP 缓存", "构建缓存已清理", "确定");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[SBP Cache] Failed to clear cache: {ex.Message}");
                    EditorUtility.DisplayDialog("错误", $"清理缓存失败: {ex.Message}", "确定");
                }
            }
            else
            {
                Debug.Log("[SBP Cache] No cache to clear");
                EditorUtility.DisplayDialog("SBP 缓存", "没有缓存需要清理", "确定");
            }
        }
        
        /// <summary>
        /// 获取缓存大小（字节）
        /// </summary>
        public static long GetCacheSize()
        {
            if (!Directory.Exists(CachePath))
                return 0;
            
            try
            {
                var files = Directory.GetFiles(CachePath, "*", SearchOption.AllDirectories);
                return files.Sum(f => new FileInfo(f).Length);
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// 获取格式化的缓存大小字符串
        /// </summary>
        public static string GetCacheSizeFormatted()
        {
            var size = GetCacheSize();
            
            if (size < 1024)
                return $"{size} B";
            else if (size < 1024 * 1024)
                return $"{size / 1024.0:F2} KB";
            else if (size < 1024 * 1024 * 1024)
                return $"{size / 1024.0 / 1024.0:F2} MB";
            else
                return $"{size / 1024.0 / 1024.0 / 1024.0:F2} GB";
        }
        
        /// <summary>
        /// 显示缓存信息
        /// </summary>
        public static void ShowCacheInfo()
        {
            var size = GetCacheSizeFormatted();
            var exists = Directory.Exists(CachePath);
            
            var message = exists 
                ? $"缓存路径: {CachePath}\n缓存大小: {size}" 
                : "缓存不存在";
            
            EditorUtility.DisplayDialog("SBP 缓存信息", message, "确定");
            Debug.Log($"[SBP Cache] {message}");
        }
    }
}
