#if UNITY_EDITOR
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 编辑器模拟加载器
    /// 用于开发阶段，直接从 AssetDatabase 加载资源，无需打包 AssetBundle
    /// </summary>
    public class EditorSimulatorLoader : IPackageLoader
    {
        private readonly string _packageName;

        public EditorSimulatorLoader(string packageName)
        {
            _packageName = packageName;
        }

        /// <summary>
        /// 通过反射调用编辑器数据类生成 Manifest
        /// </summary>
        public UniTask<PackageManifest> LoadManifestAsync()
        {
            Game.Debug.Debug($"[EditorSimulator] Generating manifest for package: {_packageName}");
            
            try
            {
                var manifest = GenerateManifestFromEditorData();
                
                if (manifest == null)
                {
                    Game.Debug.Error($"[EditorSimulator] Failed to generate manifest for package: {_packageName}");
                    Game.Debug.Warning($"[EditorSimulator] Please configure package '{_packageName}' in GameFramework > Resource > Packages");
                    return UniTask.FromResult<PackageManifest>(null);
                }
                
                Game.Debug.Debug($"[EditorSimulator] Manifest generated successfully: {manifest.name} v{manifest.version}, {manifest.bundles?.Length ?? 0} bundles");
                return UniTask.FromResult(manifest);
            }
            catch (System.Exception ex)
            {
                Game.Debug.Error($"[EditorSimulator] Exception generating manifest: {ex.Message}\n{ex.StackTrace}");
                return UniTask.FromResult<PackageManifest>(null);
            }
        }

        /// <summary>
        /// 通过反射从编辑器配置生成清单
        /// </summary>
        private PackageManifest GenerateManifestFromEditorData()
        {
            try
            {
                // 使用反射访问编辑器程序集
                var editorAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "GameFrameworkKit.Editor");
                
                if (editorAssembly == null)
                {
                    Game.Debug.Error("[EditorSimulator] Cannot find GameFrameworkKit.Editor assembly");
                    return null;
                }
                
                // 获取 ResourcePackagesData.Instance
                var packagesDataType = editorAssembly.GetType("GameDeveloperKit.Editor.Resource.ResourcePackagesData");
                var instanceProperty = packagesDataType?.GetProperty("Instance", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var packagesDataInstance = instanceProperty?.GetValue(null);
                
                if (packagesDataInstance == null)
                {
                    Game.Debug.Error("[EditorSimulator] Cannot get ResourcePackagesData.Instance");
                    return null;
                }
                
                // 获取 packages 列表
                var packagesField = packagesDataType.GetField("packages");
                var packagesList = packagesField?.GetValue(packagesDataInstance) as System.Collections.IList;
                
                if (packagesList == null)
                {
                    Game.Debug.Warning("[EditorSimulator] No packages configured");
                    return null;
                }
                
                // 查找目标 package
                var packageSettingsType = editorAssembly.GetType("GameDeveloperKit.Editor.Resource.PackageSettings");
                var packageNameField = packageSettingsType?.GetField("packageName");
                var toPackageManifestMethod = packageSettingsType?.GetMethod("ToPackageManifest");
                
                if (toPackageManifestMethod == null)
                {
                    Game.Debug.Error("[EditorSimulator] Cannot find PackageSettings.ToPackageManifest method");
                    return null;
                }
                
                object targetPackage = null;
                foreach (var pkg in packagesList)
                {
                    var pkgName = packageNameField?.GetValue(pkg) as string;
                    if (pkgName == _packageName)
                    {
                        targetPackage = pkg;
                        break;
                    }
                }
                
                if (targetPackage == null)
                {
                    Game.Debug.Warning($"[EditorSimulator] Package '{_packageName}' not found in configuration");
                    return null;
                }
                
                // 调用 ToPackageManifest() 方法
                var manifest = toPackageManifestMethod.Invoke(targetPackage, null) as PackageManifest;
                
                return manifest;
            }
            catch (System.Exception ex)
            {
                Game.Debug.Error($"[EditorSimulator] Exception in GenerateManifestFromEditorData: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// 编辑器模式无需准备资源
        /// </summary>
        public UniTask<bool> PrepareResourcesAsync(PackageManifest manifest)
        {
            Game.Debug.Debug($"[EditorSimulator] No preparation needed for package: {_packageName}");
            return UniTask.FromResult(true);
        }
    }
}
#endif
