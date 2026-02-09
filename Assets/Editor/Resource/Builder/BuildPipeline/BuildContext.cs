using System.Collections.Generic;
using UnityEditor;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 构建上下文 - 在任务间共享数据
    /// </summary>
    public class BuildContext
    {
        // ========== 构建参数 ==========
        public string OutputPath { get; set; }
        public BuildTarget BuildTarget { get; set; }
        public string PackageName { get; set; }
        public string PackageVersion { get; set; }
        public bool ForceRebuild { get; set; }
        public BuildAssetBundleOptions BuildOptions { get; set; }

        // ========== 包设置 ==========
        public PackageSettings PackageSettings { get; set; }
        
        // ========== 全局设置 ==========
        public ResourceBuilderSettings GlobalSettings { get; set; }

        // ========== 资源收集结果 ==========
        public List<CollectedAsset> CollectedAssets { get; set; }
        public Dictionary<string, List<CollectedAsset>> BundleGroups { get; set; }
        public Dictionary<string, AssetBundleBuild> AssetBundleBuilds { get; set; }

        // ========== SBP 特定数据 ==========
        public UnityEngine.BuildCompression Compression { get; set; }
        public UnityEditor.Build.Pipeline.Interfaces.IBundleBuildResults SBPBuildResults { get; set; }

        // ========== 构建结果 ==========
        public List<string> BuiltBundles { get; set; }
        public Dictionary<string, long> BundleSizes { get; set; }
        public long TotalSize { get; set; }
        
        // ========== Bundle-Asset 映射 (用于历史记录) ==========
        public Dictionary<string, List<string>> BundleAssetMap { get; set; }

        // ========== 日志和报告 ==========
        public List<string> BuildLogs { get; set; }
        public System.DateTime BuildStartTime { get; set; }
        public System.DateTime BuildEndTime { get; set; }

        // ========== 自定义数据 ==========
        private Dictionary<string, object> _customData;

        public BuildContext()
        {
            CollectedAssets = new List<CollectedAsset>();
            BundleGroups = new Dictionary<string, List<CollectedAsset>>();
            AssetBundleBuilds = new Dictionary<string, AssetBundleBuild>();
            BuiltBundles = new List<string>();
            BundleSizes = new Dictionary<string, long>();
            BundleAssetMap = new Dictionary<string, List<string>>();
            BuildLogs = new List<string>();
            _customData = new Dictionary<string, object>();
        }

        /// <summary>
        /// 设置自定义数据
        /// </summary>
        public void SetContextObject(string key, object value)
        {
            _customData[key] = value;
        }

        /// <summary>
        /// 获取自定义数据
        /// </summary>
        public T GetContextObject<T>(string key) where T : class
        {
            if (_customData.TryGetValue(key, out var value))
            {
                return value as T;
            }
            return null;
        }

        /// <summary>
        /// 添加日志
        /// </summary>
        public void Log(string message)
        {
            var timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}";
            BuildLogs.Add(logMessage);
            UnityEngine.Debug.Log($"[Build] {message}");
        }

        /// <summary>
        /// 添加警告日志
        /// </summary>
        public void LogWarning(string message)
        {
            var timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] WARNING: {message}";
            BuildLogs.Add(logMessage);
            UnityEngine.Debug.LogWarning($"[Build] {message}");
        }

        /// <summary>
        /// 添加错误日志
        /// </summary>
        public void LogError(string message)
        {
            var timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] ERROR: {message}";
            BuildLogs.Add(logMessage);
            UnityEngine.Debug.LogError($"[Build] {message}");
        }
    }
}
