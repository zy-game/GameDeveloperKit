using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameDeveloperKit.EditorCloud;
using GameDeveloperKit.EditorConfiguration;
using GameDeveloperKit.Story.Media;
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
        private const string EmptyCatalogJson =
            "{\"schemaVersion\":1,\"generation\":0,\"items\":[]}";
        private static readonly CatalogSessionCache s_SessionCache = new CatalogSessionCache();
        private readonly StoryMediaProjectConfig m_Settings;
        private readonly Func<string> m_PublicBaseUrlProvider;
        private readonly CatalogSessionCache m_Cache;
        private readonly Func<Uri, int, CancellationToken, UniTask<string>> m_LoadJson;
        private readonly CloudService m_CloudService;
        private readonly Func<CloudProjectConfig> m_CloudConfigProvider;

        public CatalogClient(StoryMediaProjectConfig settings)
            : this(
                settings,
                ResolveConfiguredPublicBaseUrl,
                s_SessionCache,
                LoadJsonAsync,
                CloudService.Shared,
                () => EditorGlobalConfig.LoadOrCreate().Cloud)
        {
        }

        internal CatalogClient(
            StoryMediaProjectConfig settings,
            Func<string> publicBaseUrlProvider,
            CatalogSessionCache cache,
            Func<Uri, int, CancellationToken, UniTask<string>> loadJson,
            CloudService cloudService = null,
            Func<CloudProjectConfig> cloudConfigProvider = null)
        {
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            m_PublicBaseUrlProvider = publicBaseUrlProvider ??
                                      throw new ArgumentNullException(nameof(publicBaseUrlProvider));
            m_Cache = cache ?? throw new ArgumentNullException(nameof(cache));
            m_LoadJson = loadJson ?? throw new ArgumentNullException(nameof(loadJson));
            m_CloudService = cloudService;
            m_CloudConfigProvider = cloudConfigProvider;
        }

        private static string ResolveConfiguredPublicBaseUrl()
        {
            if (CloudPublicUrlResolver.TryResolve(
                    EditorGlobalConfig.LoadOrCreate().Cloud,
                    out var publicBaseUrl,
                    out var error))
            {
                return publicBaseUrl;
            }

            throw new CatalogException(
                CatalogErrorKind.InvalidSettings,
                error ?? "云配置无效。");
        }

        public UniTask<CatalogPage> SearchAsync(
            MediaKind kind,
            string query,
            string cursor,
            int limit,
            CancellationToken cancellationToken)
        {
            return SearchAsync(kind, query, cursor, limit, false, cancellationToken);
        }

        internal async UniTask<CatalogPage> SearchAsync(
            MediaKind kind,
            string query,
            string cursor,
            int limit,
            bool bypassCache,
            CancellationToken cancellationToken)
        {
            ValidateSearch(kind, limit);
            var publicBaseUrl = m_PublicBaseUrlProvider();
            CatalogSettingsValidation.ValidateForRequest(m_Settings, publicBaseUrl);
            cancellationToken.ThrowIfCancellationRequested();

            var cacheScope = publicBaseUrl.TrimEnd('/');
            if (bypassCache)
            {
                m_Cache.Clear(cacheScope);
            }
            else if (m_Cache.TryGet(cacheScope, kind, query, cursor, limit, out var cachedPage))
            {
                return cachedPage;
            }

            if (m_Cache.TryGetDocument(cacheScope, out var document) is false)
            {
                string json;
                if (m_CloudService != null && m_CloudConfigProvider != null)
                {
                    // 媒体库始终读取 COS/OSS 源站，避免 CDN catalog.json 缓存旧条目。
                    json = await LoadOriginJsonAsync(cancellationToken);
                }
                else
                {
                    var requestUri = BuildCatalogUri(publicBaseUrl, bypassCache);
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
                        throw new CatalogException(
                            CatalogErrorKind.RequestFailed,
                            $"Catalog request failed. endpoint:{EndpointLabel(requestUri)}",
                            exception);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                document = HlsCatalogCodec.ParseDocument(json, publicBaseUrl, true);
                m_Cache.SetDocument(cacheScope, document);
            }

            var page = HlsCatalogCodec.Search(document, kind, query, cursor, limit);
            cancellationToken.ThrowIfCancellationRequested();
            m_Cache.Set(cacheScope, kind, query, cursor, limit, page);
            return page;
        }

        internal static CatalogPage ParsePage(string json, MediaKind expectedKind, string cdnBaseUrl)
        {
            var document = HlsCatalogCodec.ParseDocument(json, cdnBaseUrl, false);
            return HlsCatalogCodec.Search(document, expectedKind, string.Empty, null, int.MaxValue);
        }

        internal static Uri BuildCatalogUri(string cdnBaseUrl, bool bypassCache)
        {
            var baseUrl = cdnBaseUrl?.Trim().TrimEnd('/') ?? string.Empty;
            var suffix = bypassCache
                ? "/catalog.json?catalogRevision=" + DateTimeOffset.UtcNow.UtcDateTime.Ticks
                : "/catalog.json";
            return new Uri(baseUrl + suffix, UriKind.Absolute);
        }

        internal static string BuildCatalogObjectKey(string rootPrefix)
        {
            var normalized = rootPrefix?.Trim().Trim('/') ?? string.Empty;
            return normalized.Length == 0 ? "catalog.json" : normalized + "/catalog.json";
        }

        private async UniTask<string> LoadOriginJsonAsync(CancellationToken cancellationToken)
        {
            var objectKey = BuildCatalogObjectKey(m_CloudConfigProvider()?.RootPrefix);
            try
            {
                var result = await m_CloudService.GetObjectAsync(
                    new CloudObjectGetRequest(objectKey),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return result.Content;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (CloudException exception) when (exception.Kind == CloudFailureKind.NotFound)
            {
                throw new CatalogException(
                    CatalogErrorKind.RequestFailed,
                    $"Catalog object is missing. key:{objectKey}. {exception.Message}",
                    exception);
            }
            catch (CloudException exception)
            {
                throw new CatalogException(
                    CatalogErrorKind.RequestFailed,
                    $"Catalog request failed. origin:{objectKey}. {exception.Message}",
                    exception);
            }
        }

        private static void ValidateSearch(MediaKind kind, int limit)
        {
            if (kind != MediaKind.Video && kind != MediaKind.Audio)
            {
                throw new CatalogException(
                    CatalogErrorKind.UnsupportedMediaKind,
                    $"Catalog media kind is unsupported. kind:{kind}");
            }

            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }
        }

        private static async UniTask<string> LoadJsonAsync(
            Uri uri,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            using (var request = UnityWebRequest.Get(uri.AbsoluteUri))
            using (cancellationToken.Register(request.Abort))
            {
                request.timeout = timeoutSeconds;
                try
                {
                    await request.SendWebRequest();
                }
                catch (UnityWebRequestException exception) when (
                    exception.ResponseCode == 404 || request.responseCode == 404)
                {
                    return EmptyCatalogJson;
                }
                catch (Exception exception)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    throw new CatalogException(
                        CatalogErrorKind.RequestFailed,
                        $"Catalog request failed. endpoint:{EndpointLabel(uri)}",
                        exception);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (request.responseCode == 404)
                {
                    return EmptyCatalogJson;
                }

                if (request.result != UnityWebRequest.Result.Success ||
                    request.responseCode < 200 ||
                    request.responseCode >= 300)
                {
                    throw new CatalogException(
                        CatalogErrorKind.RequestFailed,
                        $"Catalog request failed. endpoint:{EndpointLabel(uri)} status:{request.responseCode} error:{request.error}");
                }

                return request.downloadHandler?.text ?? string.Empty;
            }
        }

        private static string EndpointLabel(Uri uri)
        {
            return uri?.GetLeftPart(UriPartial.Path) ?? "unknown";
        }
    }
}
