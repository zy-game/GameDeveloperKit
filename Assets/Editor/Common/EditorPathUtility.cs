using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameDeveloperKit.Editor
{
    /// <summary>
    /// 编辑器资源路径工具类
    /// 自动处理项目内开发和作为包引用时的路径差异
    /// </summary>
    public static class EditorPathUtility
    {
        private const string PACKAGE_NAME = "com.gamedeveloperkit.framework";
        
        // 项目内开发时的路径前缀
        private const string ASSETS_PATH_PREFIX = "Assets/GameFramework/";
        
        // 作为包引用时的路径前缀
        private static readonly string PACKAGE_PATH_PREFIX = $"Packages/{PACKAGE_NAME}/";
        
        private static bool? _isPackage;
        
        /// <summary>
        /// 检测当前是否作为包引用运行
        /// </summary>
        public static bool IsRunningAsPackage
        {
            get
            {
                if (_isPackage == null)
                {
                    // 检查包路径是否存在
                    var packagePath = Path.GetFullPath($"Packages/{PACKAGE_NAME}");
                    _isPackage = Directory.Exists(packagePath);
                }
                return _isPackage.Value;
            }
        }
        
        /// <summary>
        /// 获取编辑器资源的正确路径
        /// 自动根据运行环境返回正确的路径
        /// </summary>
        /// <param name="relativePath">相对于 Editor 文件夹的路径，例如 "Common/Style/EditorCommonStyle.uss"</param>
        /// <returns>完整的资源路径</returns>
        public static string GetEditorAssetPath(string relativePath)
        {
            string path;
            if (IsRunningAsPackage)
            {
                path = PACKAGE_PATH_PREFIX + "Editor/" + relativePath;
            }
            else
            {
                path = ASSETS_PATH_PREFIX + "Editor/" + relativePath;
            }
            return path;
        }
        
        /// <summary>
        /// 获取运行时资源的正确路径
        /// </summary>
        /// <param name="relativePath">相对于 Runtime 文件夹的路径</param>
        /// <returns>完整的资源路径</returns>
        public static string GetRuntimeAssetPath(string relativePath)
        {
            string path;
            if (IsRunningAsPackage)
            {
                path = PACKAGE_PATH_PREFIX + "Runtime/" + relativePath;
            }
            else
            {
                path = ASSETS_PATH_PREFIX + "Runtime/" + relativePath;
            }
            return path;
        }
        
        /// <summary>
        /// 加载编辑器样式表
        /// </summary>
        /// <param name="relativePath">相对于 Editor 文件夹的路径</param>
        /// <returns>StyleSheet 或 null</returns>
        public static UnityEngine.UIElements.StyleSheet LoadEditorStyleSheet(string relativePath)
        {
            var path = GetEditorAssetPath(relativePath);
            var styleSheet = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>(path);
            
            if (styleSheet == null)
            {
                Debug.LogWarning($"[EditorPathUtility] Failed to load StyleSheet: {path}");
            }
            
            return styleSheet;
        }
        
        /// <summary>
        /// 加载编辑器 VisualTreeAsset (UXML)
        /// </summary>
        /// <param name="relativePath">相对于 Editor 文件夹的路径</param>
        /// <returns>VisualTreeAsset 或 null</returns>
        public static UnityEngine.UIElements.VisualTreeAsset LoadEditorVisualTree(string relativePath)
        {
            var path = GetEditorAssetPath(relativePath);
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(path);
            
            if (asset == null)
            {
                Debug.LogWarning($"[EditorPathUtility] Failed to load VisualTreeAsset: {path}");
            }
            
            return asset;
        }
        
        /// <summary>
        /// 加载编辑器资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="relativePath">相对于 Editor 文件夹的路径</param>
        /// <returns>资源或 null</returns>
        public static T LoadEditorAsset<T>(string relativePath) where T : Object
        {
            var path = GetEditorAssetPath(relativePath);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            
            if (asset == null)
            {
                Debug.LogWarning($"[EditorPathUtility] Failed to load asset: {path}");
            }
            
            return asset;
        }
    }
}
