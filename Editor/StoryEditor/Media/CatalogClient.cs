using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.Story.Media;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace GameDeveloperKit.StoryEditor.Media
{
    public interface ICatalogClient
    {
        UniTask<CatalogPage> SearchAsync(
            MediaKind kind,
            string query,
            string cursor,
            int limit,
            CancellationToken cancellationToken);
    }

    internal sealed class CatalogClient : ICatalogClient
    {
        private static readonly CatalogSessionCache s_SessionCache = new CatalogSessionCache();
        private readonly CatalogSettings m_Settings;
        private readonly CatalogSessionCache m_Cache;
        private readonly Func<Uri, int, CancellationToken, UniTask<string>> m_LoadJson;

        public CatalogClient(CatalogSettings settings)
            : this(settings, s_SessionCache, LoadJsonAsync)
        {
        }

        internal CatalogClient(
            CatalogSettings settings,
            CatalogSessionCache cache,
            Func<Uri, int, CancellationToken, UniTask<string>> loadJson)
        {
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            m_Cache = cache ?? throw new ArgumentNullException(nameof(cache));
            m_LoadJson = loadJson ?? throw new ArgumentNullException(nameof(loadJson));
        }

        public async UniTask<CatalogPage> SearchAsync(
            MediaKind kind,
            string query,
            string cursor,
            int limit,
            CancellationToken cancellationToken)
        {
            if (kind != MediaKind.Video)
            {
                throw new CatalogException(CatalogErrorKind.UnsupportedMediaKind, $"Catalog media kind is unsupported. kind:{kind}");
            }

            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }

            m_Settings.EnsureDefaults();
            m_Settings.Validate();
            cancellationToken.ThrowIfCancellationRequested();
            var cacheScope = $"{m_Settings.CatalogApiUrl}|{m_Settings.CdnBaseUrl}|{m_Settings.PreviewLocale}";
            if (m_Cache.TryGet(cacheScope, kind, query, cursor, limit, out var cached))
            {
                return cached;
            }

            var requestUri = BuildRequestUri(m_Settings, kind, query, cursor, limit);
            string json;
            try
            {
                json = await m_LoadJson(requestUri, m_Settings.TimeoutSeconds, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (CatalogException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new CatalogException(CatalogErrorKind.RequestFailed, $"Catalog request failed. endpoint:{EndpointLabel(requestUri)}", exception);
            }

            var page = ParsePage(json, kind, m_Settings.CdnBaseUrl);
            cancellationToken.ThrowIfCancellationRequested();
            m_Cache.Set(cacheScope, kind, query, cursor, limit, page);
            return page;
        }

        internal static CatalogPage ParsePage(string json, MediaKind expectedKind, string cdnBaseUrl)
        {
            CatalogPageData data;
            try
            {
                data = JsonConvert.DeserializeObject<CatalogPageData>(json);
            }
            catch (JsonException exception)
            {
                throw new CatalogException(CatalogErrorKind.InvalidResponse, "Catalog response JSON is invalid.", exception);
            }

            if (data?.Items == null)
            {
                throw new CatalogException(CatalogErrorKind.InvalidResponse, "Catalog response must contain an items array.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var items = new List<CatalogItem>(data.Items.Count);
            for (var i = 0; i < data.Items.Count; i++)
            {
                var source = data.Items[i];
                if (source == null || string.IsNullOrWhiteSpace(source.MediaId))
                {
                    throw new CatalogException(CatalogErrorKind.InvalidResponse, $"Catalog item at index {i} requires mediaId.");
                }

                if (ids.Add(source.MediaId.Trim()) is false)
                {
                    throw new CatalogException(CatalogErrorKind.DuplicateMediaId, $"Catalog response contains duplicate mediaId:{source.MediaId}");
                }

                if (TryParseKind(source.Kind, out var kind) is false || kind != expectedKind)
                {
                    throw new CatalogException(CatalogErrorKind.UnsupportedMediaKind, $"Catalog item has unsupported kind. mediaId:{source.MediaId}");
                }

                if (TryParseFormat(source.Format, out var format) is false)
                {
                    throw new CatalogException(CatalogErrorKind.InvalidResponse, $"Catalog item has invalid video format. mediaId:{source.MediaId}");
                }

                ValidateMetadata(source.Width, source.Height, source.Bitrate, source.DurationMs, source.MediaId);

                var renditions = new List<CatalogRendition>();
                for (var renditionIndex = 0; renditionIndex < (source.Renditions?.Count ?? 0); renditionIndex++)
                {
                    var rendition = source.Renditions[renditionIndex];
                    if (rendition == null)
                    {
                        throw new CatalogException(CatalogErrorKind.InvalidResponse, $"Catalog rendition is null. mediaId:{source.MediaId}");
                    }

                    ValidateMetadata(
                        rendition.Width,
                        rendition.Height,
                        rendition.Bitrate,
                        rendition.DurationMs,
                        source.MediaId);

                    renditions.Add(new CatalogRendition(
                        rendition.Label,
                        rendition.MediaId,
                        rendition.Location,
                        rendition.Width,
                        rendition.Height,
                        rendition.Bitrate,
                        rendition.DurationMs));
                }

                var item = new CatalogItem(
                    source.MediaId.Trim(),
                    string.IsNullOrWhiteSpace(source.Name) ? source.MediaId.Trim() : source.Name.Trim(),
                    kind,
                    source.Location,
                    format,
                    source.ThumbnailLocation,
                    source.Width,
                    source.Height,
                    source.Bitrate,
                    source.DurationMs,
                    renditions);
                CatalogReferenceFactory.CreateVideoReference(item, cdnBaseUrl);
                if (string.IsNullOrWhiteSpace(item.ThumbnailLocation) is false)
                {
                    CatalogReferenceFactory.ExpandHttpsLocation(cdnBaseUrl, item.ThumbnailLocation);
                }

                items.Add(item);
            }

            return new CatalogPage(items, data.NextCursor);
        }

        private static Uri BuildRequestUri(CatalogSettings settings, MediaKind kind, string query, string cursor, int limit)
        {
            var separator = settings.CatalogApiUrl.IndexOf('?', StringComparison.Ordinal) >= 0 ? "&" : "?";
            var url = settings.CatalogApiUrl + separator +
                      "kind=" + UnityWebRequest.EscapeURL(kind == MediaKind.Video ? "video" : "audio") +
                      "&query=" + UnityWebRequest.EscapeURL(query?.Trim() ?? string.Empty) +
                      "&cursor=" + UnityWebRequest.EscapeURL(cursor?.Trim() ?? string.Empty) +
                      "&limit=" + limit +
                      "&locale=" + UnityWebRequest.EscapeURL(settings.PreviewLocale);
            return new Uri(url, UriKind.Absolute);
        }

        private static async UniTask<string> LoadJsonAsync(Uri uri, int timeoutSeconds, CancellationToken cancellationToken)
        {
            using (var request = UnityWebRequest.Get(uri.AbsoluteUri))
            using (cancellationToken.Register(request.Abort))
            {
                request.timeout = timeoutSeconds;
                try
                {
                    await request.SendWebRequest();
                }
                catch (Exception exception)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    throw new CatalogException(CatalogErrorKind.RequestFailed, $"Catalog request failed. endpoint:{EndpointLabel(uri)}", exception);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (request.result != UnityWebRequest.Result.Success || request.responseCode < 200 || request.responseCode >= 300)
                {
                    throw new CatalogException(
                        CatalogErrorKind.RequestFailed,
                        $"Catalog request failed. endpoint:{EndpointLabel(uri)} status:{request.responseCode} error:{request.error}");
                }

                return request.downloadHandler?.text ?? string.Empty;
            }
        }

        private static bool TryParseKind(string value, out MediaKind kind)
        {
            if (string.Equals(value, "video", StringComparison.Ordinal))
            {
                kind = MediaKind.Video;
                return true;
            }

            if (string.Equals(value, "audio", StringComparison.Ordinal))
            {
                kind = MediaKind.Audio;
                return true;
            }

            kind = default;
            return false;
        }

        private static string EndpointLabel(Uri uri)
        {
            return uri?.GetLeftPart(UriPartial.Path) ?? "unknown";
        }

        private static void ValidateMetadata(int width, int height, int bitrate, long durationMs, string mediaId)
        {
            if (width < 0 || height < 0 || bitrate < 0 || durationMs < 0)
            {
                throw new CatalogException(
                    CatalogErrorKind.InvalidResponse,
                    $"Catalog item contains negative media metadata. mediaId:{mediaId}");
            }
        }

        private static bool TryParseFormat(string value, out VideoFormat format)
        {
            if (string.Equals(value, "hls", StringComparison.Ordinal))
            {
                format = VideoFormat.Hls;
                return true;
            }

            if (string.Equals(value, "mp4", StringComparison.Ordinal))
            {
                format = VideoFormat.Mp4;
                return true;
            }

            format = default;
            return false;
        }

        [Serializable]
        private sealed class CatalogPageData
        {
            [JsonProperty("items")]
            public List<CatalogItemData> Items { get; set; }

            [JsonProperty("nextCursor")]
            public string NextCursor { get; set; }
        }

        [Serializable]
        private sealed class CatalogItemData
        {
            [JsonProperty("mediaId")]
            public string MediaId { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("kind")]
            public string Kind { get; set; }

            [JsonProperty("location")]
            public string Location { get; set; }

            [JsonProperty("format")]
            public string Format { get; set; }

            [JsonProperty("thumbnail")]
            public string ThumbnailLocation { get; set; }

            [JsonProperty("width")]
            public int Width { get; set; }

            [JsonProperty("height")]
            public int Height { get; set; }

            [JsonProperty("bitrate")]
            public int Bitrate { get; set; }

            [JsonProperty("durationMs")]
            public long DurationMs { get; set; }

            [JsonProperty("renditions")]
            public List<CatalogRenditionData> Renditions { get; set; }
        }

        [Serializable]
        private sealed class CatalogRenditionData
        {
            [JsonProperty("label")]
            public string Label { get; set; }

            [JsonProperty("mediaId")]
            public string MediaId { get; set; }

            [JsonProperty("location")]
            public string Location { get; set; }

            [JsonProperty("width")]
            public int Width { get; set; }

            [JsonProperty("height")]
            public int Height { get; set; }

            [JsonProperty("bitrate")]
            public int Bitrate { get; set; }

            [JsonProperty("durationMs")]
            public long DurationMs { get; set; }
        }
    }
}
