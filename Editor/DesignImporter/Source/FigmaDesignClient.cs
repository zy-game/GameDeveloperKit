using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace GameDeveloperKit.DesignImporter
{
    internal sealed class FigmaDesignClient : IDisposable
    {
        private const int NodeBatchSize = 80;
        private readonly HttpClient m_Client;

        public FigmaDesignClient()
        {
            m_Client = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            })
            {
                Timeout = TimeSpan.FromSeconds(100)
            };
        }

        public async Task<DesignDocument> LoadAsync(
            string fileUrlOrKey,
            string personalAccessToken,
            float exportScale,
            CancellationToken cancellationToken)
        {
            var fileKey = FigmaDocumentParser.ExtractFileKey(fileUrlOrKey);
            if (string.IsNullOrWhiteSpace(fileKey))
            {
                throw new ArgumentException("Figma 文件链接或 File Key 无效。", nameof(fileUrlOrKey));
            }

            if (string.IsNullOrWhiteSpace(personalAccessToken))
            {
                throw new ArgumentException("Figma Personal Access Token 不能为空。", nameof(personalAccessToken));
            }

            var fileJson = await GetStringAsync(
                $"https://api.figma.com/v1/files/{Uri.EscapeDataString(fileKey)}",
                personalAccessToken,
                cancellationToken);
            var parsed = FigmaDocumentParser.Parse(fileKey, fileJson, exportScale);
            var allNodeIds = parsed.RenderNodeIds.Concat(parsed.PreviewNodeIds).Distinct().ToArray();
            var renderUrls = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var offset = 0; offset < allNodeIds.Length; offset += NodeBatchSize)
            {
                var ids = allNodeIds.Skip(offset).Take(NodeBatchSize).ToArray();
                var url = $"https://api.figma.com/v1/images/{Uri.EscapeDataString(fileKey)}" +
                          $"?ids={Uri.EscapeDataString(string.Join(",", ids))}&format=png&scale={exportScale:0.##}";
                var imageJson = await GetStringAsync(url, personalAccessToken, cancellationToken);
                var images = JObject.Parse(imageJson)["images"] as JObject;
                if (images == null)
                {
                    continue;
                }

                foreach (var property in images.Properties())
                {
                    if (property.Value.Type == JTokenType.String)
                    {
                        renderUrls[property.Name] = (string)property.Value;
                    }
                }
            }

            foreach (var asset in parsed.Document.Assets)
            {
                if (renderUrls.TryGetValue(asset.Id, out var url))
                {
                    asset.Url = url;
                }
            }

            foreach (var page in parsed.Document.Pages)
            {
                if (renderUrls.TryGetValue(page.Id, out var url))
                {
                    page.PreviewUrl = url;
                }
            }

            parsed.Document.SourceLocation = fileUrlOrKey;
            parsed.Document.Normalize();
            var missing = parsed.Document.Assets.Where(x => string.IsNullOrWhiteSpace(x.Url)).Select(x => x.Name).Take(5).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException("Figma 未返回部分切图：" + string.Join("、", missing));
            }

            return parsed.Document;
        }

        public void Dispose()
        {
            m_Client.Dispose();
        }

        private async Task<string> GetStringAsync(string url, string token, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("X-Figma-Token", token.Trim());
            using var response = await m_Client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var message = TryReadError(body);
                throw new InvalidOperationException($"Figma API {(int)response.StatusCode}：{message}");
            }

            return body;
        }

        private static string TryReadError(string json)
        {
            try
            {
                return (string)JObject.Parse(json)["err"] ?? "请求失败";
            }
            catch
            {
                return "请求失败";
            }
        }
    }
}
