using System;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameDeveloperKit.File
{
    /// <summary>
    /// Exposes browser-owned file downloads to WebGL callers.
    /// </summary>
    public static class WebGLBrowserFile
    {
        /// <summary>
        /// Maximum size accepted by <see cref="Download(byte[],string,string)"/>.
        /// Larger files must use <see cref="DownloadUrl(string,string)"/>.
        /// </summary>
        public const int MaxBufferedDownloadBytes = 16 * 1024 * 1024;

        public static bool IsSupported
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR && !WEIXINMINIGAME && !UNITY_WECHATMINIGAME && !DOUYINMINIGAME
                return true;
#else
                return false;
#endif
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR && !WEIXINMINIGAME && !UNITY_WECHATMINIGAME && !DOUYINMINIGAME
        [DllImport("__Internal")]
        private static extern void GDK_WebGLDownloadFile(
            byte[] data,
            int length,
            string fileName,
            string mimeType);

        [DllImport("__Internal")]
        private static extern void GDK_WebGLDownloadUrl(string url, string fileName);
#endif

        /// <summary>
        /// Downloads a small byte array through a browser Blob.
        /// </summary>
        public static void Download(byte[] data, string fileName, string mimeType)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Download data cannot be empty.", nameof(data));
            }

            if (data.Length > MaxBufferedDownloadBytes)
            {
                throw new ArgumentException(
                    $"Buffered browser downloads cannot exceed {MaxBufferedDownloadBytes} bytes. " +
                    $"Use {nameof(DownloadUrl)} for large files.",
                    nameof(data));
            }

            ValidateFileName(fileName);
            if (string.IsNullOrWhiteSpace(mimeType))
            {
                throw new ArgumentException("Download MIME type cannot be empty.", nameof(mimeType));
            }

#if UNITY_WEBGL && !UNITY_EDITOR && !WEIXINMINIGAME && !UNITY_WECHATMINIGAME && !DOUYINMINIGAME
            GDK_WebGLDownloadFile(data, data.Length, fileName, mimeType);
#else
            throw new PlatformNotSupportedException(
                "Browser file downloads are only available in a standard WebGL browser player. " +
                "Mini-game containers require their platform file or share API.");
#endif
        }

        /// <summary>
        /// Lets the browser download a URL directly without copying its payload into Unity memory.
        /// The call must originate from a user interaction, and cross-origin servers should provide
        /// a Content-Disposition attachment header when a forced download is required.
        /// </summary>
        public static void DownloadUrl(string url, string fileName)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException(
                    "Browser download URL must be an absolute HTTP or HTTPS URL.",
                    nameof(url));
            }

            ValidateFileName(fileName);
#if UNITY_WEBGL && !UNITY_EDITOR && !WEIXINMINIGAME && !UNITY_WECHATMINIGAME && !DOUYINMINIGAME
            GDK_WebGLDownloadUrl(uri.AbsoluteUri, fileName);
#else
            throw new PlatformNotSupportedException(
                "Browser URL downloads are only available in a standard WebGL browser player. " +
                "Mini-game containers require their platform file or share API.");
#endif
        }

        private static void ValidateFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName.IndexOf('/') >= 0 ||
                fileName.IndexOf('\\') >= 0)
            {
                throw new ArgumentException(
                    "Download file name must be a single path segment.",
                    nameof(fileName));
            }
        }
    }

    /// <summary>
    /// File operations owned by WeChat and Douyin mini-game containers.
    /// </summary>
    public static class WebGLMiniGameFile
    {
        public const int MaxImageBytes = 16 * 1024 * 1024;
        private const float OperationTimeoutSeconds = 30f;

        public static bool IsSupported
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR && (WEIXINMINIGAME || UNITY_WECHATMINIGAME || DOUYINMINIGAME)
                return true;
#else
                return false;
#endif
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR && (WEIXINMINIGAME || UNITY_WECHATMINIGAME || DOUYINMINIGAME)
        [DllImport("__Internal")]
        private static extern int GDK_WebGLBeginMiniGameSaveImage(
            byte[] data,
            int length,
            string fileName);

        [DllImport("__Internal")]
        private static extern int GDK_WebGLPollMiniGameFileOperation(int requestId);
#endif

        /// <summary>
        /// Writes an image to the platform user-data directory and asks the container to save it
        /// to the user's photo album. The temporary file is removed after the platform callback.
        /// </summary>
        public static async UniTask SaveImageToPhotosAlbumAsync(byte[] data, string fileName)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Image data cannot be empty.", nameof(data));
            }

            if (data.Length > MaxImageBytes)
            {
                throw new ArgumentException(
                    $"Mini-game album images cannot exceed {MaxImageBytes} bytes.",
                    nameof(data));
            }

            ValidateFileName(fileName);
#if UNITY_WEBGL && !UNITY_EDITOR && (WEIXINMINIGAME || UNITY_WECHATMINIGAME || DOUYINMINIGAME)
            var requestId = GDK_WebGLBeginMiniGameSaveImage(data, data.Length, fileName);
            if (requestId <= 0)
            {
                throw new InvalidOperationException(
                    "The mini-game file or photo-album API is unavailable.");
            }

            var startedAt = Time.realtimeSinceStartup;
            while (true)
            {
                var status = GDK_WebGLPollMiniGameFileOperation(requestId);
                if (status == 1)
                {
                    return;
                }

                if (status < 0)
                {
                    throw new InvalidOperationException(
                        "The mini-game container failed to save the image to the photo album. " +
                        "Check platform permission and console diagnostics.");
                }

                if (Time.realtimeSinceStartup - startedAt >= OperationTimeoutSeconds)
                {
                    throw new TimeoutException(
                        $"Mini-game photo-album save exceeded {OperationTimeoutSeconds} seconds.");
                }

                await UniTask.Yield();
            }
#else
            await UniTask.CompletedTask;
            throw new PlatformNotSupportedException(
                "Mini-game photo-album save is only available in WeChat or Douyin mini-game builds.");
#endif
        }

        private static void ValidateFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName.IndexOf('/') >= 0 ||
                fileName.IndexOf('\\') >= 0)
            {
                throw new ArgumentException(
                    "File name must be a single path segment.",
                    nameof(fileName));
            }
        }
    }
}
