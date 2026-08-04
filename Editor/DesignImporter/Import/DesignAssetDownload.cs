using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.IO;
using IOFile = System.IO.File;
using System.Threading;
using System.Threading.Tasks;

namespace GameDeveloperKit.DesignImporter
{
    internal sealed class DesignAssetDownloadClient : IDisposable
    {
        private const int MaximumAssetBytes = 64 * 1024 * 1024;
        private readonly HttpClient m_Client;

        public DesignAssetDownloadClient()
        {
            m_Client = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            })
            {
                Timeout = TimeSpan.FromSeconds(100)
            };
            m_Client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "image/png,image/jpeg,image/*;q=0.8");
            m_Client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) GameDeveloperKit-DesignImporter/1.0");
            m_Client.DefaultRequestHeaders.Referrer = new Uri("https://lanhuapp.com/");
        }

        public async Task<DownloadedDesignAsset> DownloadAsync(
            DesignAsset asset,
            CancellationToken cancellationToken)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            using var response = await m_Client.GetAsync(
                asset.Url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"下载切图 {asset.Name} 失败：HTTP {(int)response.StatusCode}");
            }

            var length = response.Content.Headers.ContentLength;
            if (length > MaximumAssetBytes)
            {
                throw new InvalidOperationException($"切图 {asset.Name} 超过 64 MB 限制。");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            cancellationToken.ThrowIfCancellationRequested();
            if (bytes.Length == 0)
            {
                throw new InvalidOperationException($"切图 {asset.Name} 内容为空。");
            }

            if (bytes.Length > MaximumAssetBytes)
            {
                throw new InvalidOperationException($"切图 {asset.Name} 超过 64 MB 限制。");
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            var extension = DetectExtension(bytes, mediaType, asset.Format);
            var hash = ComputeHash(bytes);
            return new DownloadedDesignAsset(asset, bytes, extension, hash);
        }

        public static DownloadedDesignAsset ReadCached(DesignAsset asset)
        {
            if (asset == null || string.IsNullOrWhiteSpace(asset.CachedFilePath) || !IOFile.Exists(asset.CachedFilePath))
            {
                return null;
            }

            var bytes = IOFile.ReadAllBytes(asset.CachedFilePath);
            var extension = Path.GetExtension(asset.CachedFilePath).TrimStart('.').ToLowerInvariant();
            return new DownloadedDesignAsset(
                asset,
                bytes,
                string.IsNullOrWhiteSpace(extension) ? "png" : extension,
                string.IsNullOrWhiteSpace(asset.CachedHash) ? ComputeHash(bytes) : asset.CachedHash);
        }

        public void Dispose()
        {
            m_Client.Dispose();
        }

        private static string DetectExtension(byte[] bytes, string mediaType, string fallback)
        {
            if (bytes.Length >= 8 &&
                bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            {
                return "png";
            }

            if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            {
                return "jpg";
            }

            if (bytes.Length >= 12 &&
                bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
                bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
            {
                throw new InvalidOperationException("Unity 不能直接导入 WebP 切图，请在设计来源中导出 PNG。" );
            }

            if (string.Equals(mediaType, "image/png", StringComparison.OrdinalIgnoreCase))
            {
                return "png";
            }

            if (string.Equals(mediaType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return "jpg";
            }

            return DesignPathUtility.NormalizeImageExtension(fallback);
        }

        private static string ComputeHash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    internal sealed class DownloadedDesignAsset
    {
        public DownloadedDesignAsset(DesignAsset source, byte[] bytes, string extension, string hash)
        {
            Source = source;
            Bytes = bytes;
            Extension = extension;
            Hash = hash;
        }

        public DesignAsset Source { get; }
        public byte[] Bytes { get; }
        public string Extension { get; }
        public string Hash { get; }
    }
}
