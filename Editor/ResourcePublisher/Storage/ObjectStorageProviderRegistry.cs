using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDeveloperKit.ResourcePublisher
{
    /// <summary>
    /// 定义 Object Storage Provider Registry 类型。
    /// </summary>
    public static class ObjectStorageProviderRegistry
    {
        /// <summary>         /// 存储 Providers。         /// </summary>
        private static readonly Dictionary<string, IObjectStorageProvider> s_Providers = new Dictionary<string, IObjectStorageProvider>(StringComparer.Ordinal);
        /// <summary>
        /// 记录 Initialized 状态。
        /// </summary>
        private static bool s_Initialized;

        public static IReadOnlyList<IObjectStorageProvider> Providers
        {
            get
            {
                EnsureInitialized();
                return s_Providers.Values
                    .OrderBy(x => x.DisplayName, StringComparer.Ordinal)
                    .ToList();
            }
        }

        /// <summary>
        /// 注册 member。
        /// </summary>
        /// <param name="provider">provider 参数。</param>
        public static void Register(IObjectStorageProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            var platformId = provider.PlatformId;
            if (string.IsNullOrWhiteSpace(platformId))
            {
                throw new ArgumentException("Provider platform id cannot be empty.", nameof(provider));
            }

            s_Providers[platformId] = provider;
        }

        /// <summary>
        /// 尝试获取 Provider。
        /// </summary>
        /// <param name="platformId">platform Id 参数。</param>
        /// <param name="provider">provider 参数。</param>
        /// <returns>条件满足时返回 true。</returns>
        public static bool TryGetProvider(string platformId, out IObjectStorageProvider provider)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(platformId))
            {
                provider = null;
                return false;
            }

            return s_Providers.TryGetValue(platformId, out provider);
        }

        /// <summary>
        /// 获取 Provider Or Fallback。
        /// </summary>
        /// <param name="platformId">platform Id 参数。</param>
        /// <returns>执行结果。</returns>
        public static IObjectStorageProvider GetProviderOrFallback(string platformId)
        {
            EnsureInitialized();
            if (TryGetProvider(platformId, out var provider))
            {
                return provider;
            }

            return new UnavailableObjectStorageProvider(platformId, platformId);
        }

        /// <summary>
        /// 确保 Initialized。
        /// </summary>
        private static void EnsureInitialized()
        {
            if (s_Initialized)
            {
                return;
            }

            s_Initialized = true;
            Register(new CosObjectStorageProvider());
        }
    }
}
