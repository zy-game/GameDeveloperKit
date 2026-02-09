using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 远程资源包加载器
    /// 用于纯网络资源（HTTP/HTTPS），无需下载
    /// </summary>
    public class RemotePackageLoader : IPackageLoader
    {
        private readonly string _packageName;

        public RemotePackageLoader(string packageName)
        {
            _packageName = packageName;
        }

        /// <summary>
        /// 加载清单（远程资源无需清单）
        /// </summary>
        public UniTask<PackageManifest> LoadManifestAsync()
        {
            Game.Debug.Debug($"[{_packageName}] Remote package, no manifest needed");
            var manifest = new PackageManifest();
            manifest.name = _packageName;
            manifest.version = "remote";
            manifest.bundles = Array.Empty<BundleManifest>();
            return UniTask.FromResult(manifest);
        }

        /// <summary>
        /// 准备资源（远程资源无需准备）
        /// </summary>
        public UniTask<bool> PrepareResourcesAsync(PackageManifest manifest)
        {
            Game.Debug.Debug($"[{_packageName}] Remote package, no preparation needed");
            return UniTask.FromResult(true);
        }
    }
}