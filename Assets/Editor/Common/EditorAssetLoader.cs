using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeveloperKit.Editor
{
    /// <summary>
    /// 编辑器资源加载工具类
    /// 自动处理项目内开发和作为 Package 引用时的路径差异
    /// </summary>
    public static class EditorAssetLoader
    {
        private const string PACKAGE_NAME = "com.gamedeveloperkit.framework";
        
        // 项目内开发时的路径前缀
        private const string ASSETS_EDITOR_PREFIX = "Assets/Editor/";
        
        // 作为包引用时的路径前缀
        private static readonly string PACKAGE_EDITOR_PREFIX = $"Packages/{PACKAGE_NAME}/Editor/";
        
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
        /// 加载编辑器样式表
        /// 自动尝试项目内路径和 Package 路径
        /// </summary>
        /// <param name="relativePath">相对于 Editor 文件夹的路径，例如 "Common/Style/EditorCommonStyle.uss"</param>
        /// <returns>StyleSheet 或 null</returns>
        public static StyleSheet LoadStyleSheet(string relativePath)
        {
            // 尝试项目内路径
            var projectPath = ASSETS_EDITOR_PREFIX + relativePath;
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(projectPath);
            
            if (styleSheet != null)
                return styleSheet;
            
            // 尝试 Package 路径
            var packagePath = PACKAGE_EDITOR_PREFIX + relativePath;
            styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(packagePath);
            
            if (styleSheet == null)
            {
                Debug.LogWarning($"[EditorAssetLoader] 无法加载样式表: {relativePath}");
            }
            
            return styleSheet;
        }
        
        /// <summary>
        /// 加载编辑器 VisualTreeAsset (UXML)
        /// 自动尝试项目内路径和 Package 路径
        /// </summary>
        /// <param name="relativePath">相对于 Editor 文件夹的路径</param>
        /// <returns>VisualTreeAsset 或 null</returns>
        public static VisualTreeAsset LoadVisualTree(string relativePath)
        {
            // 尝试项目内路径
            var projectPath = ASSETS_EDITOR_PREFIX + relativePath;
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(projectPath);
            
            if (asset != null)
                return asset;
            
            // 尝试 Package 路径
            var packagePath = PACKAGE_EDITOR_PREFIX + relativePath;
            asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(packagePath);
            
            if (asset != null)
                return asset;
            
            Debug.LogError($"[EditorAssetLoader] 无法加载 VisualTreeAsset: {relativePath}\n尝试的路径:\n  - {projectPath}\n  - {packagePath}");
            return null;
        }
        
        /// <summary>
        /// 加载编辑器资源（通用方法）
        /// 自动尝试项目内路径和 Package 路径
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="relativePath">相对于 Editor 文件夹的路径</param>
        /// <returns>资源或 null</returns>
        public static T LoadAsset<T>(string relativePath) where T : Object
        {
            // 尝试项目内路径
            var projectPath = ASSETS_EDITOR_PREFIX + relativePath;
            var asset = AssetDatabase.LoadAssetAtPath<T>(projectPath);
            
            if (asset != null)
                return asset;
            
            // 尝试 Package 路径
            var packagePath = PACKAGE_EDITOR_PREFIX + relativePath;
            asset = AssetDatabase.LoadAssetAtPath<T>(packagePath);
            
            if (asset == null)
            {
                Debug.LogWarning($"[EditorAssetLoader] 无法加载资源: {relativePath}");
            }
            
            return asset;
        }
        
        /// <summary>
        /// 获取编辑器资源的完整路径
        /// 自动根据运行环境返回正确的路径
        /// </summary>
        /// <param name="relativePath">相对于 Editor 文件夹的路径</param>
        /// <returns>完整的资源路径</returns>
        public static string GetEditorAssetPath(string relativePath)
        {
            // 先尝试项目内路径
            var projectPath = ASSETS_EDITOR_PREFIX + relativePath;
            if (File.Exists(projectPath) || Directory.Exists(projectPath))
                return projectPath;
            
            // 再尝试 Package 路径
            var packagePath = PACKAGE_EDITOR_PREFIX + relativePath;
            if (File.Exists(packagePath) || Directory.Exists(packagePath))
                return packagePath;
            
            // 默认返回项目内路径
            return projectPath;
        }
    }
}
