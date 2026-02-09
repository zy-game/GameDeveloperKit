using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 内置资源包加载器
    /// 用于 Resources 资源，无需下载
    /// </summary>
    public class BuiltinPackageLoader : IPackageLoader
    {
        private readonly string _packageName;

        public BuiltinPackageLoader(string packageName)
        {
            _packageName = packageName;
        }

        /// <summary>
        /// 加载清单（内置资源无需清单）
        /// </summary>
        public UniTask<PackageManifest> LoadManifestAsync()
        {
            Game.Debug.Debug($"[{_packageName}] Builtin package, no manifest needed");
            var manifest = new PackageManifest();
            manifest.name = _packageName;
            manifest.version = "builtin";
            manifest.bundles = Array.Empty<BundleManifest>();
            return UniTask.FromResult(manifest);
        }

        /// <summary>
        /// 准备资源（内置资源无需准备）
        /// </summary>
        public UniTask<bool> PrepareResourcesAsync(PackageManifest manifest)
        {
            Game.Debug.Debug($"[{_packageName}] Builtin package, no preparation needed");
            return UniTask.FromResult(true);
        }
    }
}