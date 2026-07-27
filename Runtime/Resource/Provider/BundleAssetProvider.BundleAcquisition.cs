using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Operation;
using UnityEngine;
using UnityEngine.Networking;

namespace GameDeveloperKit.Resource
{
    public sealed partial class BundleAssetProvider
    {
        internal const string BundleCachePrefix = "resource/bundles/";

        internal static string CreateBundleCacheKey(BundleInfo bundleInfo)
        {
            var bundleName = ProviderBase.ResolveBundleFileName(bundleInfo);
            return BundleCachePrefix + bundleName;
        }

        internal static string CreateBundleCacheVersion(string manifestVersion, BundleInfo bundleInfo)
        {
            if (string.IsNullOrWhiteSpace(manifestVersion))
            {
                throw new ArgumentException("Manifest version cannot be empty.", nameof(manifestVersion));
            }

            if (bundleInfo == null)
            {
                throw new ArgumentNullException(nameof(bundleInfo));
            }

            var hash = bundleInfo.Hash?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(hash))
            {
                throw new ArgumentException("Bundle hash cannot be empty.", nameof(bundleInfo));
            }

            return manifestVersion.Length.ToString(CultureInfo.InvariantCulture) + ":" +
                   manifestVersion +
                   hash.Length.ToString(CultureInfo.InvariantCulture) + ":" +
                   hash;
        }

        private static UniTask<BundleLoadResult> AcquireBundleAsync(
            BundleInfo bundleInfo,
            ResourceMode mode,
            string manifestVersion,
            bool isRemote)
        {
            switch (mode)
            {
                case ResourceMode.Offline:
                    if (isRemote)
                    {
                        throw new GameException($"Offline bundle cannot have a remote source: {bundleInfo.Name}");
                    }

                    return LoadPackagedBundleAsync(bundleInfo);
                case ResourceMode.Online:
                    return isRemote
                        ? LoadOnlineRemoteBundleAsync(bundleInfo, manifestVersion)
                        : LoadPackagedBundleAsync(bundleInfo);
                case ResourceMode.Web:
                    return isRemote
                        ? LoadWebRemoteBundleAsync(bundleInfo, manifestVersion)
                        : LoadPackagedBundleAsync(bundleInfo);
                default:
                    throw new GameException($"Unsupported bundle resource mode: {mode}");
            }
        }

