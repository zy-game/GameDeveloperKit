using System;
using UnityEngine;
using UnityEngine.Networking;

namespace GameDeveloperKit.Resource
{
    /// <summary>
    /// Arguments used by a WebGL AssetBundle platform strategy.
    /// </summary>
    public readonly struct WebAssetBundleRequestOptions
    {
        internal WebAssetBundleRequestOptions(
            string url,
            bool disableUnityWebCache,
            string contentHash,
            uint crc)
        {
            Url = url;
            DisableUnityWebCache = disableUnityWebCache;
            ContentHash = contentHash;
            Crc = crc;
        }

        public string Url { get; }

        public bool DisableUnityWebCache { get; }

        public string ContentHash { get; }

        public uint Crc { get; }
    }

    /// <summary>
    /// Adapts AssetBundle request, extraction, and unload behavior for WebGL hosts.
    /// WeChat and Douyin integrations can register their SDK-backed implementation.
    /// </summary>
    public interface IWebAssetBundleStrategy
    {
        UnityWebRequest CreateAssetBundleRequest(WebAssetBundleRequestOptions options);

        AssetBundle ExtractAssetBundle(UnityWebRequest request);

        void UnloadAssetBundle(AssetBundle assetBundle, bool unloadAllLoadedObjects);
    }

    /// <summary>
    /// Selects the AssetBundle strategy used by WebGL resource providers.
    /// </summary>
    public static class WebAssetBundlePlatform
    {
        private static readonly IWebAssetBundleStrategy s_DefaultStrategy =
            CreateDefaultStrategy();
        private static IWebAssetBundleStrategy s_Strategy = s_DefaultStrategy;

        public static IWebAssetBundleStrategy Strategy
        {
            get => s_Strategy;
            set => s_Strategy = value ?? throw new ArgumentNullException(nameof(value));
        }

        public static void ResetToDefault()
        {
            s_Strategy = s_DefaultStrategy;
        }

        internal static string CreateContentVersionedUrl(string url, string contentHash)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("AssetBundle URL cannot be empty.", nameof(url));
            }

            if (string.IsNullOrWhiteSpace(contentHash))
            {
                throw new ArgumentException("AssetBundle content hash cannot be empty.", nameof(contentHash));
            }

            var fragmentIndex = url.IndexOf('#');
            var fragment = fragmentIndex >= 0 ? url.Substring(fragmentIndex) : string.Empty;
            var address = fragmentIndex >= 0 ? url.Substring(0, fragmentIndex) : url;
            var separator = address.IndexOf('?') >= 0 ? "&" : "?";
            return address + separator + "gdk-content=" + Uri.EscapeDataString(contentHash) + fragment;
        }

        private static IWebAssetBundleStrategy CreateDefaultStrategy()
        {
#if UNITY_WEBGL && (WEIXINMINIGAME || UNITY_WECHATMINIGAME)
            return new WechatWebAssetBundleStrategy();
#elif UNITY_WEBGL && DOUYINMINIGAME
            return new TiktokWebAssetBundleStrategy();
#else
            return new DefaultWebAssetBundleStrategy();
#endif
        }

        private sealed class DefaultWebAssetBundleStrategy : IWebAssetBundleStrategy
        {
            public UnityWebRequest CreateAssetBundleRequest(WebAssetBundleRequestOptions options)
            {
                var requestUrl = CreateContentVersionedUrl(options.Url, options.ContentHash);
                var request = new UnityWebRequest(requestUrl, UnityWebRequest.kHttpVerbGET);
                request.downloadHandler = CreateDownloadHandler(options, requestUrl);
                request.disposeDownloadHandlerOnDispose = true;
                return request;
            }

            public AssetBundle ExtractAssetBundle(UnityWebRequest request)
            {
                if (request == null)
                {
                    throw new ArgumentNullException(nameof(request));
                }

                var handler = request.downloadHandler as DownloadHandlerAssetBundle;
                if (handler == null)
                {
                    throw new GameException(
                        $"Web AssetBundle request has an unexpected download handler: " +
                        $"'{request.downloadHandler?.GetType().FullName ?? "<null>"}'.");
                }

                return handler.assetBundle;
            }

            public void UnloadAssetBundle(AssetBundle assetBundle, bool unloadAllLoadedObjects)
            {
                assetBundle?.Unload(unloadAllLoadedObjects);
            }

            private static DownloadHandlerAssetBundle CreateDownloadHandler(
                WebAssetBundleRequestOptions options,
                string requestUrl)
            {
                if (options.DisableUnityWebCache)
                {
                    return new DownloadHandlerAssetBundle(requestUrl, options.Crc);
                }

                // The manifest stores SHA-1 rather than Unity's Hash128. Hashing the SHA-1 text
                // still provides a stable cache version that changes with the bundle content.
                var cacheVersion = Hash128.Compute(options.ContentHash);
                return new DownloadHandlerAssetBundle(requestUrl, cacheVersion, options.Crc);
            }
        }
    }
}
