using System;
using GameDeveloperKit.EditorConfiguration;

namespace GameDeveloperKit.EditorCloud
{
    public static class CloudPublicUrlResolver
    {
        public static string Resolve(CloudProjectConfig config)
        {
            return TryResolve(config, out var resolved, out _)
                ? resolved
                : string.Empty;
        }

        public static bool TryResolve(
            CloudProjectConfig config,
            out string resolved,
            out string error)
        {
            resolved = string.Empty;
            if (EditorConfigValidation.TryValidateActiveCloudConnection(
                    config,
                    false,
                    out error) is false)
            {
                return false;
            }

            var baseUrl = config.CdnBaseUrl?.Trim().TrimEnd('/') ?? string.Empty;
            if (baseUrl.Length == 0)
            {
                baseUrl = ResolveEndpoint(config);
                if (baseUrl.Length == 0)
                {
                    error = "无法根据当前云配置生成媒体库公开地址。";
                    return false;
                }
            }

            var rootPrefix = config.RootPrefix?.Trim().Trim('/') ?? string.Empty;
            resolved = rootPrefix.Length == 0
                ? baseUrl
                : baseUrl + "/" + rootPrefix;
            error = null;
            return true;
        }

        public static string ResolveOriginBaseUrl(CloudProjectConfig config)
        {
            return TryResolveOriginBaseUrl(config, out var resolved, out _)
                ? resolved
                : string.Empty;
        }

        public static bool TryResolveOriginBaseUrl(
            CloudProjectConfig config,
            out string resolved,
            out string error)
        {
            resolved = string.Empty;
            if (EditorConfigValidation.TryValidateActiveCloudConnection(
                    config,
                    false,
                    out error) is false)
            {
                return false;
            }

            resolved = ResolveEndpoint(config);
            if (resolved.Length > 0)
            {
                error = null;
                return true;
            }

            error = "无法根据当前云配置生成媒体源站地址。";
            return false;
        }

        private static string ResolveEndpoint(CloudProjectConfig config)
        {
            var customEndpoint = config.Endpoint?.Trim().TrimEnd('/') ?? string.Empty;
            if (customEndpoint.Length > 0)
            {
                return customEndpoint;
            }

            var bucket = config.Bucket?.Trim() ?? string.Empty;
            var region = config.Region?.Trim() ?? string.Empty;
            if (bucket.Length == 0 || region.Length == 0)
            {
                return string.Empty;
            }

            if (string.Equals(config.ProviderId, CloudProviderId.TencentCos, StringComparison.Ordinal))
            {
                return $"https://{bucket}.cos.{region}.myqcloud.com";
            }

            return string.Equals(config.ProviderId, CloudProviderId.AliyunOss, StringComparison.Ordinal)
                ? $"https://{bucket}.oss-{region}.aliyuncs.com"
                : string.Empty;
        }
    }

    internal static class CloudRegionValidator
    {
        public static bool TryValidate(string providerId, string region, out string error)
        {
            var normalized = region?.Trim() ?? string.Empty;
            if (IsRegionId(normalized) is false)
            {
                error = "Cloud Region 只能包含小写字母、数字和连字符。";
                return false;
            }

            if (string.Equals(providerId, CloudProviderId.AliyunOss, StringComparison.Ordinal))
            {
                if (normalized.StartsWith("oss-", StringComparison.Ordinal))
                {
                    error = "阿里云 OSS Region 只填写地域 ID，例如 cn-chengdu，不要填写 oss-cn-chengdu。";
                    return false;
                }

                if (normalized.StartsWith("cn-", StringComparison.Ordinal) ||
                    IsNumberedRegionId(normalized))
                {
                    error = null;
                    return true;
                }

                error = "阿里云 OSS Region 无效：中国内地填写 cn-*（例如 cn-chengdu），海外地域填写带编号的地域 ID（例如 ap-southeast-1）。";
                return false;
            }

            if (string.Equals(providerId, CloudProviderId.TencentCos, StringComparison.Ordinal))
            {
                if (normalized.StartsWith("cn-", StringComparison.Ordinal) is false &&
                    IsNumberedRegionId(normalized) is false &&
                    normalized.IndexOf('-') == 2)
                {
                    error = null;
                    return true;
                }

                error = "腾讯 COS Region 无效，应填写 ap-*、na-*、eu-* 等地域 ID（例如 ap-chengdu）。";
                return false;
            }

            error = "云配置必须选择腾讯 COS 或阿里云 OSS。";
            return false;
        }

        public static void ValidateOrThrow(string providerId, string region)
        {
            if (TryValidate(providerId, region, out var error))
            {
                return;
            }

            throw new CloudException(
                CloudFailureKind.InvalidConfiguration,
                error,
                providerId);
        }

        private static bool IsRegionId(string value)
        {
            if (value.Length < 4 || value[0] == '-' || value[value.Length - 1] == '-')
            {
                return false;
            }

            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if ((character < 'a' || character > 'z') &&
                    (character < '0' || character > '9') &&
                    character != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsNumberedRegionId(string value)
        {
            var lastDash = value.LastIndexOf('-');
            if (lastDash <= 2 || lastDash == value.Length - 1)
            {
                return false;
            }

            for (var i = lastDash + 1; i < value.Length; i++)
            {
                if (value[i] < '0' || value[i] > '9')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
