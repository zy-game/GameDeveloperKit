using System;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GameDeveloperKit.File
{
    /// <summary>
    /// Reads small control files from StreamingAssets through the WebGL browser transport.
    /// </summary>
    internal static class WebGLStreamingAssets
    {
        internal const int MaxBufferedBytes = 16 * 1024 * 1024;

        internal static async UniTask<Stream> OpenReadAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("StreamingAssets address cannot be empty.", nameof(address));
            }

            var requestAddress = CreateBuildVersionedAddress(address, GetRuntimeBuildVersion());
            using (var request = UnityWebRequest.Get(requestAddress))
            {
                var handler = new LimitedMemoryDownloadHandler(MaxBufferedBytes);
                request.downloadHandler = handler;
                await request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    return handler.DetachStream();
                }

                if (request.responseCode == 404)
                {
                    return null;
                }

                if (handler.LimitExceeded)
                {
                    throw new GameException(
                        $"WebGL StreamingAssets buffered read exceeded {MaxBufferedBytes} bytes. " +
                        $"Address='{address}'. AssetBundles must use the Web asset-bundle loader, " +
                        "and large media must be streamed from a URL.");
                }

                throw new GameException(
                    $"WebGL StreamingAssets read failed. Address='{address}', " +
                    $"HttpStatus='{request.responseCode}', Error='{request.error ?? "<none>"}'.");
            }
        }

        internal static string CreateBuildVersionedAddress(string address, string buildGuid)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("StreamingAssets address cannot be empty.", nameof(address));
            }

            if (string.IsNullOrWhiteSpace(buildGuid))
            {
                throw new ArgumentException("WebGL build GUID cannot be empty.", nameof(buildGuid));
            }

            var fragmentIndex = address.IndexOf('#');
            var fragment = fragmentIndex >= 0 ? address.Substring(fragmentIndex) : string.Empty;
            var resourceAddress = fragmentIndex >= 0 ? address.Substring(0, fragmentIndex) : address;
            var separator = resourceAddress.IndexOf('?') >= 0 ? "&" : "?";
            return resourceAddress + separator + "gdk-build=" + Uri.EscapeDataString(buildGuid) + fragment;
        }

        private static string GetRuntimeBuildVersion()
        {
            if (string.IsNullOrWhiteSpace(Application.buildGUID) is false)
            {
                return Application.buildGUID;
            }

            return string.IsNullOrWhiteSpace(Application.version)
                ? "editor"
                : "editor-" + Application.version;
        }

        private sealed class LimitedMemoryDownloadHandler : DownloadHandlerScript
        {
            private MemoryStream m_Stream = new MemoryStream();
            private readonly int m_MaxBytes;

            internal LimitedMemoryDownloadHandler(int maxBytes)
                : base(new byte[64 * 1024])
            {
                m_MaxBytes = maxBytes;
            }

            internal bool LimitExceeded { get; private set; }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength <= 0)
                {
                    return true;
                }

                if (m_Stream.Length + dataLength > m_MaxBytes)
                {
                    LimitExceeded = true;
                    return false;
                }

                m_Stream.Write(data, 0, dataLength);
                return true;
            }

            internal Stream DetachStream()
            {
                var stream = m_Stream ?? throw new ObjectDisposedException(nameof(LimitedMemoryDownloadHandler));
                stream.Position = 0;
                m_Stream = null;
                return stream;
            }
        }
    }
}
