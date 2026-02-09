using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GameDeveloperKit.Resource;

namespace GameDeveloperKit.Editor.Resource
{
    /// <summary>
    /// 资源包类型
    /// </summary>
    public enum PackageType
    {
        /// <summary>
        /// 首包资源 - 会拷贝到 StreamingAssets，随安装包发布
        /// </summary>
        BasePackage = 0,
        
        /// <summary>
        /// 热更新资源 - 不拷贝到 StreamingAssets，需要运行时下载
        /// </summary>
        HotfixPackage = 1
    }
    
    /// <summary>
    /// 资源包数据（存储在 ProjectSettings）
    /// </summary>
    [Serializable]
    public class ResourcePackagesData
    {
        private const string SETTINGS_PATH = "ProjectSettings/ResourcePackages.json";
        
        public List<PackageSettings> packages = new List<PackageSettings>();
        
        /// <summary>
        /// 数据保存后触发的事件
        /// </summary>
        public static event Action OnDataSaved;
        
        private static ResourcePackagesData _instance;
        public static ResourcePackagesData Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Load();
                return _instance;
            }
        }
        
        public static ResourcePackagesData Load()
        {
            if (File.Exists(SETTINGS_PATH))
            {
                try
                {
                    var json = File.ReadAllText(SETTINGS_PATH);
                    var data = JsonUtility.FromJson<ResourcePackagesData>(json);
                    return data ?? new ResourcePackagesData();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to load ResourcePackages.json: {ex.Message}");
                    return new ResourcePackagesData();
                }
            }
            return new ResourcePackagesData();
        }
        
        public void Save()
        {
            try
            {
                var json = JsonUtility.ToJson(this, true);
                File.WriteAllText(SETTINGS_PATH, json);
                AssetDatabase.Refresh();
                
                // 触发保存事件
                OnDataSaved?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save ResourcePackages.json: {ex.Message}");
            }
        }
        
        public void AddPackage(PackageSettings package)
        {
            if (packages.Any(p => p.packageName == package.packageName))
            {
                Debug.LogWarning($"Package '{package.packageName}' already exists");
                return;
            }
            packages.Add(package);
            Save();
        }
        
        public void RemovePackage(string packageName)
        {
            packages.RemoveAll(p => p.packageName == packageName);
            Save();
        }
        
        public PackageSettings FindPackage(string packageName)
        {
            return packages.Find(p => p.packageName == packageName);
        }
        
        public bool PackageExists(string packageName)
        {
            return packages.Any(p => p.packageName == packageName);
        }
    }
    
    /// <summary>
    /// Package 配置
    /// </summary>
    [Serializable]
    public class PackageSettings
    {
        public string packageName;
        public string version;
        public PackageType packageType = PackageType.HotfixPackage;
        public AddressMode addressMode = AddressMode.FullPath;
        public CompressionType compression;
        
        /// <summary>
        /// 资源收集器
        /// </summary>
        [SerializeReference]
        public IAssetCollector collector;
        
        /// <summary>
        /// 打包策略配置
        /// </summary>
        [SerializeReference]
        public PackStrategyConfig packStrategyConfig;
        
        public PackageSettings()
        {
            packageName = "NewPackage";
            version = "1.0.0";
            packageType = PackageType.BasePackage;
            addressMode = AddressMode.FullPath;
        }
        
        public PackageSettings(string name)
        {
            packageName = name;
            version = "1.0.0";
            packageType = PackageType.BasePackage;
            addressMode = AddressMode.FullPath;
        }
        
        /// <summary>
        /// 获取此 Package 的打包策略
        /// </summary>
        public IPackStrategy GetPackStrategy()
        {
            if (packStrategyConfig != null && packStrategyConfig.enabled)
            {
                return packStrategyConfig.CreateStrategy();
            }
            
            // 默认使用目录打包策略
            return new PackByDirectoryStrategy();
        }
        
        /// <summary>
        /// 收集此 Package 的所有资源
        /// </summary>
        public List<CollectedAsset> CollectAssets()
        {
            if (collector == null)
            {
                Debug.LogWarning($"[PackageSettings] Package '{packageName}' has no collector configured");
                return new List<CollectedAsset>();
            }
            
            var context = new CollectorContext
            {
                PackageName = packageName,
                AddressMode = addressMode,
                BaseDirectory = "Assets"
            };
            
            return collector.Collect(context).ToList();
        }
        
        /// <summary>
        /// 获取打包分组
        /// </summary>
        public Dictionary<string, List<CollectedAsset>> GetBundleGroups()
        {
            var assets = CollectAssets();
            var strategy = GetPackStrategy();
            return strategy.Pack(assets);
        }

        /// <summary>
        /// 生成 PackageManifest（用于 EditorSimulator 模式）
        /// </summary>
        public PackageManifest ToPackageManifest()
        {
            var bundleGroups = GetBundleGroups();
            var bundleList = new List<BundleManifest>();
            
            foreach (var kvp in bundleGroups)
            {
                var bundleName = kvp.Key;
                var assets = kvp.Value;
                
                var assetInfoList = assets.Select(a => new AssetInfo
                {
                    name = a.name,
                    address = a.address,
                    guid = a.guid,
                    path = a.assetPath,
                    labels = a.labels
                }).ToArray();
                
                bundleList.Add(new BundleManifest
                {
                    name = bundleName,
                    version = version,
                    hash = "editor_simulator",
                    size = 0,
                    resources = assetInfoList,
                    dependencies = new string[0]
                });
            }
            
            var manifest = new PackageManifest
            {
                name = packageName,
                version = version,
                bundles = bundleList.ToArray()
            };
            
            Debug.Log($"[PackageSettings] Generated manifest: {packageName} v{version}, {bundleList.Count} bundles");
            return manifest;
        }
    }
}
