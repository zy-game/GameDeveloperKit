using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 版本管理器
    /// 负责管理资源版本和URL生成
    /// </summary>
    public class VersionManager
    {
        private VersionManifest _globalManifest;
        private string _baseUrl;
        private readonly Dictionary<string, string> _packageVersions = new Dictionary<string, string>();

        /// <summary>
        /// 设置基础URL
        /// </summary>
        /// <param name="url">CDN基础URL，例如: https://cdn.example.com/bundles</param>
        public void SetBaseUrl(string url)
        {
            _baseUrl = url?.TrimEnd('/');
            Game.Debug.Debug($"[VersionManager] Base URL set to: {_baseUrl}");
        }

        /// <summary>
        /// 获取基础URL
        /// </summary>
        public string GetBaseUrl()
        {
            return _baseUrl;
        }

        /// <summary>
        /// 设置某个package使用的版本
        /// </summary>
        /// <param name="packageName">Package名称</param>
        /// <param name="version">版本号</param>
        public void SetPackageVersion(string packageName, string version)
        {
            _packageVersions[packageName] = version;
            Game.Debug.Debug($"[VersionManager] Package '{packageName}' version set to: {version}");
        }

        /// <summary>
        /// 获取package当前使用的版本
        /// </summary>
        public string GetPackageVersion(string packageName)
        {
            // 优先使用手动设置的版本
            if (_packageVersions.TryGetValue(packageName, out var version))
            {
                return version;
            }

            // 否则使用清单中的currentVersion
            var info = GetPackageInfo(packageName);
            if (info != null)
            {
                return info.currentVersion;
            }

            // 默认版本
            Game.Debug.Warning($"[VersionManager] No version found for package '{packageName}', using default: 1.0.0");
            return "1.0.0";
        }

        /// <summary>
        /// 加载全局清单
        /// </summary>
        public async UniTask<bool> LoadGlobalManifestAsync()
        {
            if (string.IsNullOrEmpty(_baseUrl))
            {
                Game.Debug.Warning("[VersionManager] Base URL not set, cannot load global manifest");
                return false;
            }

            var manifestUrl = $"{_baseUrl}/manifest.json";
            Game.Debug.Debug($"[VersionManager] Loading global manifest from: {manifestUrl}");

            try
            {
                using (var request = UnityWebRequest.Get(manifestUrl))
                {
                    await request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                    if (request.result == UnityWebRequest.Result.Success)
#else
                    if (!request.isHttpError && !request.isNetworkError)
#endif
                    {
                        var json = request.downloadHandler.text;
                        _globalManifest = JsonUtility.FromJson<VersionManifest>(json);

                        Game.Debug.Debug($"[VersionManager] Global manifest loaded successfully");
                        Game.Debug.Debug($"[VersionManager] Found {_globalManifest.packages?.Length ?? 0} packages");

                        return true;
                    }
                    else
                    {
                        Game.Debug.Warning($"[VersionManager] Failed to load global manifest: {request.error}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Game.Debug.Error($"[VersionManager] Exception loading global manifest: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从StreamingAssets加载全局清单
        /// </summary>
        public async UniTask<bool> LoadGlobalManifestFromStreamingAssetsAsync()
        {
            var manifestPath = Path.Combine(Application.streamingAssetsPath, "manifest.json");
            Game.Debug.Debug($"[VersionManager] Loading global manifest from StreamingAssets: {manifestPath}");

            try
            {
                using (var request = UnityWebRequest.Get(manifestPath))
                {
                    await request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                    if (request.result == UnityWebRequest.Result.Success)
#else
                    if (!request.isHttpError && !request.isNetworkError)
#endif
                    {
                        var json = request.downloadHandler.text;
                        _globalManifest = JsonUtility.FromJson<VersionManifest>(json);

                        Game.Debug.Debug($"[VersionManager] Global manifest loaded from StreamingAssets");
                        Game.Debug.Debug($"[VersionManager] Found {_globalManifest.packages?.Length ?? 0} packages");

                        return true;
                    }
                    else
                    {
                        Game.Debug.Warning($"[VersionManager] Failed to load global manifest from StreamingAssets: {request.error}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Game.Debug.Error($"[VersionManager] Exception loading global manifest from StreamingAssets: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取package的清单URL
        /// </summary>
        public string GetPackageManifestUrl(string packageName)
        {
            var version = GetPackageVersion(packageName);
            var manifestFileName = $"{packageName.ToLower()}.json";

            // 返回: {baseUrl}/PackageName/1.0.0/packagename.json
            var url = $"{_baseUrl}/{packageName}/{version}/{manifestFileName}";

            Game.Debug.Debug($"[VersionManager] Package manifest URL: {url}");
            return url;
        }

        /// <summary>
        /// 获取bundle的下载URL
        /// </summary>
        public string GetBundleDownloadUrl(string packageName, string bundleName)
        {
            var version = GetPackageVersion(packageName);

            // 返回: {baseUrl}/PackageName/1.0.0/bundlename.bundle
            var url = $"{_baseUrl}/{packageName}/{version}/{bundleName}";

            return url;
        }

        /// <summary>
        /// 获取StreamingAssets中package清单的路径
        /// </summary>
        public string GetStreamingAssetsManifestPath(string packageName)
        {
            var version = GetPackageVersion(packageName);
            var manifestFileName = $"{packageName.ToLower()}.json";

            // 返回: StreamingAssets/PackageName/1.0.0/packagename.json
            var path = Path.Combine(Application.streamingAssetsPath, "AssetBundles", packageName, manifestFileName);

            Game.Debug.Debug($"[VersionManager] StreamingAssets manifest path: {path}");
            return path;
        }

        /// <summary>
        /// 获取StreamingAssets中bundle的路径
        /// </summary>
        public string GetStreamingAssetsBundlePath(string packageName, string bundleName)
        {
            var version = GetPackageVersion(packageName);

            // 返回: StreamingAssets/PackageName/1.0.0/bundlename.bundle
            var path = Path.Combine(Application.streamingAssetsPath, "AssetBundles", packageName, bundleName);

            return path;
        }

        /// <summary>
        /// 获取package的版本信息
        /// </summary>
        public PackageVersionInfo GetPackageInfo(string packageName)
        {
            if (_globalManifest == null || _globalManifest.packages == null)
            {
                return null;
            }

            return _globalManifest.packages.FirstOrDefault(p => p.name == packageName);
        }

        /// <summary>
        /// 获取全局清单
        /// </summary>
        public VersionManifest GetGlobalManifest()
        {
            return _globalManifest;
        }

        /// <summary>
        /// 清理
        /// </summary>
        public void Clear()
        {
            _globalManifest = null;
            _packageVersions.Clear();
            Game.Debug.Debug("[VersionManager] Cleared");
        }
    }
}
