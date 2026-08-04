using System;
using System.Collections.Generic;
using System.IO;
using GameDeveloperKit.EditorCloud;

namespace GameDeveloperKit.EditorConfiguration
{
    internal static class EditorConfigValidation
    {
        private static readonly HashSet<string> s_CSharpKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
            "void", "volatile", "while"
        };

        public static bool TryNormalize(EditorGlobalConfig config, out string error)
        {
            error = null;
            var studio = config.UiPrefabStudio;
            studio.EnsureDefaults();
            if (studio.TargetWidth > 16384 || studio.TargetHeight > 16384)
            {
                error = "UI Prefab Studio 的目标分辨率不能超过 16384。";
                return false;
            }

            if (!studio.OutputRoot.Equals("Assets", StringComparison.OrdinalIgnoreCase) &&
                !studio.OutputRoot.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                error = "UI Prefab Studio 的输出目录必须位于 Assets 下。";
                return false;
            }

            if (!studio.GeneratedCodeRoot.Equals("Assets", StringComparison.OrdinalIgnoreCase) &&
                !studio.GeneratedCodeRoot.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                error = "UI Prefab Studio 的窗口代码目录必须位于 Assets 下。";
                return false;
            }

            if (IsValidNamespace(studio.CodeNamespace) is false)
            {
                error = $"UI Prefab Studio 的窗口代码命名空间无效：{studio.CodeNamespace}";
                return false;
            }

            var luban = config.Luban;
            if (TryNormalizePath(luban.TableDirectory, "配置表目录", out var tableDirectory, out error) is false ||
                TryNormalizePath(luban.GeneratedCodeDirectory, "生成代码目录", out var codeDirectory, out error) is false ||
                TryNormalizePath(luban.GeneratedDataDirectory, "导出数据目录", out var dataDirectory, out error) is false)
            {
                return false;
            }

            var codeNamespace = luban.CodeNamespace?.Trim() ?? string.Empty;
            if (IsValidNamespace(codeNamespace) is false)
            {
                error = $"代码命名空间无效：{luban.CodeNamespace}";
                return false;
            }

            var localization = config.Localization;
            localization.CatalogAssetGuid = localization.CatalogAssetGuid?.Trim() ?? string.Empty;
            localization.PreviewLocale = localization.PreviewLocale?.Trim() ?? string.Empty;

            var cloud = config.Cloud;
            if (TryNormalizeCloud(cloud, out error) is false)
            {
                return false;
            }

            luban.TableDirectory = tableDirectory;
            luban.GeneratedCodeDirectory = codeDirectory;
            luban.GeneratedDataDirectory = dataDirectory;
            luban.CodeNamespace = codeNamespace;
            return true;
        }

        private static bool TryNormalizeCloud(CloudProjectConfig cloud, out string error)
        {
            error = null;
            cloud.EnsureDefaults();
            cloud.ProviderId = cloud.ProviderId?.Trim() ?? string.Empty;
            return TryNormalizeCloudConnection(cloud.TencentCos, "腾讯 COS", out error) &&
                   TryNormalizeCloudConnection(cloud.AliyunOss, "阿里云 OSS", out error);
        }

        internal static bool TryValidateActiveCloudConnection(
            CloudProjectConfig cloud,
            bool requireCredentialProfile,
            out string error)
        {
            if (cloud == null)
            {
                error = "云配置不存在。";
                return false;
            }

            if (TryNormalizeCloud(cloud, out error) is false)
            {
                return false;
            }

            var providerId = cloud.ProviderId;
            var providerName = string.Equals(
                providerId,
                CloudProviderId.TencentCos,
                StringComparison.Ordinal)
                ? "腾讯 COS"
                : string.Equals(providerId, CloudProviderId.AliyunOss, StringComparison.Ordinal)
                    ? "阿里云 OSS"
                    : string.Empty;
            if (providerName.Length == 0)
            {
                error = "云配置必须选择腾讯 COS 或阿里云 OSS。";
                return false;
            }

            if (requireCredentialProfile && string.IsNullOrWhiteSpace(cloud.CredentialProfileName))
            {
                error = $"{providerName} 必须填写凭证 Profile。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(cloud.Bucket) || string.IsNullOrWhiteSpace(cloud.Region))
            {
                error = $"{providerName} 必须填写 Bucket 和 Region。";
                return false;
            }

            return CloudRegionValidator.TryValidate(providerId, cloud.Region, out error);
        }

        private static bool TryNormalizeCloudConnection(
            CloudConnectionConfig connection,
            string providerName,
            out string error)
        {
            error = null;
            connection.EnsureDefaults();
            connection.RootPrefix = connection.RootPrefix.Trim('/');
            connection.CdnBaseUrl = connection.CdnBaseUrl.TrimEnd('/');

            if (TryNormalizeOptionalHttpsOrigin(
                    connection.CdnBaseUrl,
                    $"{providerName} CDN 加速域名",
                    out var cdnBaseUrl,
                    out error) is false)
            {
                return false;
            }

            if (TryNormalizeCloudEndpoint(
                    connection.Endpoint,
                    out var endpoint,
                    out error) is false)
            {
                return false;
            }

            if (IsValidObjectPrefix(connection.RootPrefix) is false)
            {
                error = $"{providerName} 云根前缀必须是相对对象路径，不能包含反斜杠、空段、点段或控制字符。";
                return false;
            }

            connection.CdnBaseUrl = cdnBaseUrl.TrimEnd('/');
            connection.Endpoint = endpoint;
            return true;
        }

        private static bool TryNormalizeCloudEndpoint(
            string value,
            out string normalized,
            out string error)
        {
            normalized = value?.Trim().TrimEnd('/') ?? string.Empty;
            error = null;
            if (normalized.Length == 0)
            {
                return true;
            }

            if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) &&
                string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(uri.Host) is false &&
                string.IsNullOrWhiteSpace(uri.UserInfo) &&
                (uri.AbsolutePath.Length == 0 || uri.AbsolutePath == "/") &&
                string.IsNullOrEmpty(uri.Query) &&
                string.IsNullOrEmpty(uri.Fragment))
            {
                return true;
            }

