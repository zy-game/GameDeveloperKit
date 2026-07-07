using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// 网络散资源提供者，用于加载 http 前缀的散资源（如 png、txt），不进入 package/manifest 体系。
    /// </summary>
    public sealed class NetworkAssetProvider
    {
        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg" };

        private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();

        /// <summary>
        /// 判断资源地址是否为网络散资源（http/https 前缀）。
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <returns>如果是网络散资源，则返回true；否则返回false。</returns>
        public static bool IsNetworkLocation(string location)
        {
            return string.IsNullOrWhiteSpace(location) is false
                && (location.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || location.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 异步加载网络资源，图片地址返回 Texture2D，其余按二进制包装。
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <returns>资源加载句柄。</returns>
        public async UniTask<AssetHandle> LoadAssetAsync(string location)
        {
            if (IsNetworkLocation(location) is false)
            {
                return AssetHandle.Failure(new ArgumentException("Network asset location must start with http.", nameof(location)));
            }

            var info = new AssetInfo { Location = location };
            try
            {
                if (IsImageLocation(location))
                {
                    var texture = await DownloadTextureAsync(location);
                    _createdObjects.Add(texture);
                    return AssetHandle.Success(info, texture);
                }

                var bytes = await DownloadBytesAsync(location);
                var textAsset = new TextAsset(System.Text.Encoding.UTF8.GetString(bytes));
                _createdObjects.Add(textAsset);
                return AssetHandle.Success(info, textAsset);
            }
            catch (Exception exception)
            {
                return AssetHandle.Failure(exception);
            }
        }

        /// <summary>
        /// 异步加载网络二进制资源。
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <returns>二进制资源句柄。</returns>
        public async UniTask<RawAssetHandle> LoadRawAssetAsync(string location)
        {
            if (IsNetworkLocation(location) is false)
            {
                return RawAssetHandle.Failure(new ArgumentException("Network asset location must start with http.", nameof(location)));
            }

            try
            {
                var bytes = await DownloadBytesAsync(location);
                return RawAssetHandle.Success(new AssetInfo { Location = location }, bytes);
            }
            catch (Exception exception)
            {
                return RawAssetHandle.Failure(exception);
            }
        }

        /// <summary>
        /// 释放已加载的网络资源对象。
        /// </summary>
        public void Release()
        {
            foreach (var target in _createdObjects)
            {
                if (target != null)
                {
                    UnityEngine.Object.Destroy(target);
                }
            }

            _createdObjects.Clear();
        }

        private static bool IsImageLocation(string location)
        {
            foreach (var extension in ImageExtensions)
            {
                if (location.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static async UniTask<Texture2D> DownloadTextureAsync(string location)
        {
            using (var request = UnityWebRequestTexture.GetTexture(location))
            {
                await request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new GameException(request.error ?? $"Network asset load failed: {location}");
                }

                return DownloadHandlerTexture.GetContent(request);
            }
        }

        private static async UniTask<byte[]> DownloadBytesAsync(string location)
        {
            using (var request = UnityWebRequest.Get(location))
            {
                await request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new GameException(request.error ?? $"Network asset load failed: {location}");
                }

                return request.downloadHandler.data ?? Array.Empty<byte>();
            }
        }
    }
}
