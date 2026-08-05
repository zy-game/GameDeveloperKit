using System;
using UnityEngine;

namespace GameDeveloperKit.Media
{
    /// <summary>
    /// Public media delivery endpoints shipped with the player.
    /// </summary>
    public sealed class MediaDeliverySettings : ScriptableObject
    {
        public const string ResourcePath = "GameDeveloperKit/MediaDeliverySettings";

        [SerializeField] private string m_OriginBaseUrl = string.Empty;
        [SerializeField] private string m_CdnBaseUrl = string.Empty;
        [SerializeField] private bool m_UseDisplayUGUI;

        public string OriginBaseUrl => m_OriginBaseUrl;

        public string CdnBaseUrl => m_CdnBaseUrl;

        public bool UsesCdn => string.IsNullOrEmpty(m_CdnBaseUrl) is false;

        /// <summary>
        /// 剧情视频用 AVPro DisplayUGUI 渲染（自动处理 Linear 色彩空间，避免画面泛白）。
        /// </summary>
        public bool UseDisplayUGUI => m_UseDisplayUGUI;

        /// <summary>
        /// Sets the public endpoints. Credentials never belong in this asset.
        /// </summary>
        public void SetPublicUrls(string originBaseUrl, string cdnBaseUrl = null)
        {
            m_OriginBaseUrl = NormalizeBaseUrl(originBaseUrl, nameof(originBaseUrl), false);
            m_CdnBaseUrl = NormalizeBaseUrl(cdnBaseUrl, nameof(cdnBaseUrl), true);
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