            error = "Cloud Endpoint 必须是无路径、查询和片段的 HTTPS origin。";
            return false;
        }

        private static bool IsValidObjectPrefix(string prefix)
        {
            if (prefix.Length == 0)
            {
                return true;
            }

            if (prefix.IndexOf('\\') >= 0 || Uri.TryCreate(prefix, UriKind.Absolute, out _))
            {
                return false;
            }

            var segments = prefix.Split('/');
            for (var i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length == 0 ||
                    string.Equals(segments[i], ".", StringComparison.Ordinal) ||
                    string.Equals(segments[i], "..", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            for (var i = 0; i < prefix.Length; i++)
            {
                if (char.IsControl(prefix[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryNormalizeOptionalHttpsOrigin(
            string value,
            string label,
            out string normalized,
            out string error)
        {
            normalized = value?.Trim() ?? string.Empty;
            error = null;
            if (normalized.Length == 0)
            {
                return true;
            }

            if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) &&
                string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(uri.Host) is false &&
                string.IsNullOrWhiteSpace(uri.UserInfo) &&
                (uri.AbsolutePath.Length == 0 || uri.AbsolutePath == "/") &&
                string.IsNullOrEmpty(uri.Query) &&
                string.IsNullOrEmpty(uri.Fragment))
            {
                return true;
            }

            error = $"{label} 必须是无路径、查询和片段的 HTTPS origin。";
            return false;
        }

        private static bool TryNormalizePath(
            string value,
            string label,
            out string normalized,
            out string error)
        {
            normalized = (value ?? string.Empty).Trim().Replace('\\', '/');
            error = null;
            if (normalized.Length == 0)
            {
                error = $"{label}不能为空。";
                return false;
            }

            if (Path.IsPathRooted(normalized))
            {
                try
                {
                    normalized = Path.GetFullPath(normalized).Replace('\\', '/');
                    return true;
                }
                catch (Exception exception)
                {
                    error = $"{label}无效：{exception.Message}";
                    return false;
                }
            }

            var result = new List<string>();
            var segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < segments.Length; i++)
            {
                if (string.Equals(segments[i], ".", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(segments[i], "..", StringComparison.Ordinal))
                {
                    if (result.Count > 0 && string.Equals(result[result.Count - 1], "..", StringComparison.Ordinal) is false)
                    {
                        result.RemoveAt(result.Count - 1);
                    }
                    else
                    {
                        result.Add(segments[i]);
                    }

                    continue;
                }

                result.Add(segments[i]);
            }

            normalized = string.Join("/", result);
            if (normalized.Length == 0)
            {
                error = $"{label}不能为空。";
                return false;
            }

            return true;
        }

        private static bool IsValidNamespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var segments = value.Split('.');
            for (var i = 0; i < segments.Length; i++)
            {
                if (IsValidIdentifier(segments[i]) is false)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || s_CSharpKeywords.Contains(value) ||
                (char.IsLetter(value[0]) is false && value[0] != '_'))
            {
                return false;
            }

            for (var i = 1; i < value.Length; i++)
            {
                if (char.IsLetterOrDigit(value[i]) is false && value[i] != '_')
                {
                    return false;
                }
            }

            return true;
        }

    }
}
