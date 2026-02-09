using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Network;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// AssetBundle 包加载器
    /// 支持从 StreamingAssets（母包资源）和网络（热更新）混合加载
    /// </summary>
    public class BundlePackageLoader : IPackageLoader
    {
        private readonly string _packageName;
        private readonly VersionManager _versionManager;
        private readonly bool _enableRemote;

        public BundlePackageLoader(string packageName, VersionManager versionManager, bool enableRemote = true)
        {
            _packageName = packageName;
            _versionManager = versionManager;
            _enableRemote = enableRemote;
        }

        /// <summary>
        /// 异步加载清单（优先从 StreamingAssets，然后从网络）
        /// </summary>
        public async UniTask<PackageManifest> LoadManifestAsync()
        {
            PackageManifest manifest = null;
            // 1. 优先尝试从 StreamingAssets 加载 Manifest（母包资源）
            manifest = await TryLoadManifestFromStreamingAssets();
            if (manifest != null)
            {
                Game.Debug.Debug($"[{_packageName}] Manifest loaded from StreamingAssets");
                return manifest;
            }
            // 2. 从网络下载 Manifest（如果启用远程加载）
            if (_enableRemote)
            {
                manifest = await DownloadManifestFromNetwork();
                if (manifest != null)
                {
                    Game.Debug.Debug($"[{_packageName}] Manifest downloaded from network");
                    return manifest;
                }
            }

            Game.Debug.Error($"[{_packageName}] Failed to load manifest from any source");
            return null;
        }

        /// <summary>
        /// 尝试从 StreamingAssets 加载 Manifest
        /// </summary>
        private async UniTask<PackageManifest> TryLoadManifestFromStreamingAssets()
        {
            // 使用VersionManager获取StreamingAssets中的清单路径
            var manifestPath = _versionManager.GetStreamingAssetsManifestPath(_packageName);
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
                        return JsonConvert.DeserializeObject<PackageManifest>(json);
                    }
                }
            }
            finally
            {
            }
            return null;
        }

        /// <summary>
        /// 从网络下载 Manifest
        /// </summary>
        private async UniTask<PackageManifest> DownloadManifestFromNetwork()
        {
            try
            {
                // 使用VersionManager获取清单下载URL
                var manifestUrl = _versionManager.GetPackageManifestUrl(_packageName);
                Game.Debug.Debug($"[{_packageName}] Downloading manifest from: {manifestUrl}");

                var result = await Game.Web.GetAsync<PackageManifest>(manifestUrl);

                return result.IsSuccess ? result.Data : null;
            }
            catch (System.Exception ex)
            {
                Game.Debug.Error($"[{_packageName}] Download manifest exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 准备资源（检查 VFS、StreamingAssets，必要时下载）
        /// </summary>
        public async UniTask<bool> PrepareResourcesAsync(PackageManifest manifest)
        {
            if (manifest == null || manifest.bundles == null || manifest.bundles.Length == 0)
            {
                Game.Debug.Warning($"[{_packageName}] Manifest is empty, no bundles to prepare");
                return true;
            }

            try
            {
                var bundlesToDownload = new List<BundleManifest>();
                var corruptedBundles = new List<BundleManifest>();

                foreach (var bundle in manifest.bundles)
                {
                    // 检查VFS中是否存在
                    if (Game.File.Exists(bundle.name, bundle.version))
                    {
                        // 传入bundle.hash进行完整性校验
                        if (!string.IsNullOrEmpty(bundle.hash))
                        {
                            if (!await Game.File.VerifyHashAsync(bundle.name, bundle.version, bundle.hash))
                            {
                                Game.Debug.Warning($"[{_packageName}] Bundle corrupted (hash mismatch), will re-download: {bundle.name}");
                                Game.File.Delete(bundle.name, bundle.version);
                                corruptedBundles.Add(bundle);
                                continue;
                            }
                        }

                        Game.Debug.Info($"[{_packageName}] Bundle verified in VFS: {bundle.name}");
                        continue;
                    }

                    // 检查StreamingAssets
                    if (_enableRemote && await ExistsInStreamingAssets(bundle.name))
                    {
                        Game.Debug.Info($"[{_packageName}] Bundle found in StreamingAssets: {bundle.name}");

                        // 复制到VFS
                        await CopyBundleFromStreamingAssetsToVFS(bundle.name, bundle.version);

                        // 复制后立即校验
                        if (!string.IsNullOrEmpty(bundle.hash))
                        {
                            if (!await Game.File.VerifyHashAsync(bundle.name, bundle.version, bundle.hash))
                            {
                                Game.Debug.Warning($"[{_packageName}] StreamingAssets bundle corrupted, will download: {bundle.name}");
                                Game.File.Delete(bundle.name, bundle.version);
                                bundlesToDownload.Add(bundle);
                                continue;
                            }
                        }

                        continue;
                    }

                    // 需要下载
                    bundlesToDownload.Add(bundle);
                }

                if (bundlesToDownload.Count == 0 && corruptedBundles.Count == 0)
                {
                    Game.Debug.Info($"[{_packageName}] All bundles are ready and verified");
                    return true;
                }

                var totalToDownload = bundlesToDownload.Count + corruptedBundles.Count;
                Game.Debug.Info($"[{_packageName}] Need to download {totalToDownload} bundles " +
                                  $"({bundlesToDownload.Count} new + {corruptedBundles.Count} corrupted)");

                // 使用DownloadCollection批量下载
                var allBundles = new List<BundleManifest>();
                allBundles.AddRange(bundlesToDownload);
                allBundles.AddRange(corruptedBundles);
                await DownloadBundlesWithCollectionAsync(allBundles);

                Game.Debug.Info($"[{_packageName}] All bundles prepared and verified successfully");
                return true;
            }
            catch (System.Exception ex)
            {
                Game.Debug.Error($"[{_packageName}] Prepare resources exception", ex);
                return false;
            }
        }



        /// <summary>
        /// 检查 StreamingAssets 中是否存在 Bundle
        /// Bundle 文件直接放在 StreamingAssets 根目录下
        /// </summary>
        private async UniTask<bool> ExistsInStreamingAssets(string bundleName)
        {
            var bundlePath = _versionManager.GetStreamingAssetsBundlePath(_packageName, bundleName);

            using (var request = UnityWebRequest.Get(bundlePath))
            {
                await request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                return request.result == UnityWebRequest.Result.Success;
#else
                return !request.isHttpError && !request.isNetworkError;
#endif
            }
        }

        /// <summary>
        /// 从 StreamingAssets 复制 Bundle 到 VFS
        /// Bundle 文件直接放在 StreamingAssets 根目录下
        /// </summary>
        private async UniTask CopyBundleFromStreamingAssetsToVFS(string bundleName, string version)
        {
            var bundlePath = _versionManager.GetStreamingAssetsBundlePath(_packageName, bundleName);

            using (var request = UnityWebRequest.Get(bundlePath))
            {
                await request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isHttpError || request.isNetworkError)
#endif
                {
                    throw new System.Exception($"Failed to copy bundle from StreamingAssets: {bundleName}");
                }
                using var handle = await Game.File.WriteHandleAsync(bundleName, version, request.downloadHandler.data);
                Game.Debug.Info($"[{_packageName}] Bundle copied to VFS: {bundleName}");
            }
        }

        /// <summary>
        /// 使用DownloadCollection批量下载Bundle（支持进度报告）
        /// </summary>
        private async UniTask DownloadBundlesWithCollectionAsync(List<BundleManifest> bundles)
        {
            if (!_enableRemote)
            {
                throw new System.InvalidOperationException($"[{_packageName}] Cannot download bundles: remote loading is disabled (Offline mode?)");
            }

            // 创建DownloadCollection
            var collection = Game.Download.CreateCollection();

            // 创建Bundle映射（URL -> BundleManifest）
            var bundleMap = new Dictionary<string, BundleManifest>();

            // 添加所有下载任务
            foreach (var bundle in bundles)
            {
                // 使用VersionManager获取Bundle下载URL
                var url = _versionManager.GetBundleDownloadUrl(_packageName, bundle.name);
                bundleMap[url] = bundle;
                collection.Add(url, bundle.version);
            }

            // 监听总体进度
            collection.ProgressChanged += progress =>
            {
                // Game.Logger.Debug($"[{_packageName}] Download progress: {progress * 100:F1}% " +
                //                   $"({collection.CompletedCount}/{collection.TotalCount} files, " +
                //                   $"Speed: {collection.TotalCurrentSpeed / 1024.0:F1} KB/s)");
            };

            // 监听单个文件完成
            collection.ItemCompleted += handle =>
            {
                ProcessDownloadedBundleAsync(handle, bundleMap).SafeForget($"BundlePackageLoader.ProcessDownload:{handle.Url}");
            };

            // 等待所有下载完成
            var success = await collection.WaitForCompletionAsync();

            if (!success)
            {
                var failedHandles = collection.FailedHandles;
                var failedNames = string.Join(", ", failedHandles.Select(h => System.IO.Path.GetFileName(h.Url)));
                throw new System.InvalidOperationException($"[{_packageName}] Failed to download {failedHandles.Count} bundles: {failedNames}");
            }

            collection.Dispose();
        }

        /// <summary>
        /// 处理下载完成的Bundle（添加到VFS并校验）
        /// </summary>
        private async UniTask ProcessDownloadedBundleAsync(DownloadHandle handle, Dictionary<string, BundleManifest> bundleMap)
        {
            if (!bundleMap.TryGetValue(handle.Url, out var bundle))
                return;

            try
            {
                // 添加到VFS（不需要传hash）
                await Game.File.Add(
                    handle.SavedFilePath,
                    bundle.name,
                    bundle.version
                );

                Game.Debug.Info($"[{_packageName}] Bundle downloaded and added to VFS: {bundle.name}");

                // 添加后立即校验（传入remoteHash）
                if (!string.IsNullOrEmpty(bundle.hash))
                {
                    if (!await Game.File.VerifyHashAsync(bundle.name, bundle.version, bundle.hash))
                    {
                        Game.Debug.Error($"[{_packageName}] Downloaded bundle verification failed: {bundle.name}");
                        Game.File.Delete(bundle.name, bundle.version);
                        throw new System.InvalidOperationException($"Bundle verification failed: {bundle.name}");
                    }

                    Game.Debug.Info($"[{_packageName}] Bundle hash verified: {bundle.name}");
                }
            }
            catch (System.Exception ex)
            {
                Game.Debug.Error($"[{_packageName}] Failed to process downloaded bundle: {bundle.name}", ex);
                throw;
            }
        }
    }
}