        private static async UniTask<BundleLoadResult> LoadPackagedBundleAsync(BundleInfo bundleInfo)
        {
            var bundleName = ProviderBase.ResolveBundleFileName(bundleInfo);
            var stream = await App.File.OpenPackagedReadAsync(bundleName);
            if (stream == null)
            {
                throw new FileNotFoundException($"Packaged AssetBundle was not found: {bundleName}", bundleName);
            }

            try
            {
                var bundle = await LoadBundleFromStreamAsync(stream, bundleInfo, "Packaged");
                return new BundleLoadResult(bundle, stream);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        private static async UniTask<BundleLoadResult> LoadOnlineRemoteBundleAsync(
            BundleInfo bundleInfo,
            string manifestVersion)
        {
            var cacheKey = CreateBundleCacheKey(bundleInfo);
            var cacheVersion = CreateBundleCacheVersion(manifestVersion, bundleInfo);
            Stream cachedStream = null;
            try
            {
                cachedStream = await App.File.OpenReadAsync(cacheKey, cacheVersion);
                if (cachedStream != null)
                {
                    var cachedBundle = await LoadVerifiedOnlineStreamAsync(cachedStream, bundleInfo, "VFS hit");
                    cachedStream = null;
                    App.Debug.Info($"AssetBundle VFS hit. Name: {bundleInfo.Name}, Key: {cacheKey}");
                    return cachedBundle;
                }
            }
            catch (Exception exception)
            {
                App.Debug.Warning(
                    $"AssetBundle VFS entry is corrupt. Name: {bundleInfo.Name}, Key: {cacheKey}, Error: {exception.Message}");
                await App.File.DeleteAsync(cacheKey);
            }
            finally
            {
                cachedStream?.Dispose();
            }

            var settings = App.Resource.Settings ?? throw new GameException("Resource settings are unavailable.");
            var uri = App.Resource.GetAssetAddress(settings, bundleInfo.Name, manifestVersion);
            var download = App.Download.DownloadAsync(uri);
            try
            {
                await download.WaitCompletionAsync();
                if (download.Status is not OperationStatus.Succeeded)
                {
                    throw download.Error ?? new GameException($"AssetBundle download failed: {uri}");
                }

                await download.SaveVerifiedAsync(
                    cacheKey,
                    cacheVersion,
                    stream => ValidateBundleIdentityAsync(stream, bundleInfo, "VFS candidate"));

                App.Debug.Info($"AssetBundle VFS miss committed. Name: {bundleInfo.Name}, Key: {cacheKey}");
            }
            catch (Exception operationException)
            {
                try
                {
                    await App.Download.ReleaseAsync(download);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(
                        $"AssetBundle acquisition failed and its download result could not be released: {bundleInfo.Name}",
                        operationException,
                        cleanupException);
                }

                throw;
            }

            try
            {
                await App.Download.ReleaseAsync(download);
            }
            catch
            {
                throw;
            }

            Stream committedStream = null;
            try
            {
                committedStream = await App.File.OpenReadAsync(cacheKey, cacheVersion);
                if (committedStream == null)
                {
                    throw new FileNotFoundException(
                        $"Committed AssetBundle cache entry was not found: {cacheKey}",
                        cacheKey);
                }

                var committedBundle = await LoadVerifiedOnlineStreamAsync(
                    committedStream,
                    bundleInfo,
                    "VFS committed");
                committedStream = null;
                return committedBundle;
            }
            catch
            {
                committedStream?.Dispose();
                await App.File.DeleteAsync(cacheKey);
                throw;
            }
        }

        private static async UniTask<BundleLoadResult> LoadWebRemoteBundleAsync(
            BundleInfo bundleInfo,
            string manifestVersion)
        {
            if (bundleInfo.Crc == 0)
            {
                throw new GameException($"Web remote AssetBundle requires a non-zero CRC: {bundleInfo.Name}");
            }

            var settings = App.Resource.Settings ?? throw new GameException("Resource settings are unavailable.");
            var uri = App.Resource.GetAssetAddress(settings, bundleInfo.Name, manifestVersion);
            using (var request = UnityWebRequestAssetBundle.GetAssetBundle(uri, bundleInfo.Crc))
            {
                await request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new GameException(request.error ?? $"AssetBundle web request failed: {uri}");
                }

                if (bundleInfo.Size > 0 && request.downloadedBytes != (ulong)bundleInfo.Size)
                {
                    throw new InvalidDataException(
                        $"AssetBundle size mismatch. Name: {bundleInfo.Name}, Expected: {bundleInfo.Size}, Actual: {request.downloadedBytes}");
                }

                var bundle = DownloadHandlerAssetBundle.GetContent(request) ??
                             throw new GameException($"AssetBundle web request returned no bundle: {uri}");
                return new BundleLoadResult(bundle, null);
            }
        }

        private static async UniTask<BundleLoadResult> LoadVerifiedOnlineStreamAsync(
            Stream stream,
            BundleInfo bundleInfo,
            string source)
        {
            await ValidateBundleIdentityAsync(stream, bundleInfo, source);
            stream.Position = 0;
            var bundle = await LoadBundleFromStreamAsync(stream, bundleInfo, source);
            return new BundleLoadResult(bundle, stream);
        }

        internal static async UniTask ValidateBundleIdentityAsync(
            Stream stream,
            BundleInfo bundleInfo,
            string source)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new ArgumentException("AssetBundle verification requires a readable, seekable stream.", nameof(stream));
            }

            if (stream.Length != bundleInfo.Size)
            {
                throw new InvalidDataException(
                    $"AssetBundle size mismatch. Name: {bundleInfo.Name}, Source: {source}, Expected: {bundleInfo.Size}, Actual: {stream.Length}");
            }

            stream.Position = 0;
            var actualHash = await ComputeSha1Async(stream);
            if (string.Equals(actualHash, bundleInfo.Hash, StringComparison.OrdinalIgnoreCase) is false)
            {
                throw new InvalidDataException(
                    $"AssetBundle SHA-1 mismatch. Name: {bundleInfo.Name}, Source: {source}, Expected: {bundleInfo.Hash}, Actual: {actualHash}");
            }
        }

        private static async UniTask<string> ComputeSha1Async(Stream stream)
        {
            using (var sha1 = SHA1.Create())
            {
                var buffer = new byte[81920];
                while (true)
                {
                    var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (read == 0)
                    {
                        break;
                    }

                    sha1.TransformBlock(buffer, 0, read, buffer, 0);
                }

                sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(sha1.Hash ?? Array.Empty<byte>())
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private static async UniTask<AssetBundle> LoadBundleFromStreamAsync(
            Stream stream,
            BundleInfo bundleInfo,
            string source)
        {
            if (stream == null || !stream.CanRead || !stream.CanSeek || stream.Length == 0)
            {
                throw new InvalidDataException($"AssetBundle stream is invalid. Name: {bundleInfo.Name}, Source: {source}");
            }

            stream.Position = 0;
            var bundle = await AssetBundle.LoadFromStreamAsync(stream, bundleInfo.Crc);
            return bundle ?? throw new GameException(
                $"Unity AssetBundle load failed. Name: {bundleInfo.Name}, Source: {source}, CRC: {bundleInfo.Crc}");
        }

        private sealed class BundleLoadResult
        {
            internal BundleLoadResult(AssetBundle bundle, Stream loadSource)
            {
                Bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
                LoadSource = loadSource;
            }

            internal AssetBundle Bundle { get; }

            internal Stream LoadSource { get; }
        }
    }
}
