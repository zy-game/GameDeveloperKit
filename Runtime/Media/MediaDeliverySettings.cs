using System;
using Newtonsoft.Json;

namespace GameDeveloperKit.Media
{
    /// <summary>
    /// 媒体分发端点配置。运行时通过 GDKSetting.json 的 mediaDelivery section 读取，编辑器由云配置页写入。
    /// </summary>
    [Serializable]
    public sealed class MediaDeliverySettings
    {
        [JsonProperty("originBaseUrl")]
        public string OriginBaseUrl { get; set; } = string.Empty;

        [JsonProperty("cdnBaseUrl")]
        public string CdnBaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// 剧情视频用 AVPro DisplayUGUI 渲染（自动处理 Linear 色彩空间，避免画面泛白）。
        /// </summary>
        [JsonProperty("useDisplayUGUI")]
        public bool UseDisplayUGUI { get; set; }

        [JsonIgnore]
        public bool UsesCdn => string.IsNullOrEmpty(CdnBaseUrl) is false;

        public void EnsureDefaults()
        {
            OriginBaseUrl = OriginBaseUrl?.Trim() ?? string.Empty;
            CdnBaseUrl = CdnBaseUrl?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Sets the public endpoints. Credentials never belong in this model.
        /// </summary>
        public void SetPublicUrls(string originBaseUrl, string cdnBaseUrl = null)
        {
            OriginBaseUrl = NormalizeBaseUrl(originBaseUrl, nameof(originBaseUrl), false);
            CdnBaseUrl = NormalizeBaseUrl(cdnBaseUrl, nameof(cdnBaseUrl), true);
        }

        internal static string NormalizeBaseUrl(string value, string parameterName, bool optional)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (optional)
                {
                    return string.Empty;
                }

                throw new ArgumentException("Media delivery origin base URL cannot be empty.", parameterName);
            }

            var normalized = value.Trim().TrimEnd('/');
            if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) is false ||
                string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) is false ||
                string.IsNullOrWhiteSpace(uri.Host) ||
                string.IsNullOrWhiteSpace(uri.UserInfo) is false ||
                string.IsNullOrWhiteSpace(uri.Query) is false ||
                string.IsNullOrWhiteSpace(uri.Fragment) is false)
            {
                throw new ArgumentException("Media delivery base URL must be an absolute HTTPS URL without credentials, query, or fragment.", parameterName);
            }

            return normalized;
        }
    }
}
